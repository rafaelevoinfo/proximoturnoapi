# Registro de uso e custo das chamadas de LLM

## O problema

Hoje o pipeline de indexação gasta dinheiro e não deixa rastro do gasto. O log conta o que
aconteceu — qual modelo aceitou a extração, quantas correções a revisão aplicou — mas não
quanto custou, e nem sempre conta que uma chamada *paga* foi descartada. Três exemplos
observados em dev, todos invisíveis em dinheiro:

- Uma extração que volta com `finish_reason: length` transcreveu meio manual, foi cobrada e
  descartada. O log avisa que truncou; ninguém sabe quanto custou.
- Um bloco do revisor morreu com `finish_reason: error` da OpenRouter. Cobrado do mesmo jeito.
- A cascata de OCR escala para um modelo mais caro sem que o preço das duas tentativas
  apareça em lugar nenhum.

O objetivo é responder, por SQL, a três perguntas: **quanto custou indexar este manual**,
**quanto gastei neste mês e em quê**, e **quanto do gasto foi em chamada que não serviu**.

## Escopo

Entram os três pontos pagos que existem hoje, todos dentro do worker de indexação: a extração
por OCR (`PdfTextExtractor`), a revisão do markdown (`LlmMarkdownRevisor`) e a geração de
embeddings (`EmbeddingExtractor`).

Fica fora, por YAGNI: endpoint ou tela de relatório. O ledger é consultado por SQL; se um dia
uma tela fizer sentido, é tarefa própria.

## Decisões e a evidência de cada uma

**O custo vem da OpenRouter, não de uma tabela de preços nossa.** Ela devolve o custo real
cobrado em `usage.cost`, e esta conta o manda sem precisar pedir `usage.include`. Uma tabela
local erraria em dois eixos: quando os preços mudam e quando a rota muda de provider. O segundo
não é hipotético — sondando o mesmo modelo em duas chamadas seguidas, ele foi atendido pela
`StreamLake` e depois pela `Parasail`.

**O SDK descarta esse campo.** Sondado: `UsageDetails` carrega apenas contagens de tokens e
`AdditionalCounts` só traz campos da própria OpenAI. O `cost` não chega de nenhuma forma.

**Então a captura é uma `PipelinePolicy` no cliente, lendo a resposta.** Foi medido que isso é
seguro: com `BufferResponse = true` — o padrão nos dois caminhos que usamos — `Response.Content`
é memória que pode ser relida, e o SDK interpretou a mesma resposta normalmente depois da
policy. A alternativa, consultar `GET /api/v1/generation?id=` com o `ResponseId`, foi avaliada e
recusada: o endpoint é proprietário da OpenRouter, não existe na API da OpenAI, então não
compraria portabilidade nenhuma — e custaria uma chamada HTTP extra por chamada de LLM, além de
não cobrir embedding, cujo id o SDK não expõe.

**Custo por chamada não é portável, e nenhum desenho conserta isso.** Não está na especificação
da OpenAI: o `usage.cost` é invenção da OpenRouter, como seria qualquer outro campo de preço em
qualquer outro provedor. Trocar de provedor compatível mantém tokens, duração, id e
`finish_reason`, que são padrão, e derruba o custo para nulo. Está em *Riscos*.

**Posição `PerTry`, não `PerCall`.** O `ClientRetryPolicy` repete dentro de uma mesma chamada
lógica, e uma tentativa que morre do nosso lado por timeout não impede o servidor de concluir e
cobrar — receio que já está escrito em comentário no `PdfTextExtractor` hoje. Em `PerTry` cada
tentativa vira uma linha, e esse gasto deixa de ser invisível.

## Peças

Diretório novo `Src/Infrastructure/IA/`, com os contratos em `Src/Application/UseCases/IA/`,
no mesmo padrão que RAG já usa. O diretório não é "RAG" de propósito: um chat de regras no
futuro usa as mesmas peças.

| Peça | Arquivo | Responsabilidade |
|---|---|---|
| `IFabricaOpenRouter` | `Application/UseCases/IA/IFabricaOpenRouter.cs` | contrato da fábrica |
| `FabricaOpenRouter` | `Infrastructure/IA/FabricaOpenRouter.cs` | único lugar que lê a chave e monta `OpenAIClient`, já com a policy instalada |
| `PoliticaUsoLlm` | `Infrastructure/IA/PoliticaUsoLlm.cs` | `PipelinePolicy`: cronometra, deixa passar, lê a resposta, entrega a linha |
| `EscopoUsoLlm` | `Application/UseCases/IA/EscopoUsoLlm.cs` | `AsyncLocal` com o alvo (jogo, link, rótulo) |
| `IRegistradorUsoLlm` / `RegistradorUsoLlm` | `Application/UseCases/IA/`, `Infrastructure/IA/` | grava a linha e emite uma linha de log; nunca lança |
| `IUsoLlmRepository` / `UsoLlmRepository` | `Infrastructure/Repositories/UsoLlmRepository.cs` | persistência, no padrão `IBaseRepository` |
| `UsoLlm`, `OperacaoLlm`, `DesfechoLlm` | `Infrastructure/Models/UsoLlm.cs` | entidade e enums, como `JogoLinkIndexacao` faz |

Não há consulta de custo, cliente HTTP extra, retentativa com espera nem disjuntor: o custo
chega na própria resposta que já estamos recebendo.

### Contratos

```csharp
public enum OperacaoLlm : short {
    Ocr = 0,
    RevisaoMarkdown = 1,
    Embedding = 2
}

public enum DesfechoLlm : short {
    /// <summary>Resposta completa. finish_reason "stop" ou ausente (embedding).</summary>
    Ok = 0,
    /// <summary>finish_reason "length": pago e descartado pelo chamador.</summary>
    Truncado = 1,
    /// <summary>Qualquer outro finish_reason, incluindo o "error" da OpenRouter.</summary>
    ErroDoModelo = 2,
    /// <summary>Resposta com status fora de 2xx.</summary>
    ErroHttp = 3,
    /// <summary>A chamada lançou antes de haver resposta. Sem tokens e sem custo.</summary>
    Excecao = 4
}

public interface IFabricaOpenRouter {
    IChatClient CriarChat(string modelo, OperacaoLlm operacao, TimeSpan timeout, int tentativas);
    IEmbeddingGenerator<string, Embedding<float>> CriarEmbedding(string modelo);
}

public sealed record RegistroUsoLlm(
    OperacaoLlm Operacao,
    string ModeloPedido,
    string? ModeloRespondeu,
    string? Provider,
    int TokensEntrada,
    int TokensSaida,
    int TokensRaciocinio,
    int TokensCache,
    decimal? CustoUsd,
    int DuracaoMs,
    DesfechoLlm Desfecho,
    string? Detalhe,
    string? IdGeracao);

public interface IRegistradorUsoLlm {
    /// <summary>Nunca lança. O alvo, o trace id e o momento vêm do ambiente.</summary>
    Task RegistrarAsync(RegistroUsoLlm registro);

    /// <summary>Caminho síncrono, para o override sync da policy. Nunca lança.</summary>
    void Registrar(RegistroUsoLlm registro);
}

public sealed record AlvoUsoLlm(int? IdJogo, int? IdJogoLink, string? Alvo);

public static class EscopoUsoLlm {
    public static AlvoUsoLlm? Atual { get; }
    /// <summary>Descartar restaura o escopo anterior.</summary>
    public static IDisposable Abrir(int? idJogo, int? idJogoLink, string? alvo);
}
```

`RegistrarAsync` não recebe `CancellationToken` de propósito: ver *Regra do dinheiro*.

### A fábrica

Lê `OPENROUTER_API_KEY` **na primeira vez que um cliente é pedido**, não no construtor, e
lança `InvalidOperationException("OPENROUTER_API_KEY não configurada.")` se faltar. Ler no
construtor mudaria comportamento: hoje a chave é lida dentro da lambda do DI, e o comentário no
`Program.cs` registra o porquê — faltar chave derruba só a indexação, que roda em background e
já trata erro por manual, em vez de impedir a aplicação de subir.

Cada cliente nasce com uma instância de `PoliticaUsoLlm` que já sabe **qual modelo foi pedido e
para que operação** — é assim que a policy identifica a etapa sem ler o corpo da requisição, que
no OCR tem 47MB de base64 e não pode ser tocado. Logo, o cache interno de `OpenAIClient` é por
`(modelo, operacao, timeout, tentativas)`. Na prática são quatro entradas: dois modelos de OCR,
o revisor e o embedding.

Preserva as configurações de hoje, que não são arbitrárias:

| Cliente | Timeout | Tentativas | Motivo já documentado no código |
|---|---|---|---|
| OCR | 10 min | 1 | transcrever um manual inteiro leva minutos; a cascata já é a nossa retentativa |
| Revisor | 5 min | 2 | cada bloco leva dezenas de segundos porque o modelo raciocina |
| Embedding | 2 min | 2 | chamada curta e idempotente |

### A policy

```csharp
public override async ValueTask ProcessAsync(PipelineMessage mensagem, IReadOnlyList<PipelinePolicy> pipeline, int indice) {
    var relogio = Stopwatch.StartNew();
    try {
        await ProcessNextAsync(mensagem, pipeline, indice);
    } finally {
        relogio.Stop();
        await RegistrarAsync(mensagem, relogio.Elapsed);   // nunca lança
    }
}
```

O `finally` é o que garante a linha tanto no sucesso quanto na exceção. O override síncrono
existe porque `PipelinePolicy` exige os dois; ele chama `Registrador.Registrar`, sem
`GetAwaiter().GetResult()` em lugar nenhum.

O que ela lê da resposta, tudo confirmado em sonda nos dois caminhos:

| Campo do registro | Origem no JSON |
|---|---|
`ModeloRespondeu` | `model` |
`Provider` | `provider` |
`TokensEntrada` / `TokensSaida` | `usage.prompt_tokens` / `usage.completion_tokens` (ausente em embedding, vira 0) |
`TokensRaciocinio` | `usage.completion_tokens_details.reasoning_tokens` |
`TokensCache` | `usage.prompt_tokens_details.cached_tokens` |
`CustoUsd` | `usage.cost` |
`IdGeracao` | `id` (`gen-...` em chat, `gen-emb-...` em embedding) |
`Desfecho` | status HTTP + `choices[0].finish_reason` |

Ela **só lê a resposta**. A requisição não é lida, reescrita nem logada — é justamente por não
precisar mexer nela que este desenho é viável com um PDF de 47MB no corpo.

Guardas obrigatórias, porque uma policy que lança derruba a chamada de LLM:

- `try/catch` em volta de toda a observação. Falha de leitura vira Debug e linha com menos
  campos, nunca exceção.
- `BufferResponse == false` (streaming, que ninguém usa hoje): não registra e loga Debug uma
  vez, para não abrir buraco silencioso se alguém usar depois.
- Corpo vazio ou não-JSON: linha com status e duração, sem números.

### Registro no `Program.cs`

```csharp
builder.Services.AddSingleton<IFabricaOpenRouter, FabricaOpenRouter>();
builder.Services.AddSingleton<IRegistradorUsoLlm, RegistradorUsoLlm>();
builder.Services.AddScoped<IUsoLlmRepository, UsoLlmRepository>();

builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
    sp.GetRequiredService<IFabricaOpenRouter>().CriarEmbedding(IAModel.EMBEDDING_MODEL));

builder.Services.AddKeyedSingleton<IChatClient>(LlmMarkdownRevisor.ChaveChat, (sp, _) =>
    sp.GetRequiredService<IFabricaOpenRouter>()
      .CriarChat(IAModel.REVISOR_MODEL, OperacaoLlm.RevisaoMarkdown, TimeSpan.FromMinutes(5), tentativas: 2));
```

O registrador é singleton e o `DbContext` é scoped, então ele abre o próprio escopo por
`IServiceScopeFactory` para alcançar o repositório — o mesmo que o `IndexacaoManuaisWorker`
já faz por item da fila.

Migração: `dotnet ef migrations add UsoLlm --project Src/ProximoTurnoApi.csproj`.

## A tabela `USO_LLM`

Uma linha por tentativa de chamada. Um manual típico gera ~10 (1 de OCR, 8 de revisão, 1 lote
de embedding).

| Coluna | Tipo | Nulo | Por que existe |
|---|---|---|---|
`ID` | int | não | chave, via `BaseModel`
`MOMENTO` | datetime(6) | não | quando a chamada terminou
`TRACE_ID` | varchar(32) | sim | o `RastreioBackground` dá um trace id por item da fila e o Serilog o imprime em toda linha; guardado aqui, uma linha cara leva direto ao trecho do log
`OPERACAO` | smallint | não | `OperacaoLlm`
`ID_JOGO` | int | sim | **sem foreign key de propósito**
`ID_JOGO_LINK` | int | sim | **sem foreign key de propósito**: apagar um link não pode apagar o registro do que ele custou
`ALVO` | varchar(200) | sim | `"Balde De Caranguejo / FAQ"`, para ler sem join
`MODELO_PEDIDO` | varchar(100) | não | o que pedimos, vindo da fábrica
`MODELO_RESPONDEU` | varchar(100) | sim | o que respondeu, que pode vir com a versão exata
`PROVIDER` | varchar(60) | sim | quem atendeu. Medido variando entre chamadas do mesmo modelo, e é o que explica preço diferente
`TOKENS_ENTRADA` | int | não | —
`TOKENS_SAIDA` | int | não | zero em embedding
`TOKENS_RACIOCINIO` | int | não | explica a conta do revisor novo, que raciocina antes de responder
`TOKENS_CACHE` | int | não | entrada servida de cache
`CUSTO_USD` | decimal(18,10) | sim | uma chamada custa `0.000000343`. Nulo é "não sei", zero é "não custou"
`DURACAO_MS` | int | não | os ~18s por bloco do revisor, medidos
`DESFECHO` | smallint | não | `DesfechoLlm`
`DETALHE` | varchar(300) | sim | `finish_reason` cru, ou o corpo do erro HTTP truncado, ou tipo e mensagem da exceção
`ID_GERACAO` | varchar(80) | sim | o `gen-...`: audita a chamada no painel da OpenRouter

Índices: `MOMENTO` e `ID_JOGO_LINK`.

## Fluxo

`SincronizarManual.ProcessarAsync` abre um escopo de alvo logo depois de montar o `contexto`,
envolvendo extração, revisão e embedding:

```csharp
var contexto = new ContextoManual(estado.NomeJogo, estado.TituloLink);
using var _ = EscopoUsoLlm.Abrir(estado.IdJogo, estado.IdJogoLink, contexto.Prefixo);
```

É a única mudança fora da infraestrutura. O `PdfTextExtractor` passa a pedir clientes à fábrica
em vez de montar o seu, declarando `OperacaoLlm.Ocr`. Daí em diante ninguém mais precisa saber
de nada: a policy cronometra, lê, e o registrador grava e loga.

### Regra do dinheiro

**Dinheiro gasto é sempre gravado; o custo é enfeite opcional.** Disso decorre:

- Se o `usage.cost` não vier, a linha vai com `CUSTO_USD` nulo e o resto preenchido.
- Se a chamada terminou e o `CancellationToken` do chamador é cancelado em seguida, a linha é
  gravada de qualquer forma: o registrador escreve com `CancellationToken.None`. Um desligamento
  não apaga um gasto que já aconteceu.
- Falha do registrador (banco fora, por exemplo) nunca derruba a chamada de LLM: captura,
  loga Warning e devolve.
- A gravação é direta, não enfileirada: um `INSERT` de poucos milissegundos ao lado de uma
  chamada de LLM de dezenas de segundos não justifica fila, e fila abriria janela de perda no
  desligamento. Se um dia um chat de regras puser a policy no caminho de uma requisição de
  usuário, aí vale reavaliar.

### Desfechos, e o que cada um torna visível

- `Truncado` — o `finish_reason: length` que fez o OCR transcrever meio manual e ser descartado.
  Capturado com preço na sonda: `custo=0,0000068600 finish=length`.
- `ErroDoModelo` — inclui o `finish_reason: "error"` da OpenRouter que matou um bloco do revisor
  em dev. Aqui está o ganho sobre qualquer envelope em nível de abstração: esse caso chega como
  **HTTP 200** e o SDK só lança depois, ao desserializar um enum que não conhece. A policy vê a
  resposta antes disso e registra o gasto **com** tokens e custo.
- `ErroHttp` — 429 e 5xx, que o pipeline entrega como resposta, não como exceção. Guarda o
  status e um trecho do corpo do erro.
- `Excecao` — timeout, DNS, falha de socket: não houve resposta, então a linha vai sem números.
  A policy re-lança; o comportamento atual do pipeline não muda em nada.

## Pontos cegos assumidos

1. **Streaming** não é registrado. Ninguém usa hoje, e a policy loga Debug quando encontra.
2. **Chamada interrompida por desligamento** pode ter sido cobrada sem deixar linha, porque não
   houve resposta para ler.
3. **Cliente criado fora da fábrica** não é registrado. É por isso que o `PdfTextExtractor`
   deixa de montar o seu: com um só lugar construindo clientes, escapar exige intenção.
4. **Custo em provedor não-OpenRouter** vira nulo, por não estar na especificação da OpenAI.

## Testes

Nenhum teste do projeto toca `DatabaseContext`; repositório aqui é validado em dev com SQL,
como foi feito na indexação. Os testes cobrem a lógica:

- **`PoliticaUsoLlmTests`** — o teste central, e ele exercita o caminho real: um
  `PipelineTransport` falso devolve um JSON canned, o cliente é montado pela fábrica de
  verdade, e a chamada sai por `IChatClient.GetResponseAsync`. Assim cada caso prova as duas
  coisas de uma vez, a linha registrada e o SDK tendo conseguido interpretar a mesma resposta:
  - resposta completa grava tokens, modelos, provider, custo, id, duração e `Ok`;
  - `finish_reason: length` grava `Truncado` **com** custo;
  - `finish_reason: error` grava `ErroDoModelo` com custo, e a exceção que o SDK lança depois
    não apaga a linha;
  - resposta sem `usage` grava a linha com custo nulo;
  - corpo não-JSON grava linha com status e duração, sem números;
  - status 429 grava `ErroHttp` com trecho do corpo;
  - transporte que lança grava `Excecao` e a exceção chega ao chamador;
  - registrador que lança não impede a chamada de retornar;
  - com `tentativas: 1` e o transporte falhando só na primeira, saem **duas** linhas.
- **`EscopoUsoLlmTests`** — aninhar e descartar restaura o anterior; sem escopo, `Atual` é nulo;
  o escopo atravessa `await`; o alvo do escopo chega no registro.
- **`FabricaOpenRouterTests`** — sem chave lança com a mensagem de hoje; a mesma combinação de
  `(modelo, operacao, timeout, tentativas)` reaproveita o cliente; combinações diferentes não.

Verificação em dev, depois dos testes: indexar um manual e conferir as ~10 linhas em `USO_LLM`
— operações corretas, custo preenchido nas três etapas, `TRACE_ID` batendo com o log daquele
manual, e a soma conferindo com o painel da OpenRouter.

## Consultas que o ledger passa a responder

```sql
-- Quanto custou indexar um manual, etapa por etapa
SELECT OPERACAO, MODELO_RESPONDEU, PROVIDER, COUNT(*) AS chamadas,
       SUM(TOKENS_ENTRADA) AS entrada, SUM(TOKENS_SAIDA) AS saida,
       SUM(CUSTO_USD) AS custo, SUM(CUSTO_USD IS NULL) AS sem_custo
FROM USO_LLM
WHERE ID_JOGO_LINK = 14
GROUP BY OPERACAO, MODELO_RESPONDEU, PROVIDER;

-- Gasto por mês, e quanto dele foi em chamada que não serviu
SELECT DATE_FORMAT(MOMENTO, '%Y-%m') AS mes, OPERACAO, MODELO_RESPONDEU,
       COUNT(*) AS chamadas, SUM(CUSTO_USD) AS custo,
       SUM(DESFECHO <> 0) AS nao_serviram,
       SUM(CASE WHEN DESFECHO <> 0 THEN CUSTO_USD ELSE 0 END) AS custo_desperdicado
FROM USO_LLM
GROUP BY mes, OPERACAO, MODELO_RESPONDEU
ORDER BY mes DESC, custo DESC;

-- Os manuais mais caros do acervo
SELECT ID_JOGO_LINK, ALVO, SUM(CUSTO_USD) AS custo, COUNT(*) AS chamadas
FROM USO_LLM
GROUP BY ID_JOGO_LINK, ALVO
ORDER BY custo DESC
LIMIT 20;
```

## Riscos

- **O formato do `usage` é da OpenRouter.** Se `cost` mudar de nome, o custo vira nulo e o resto
  da linha continua. O ledger degrada, não quebra — e a verificação em dev compara a soma com o
  painel deles, então uma divergência aparece.
- **Trocar de provedor compatível derruba o custo para nulo**, porque preço por chamada não
  existe na especificação da OpenAI. Tokens, duração, id e `finish_reason` sobrevivem. Quem
  fizer essa troca precisa decidir entre viver sem custo ou acrescentar uma tabela de preços
  local — e, nesse dia, uma coluna dizendo que o número é estimado.
- **Uma linha por chamada não escala para sempre.** Com o acervo atual são ~10 linhas por manual
  indexado, algumas centenas no deploy inicial: irrelevante. Um chat de regras no ar mudaria a
  ordem de grandeza para "por pergunta de usuário", e aí a tabela precisa de política de
  retenção. Registrado, não resolvido agora.
- **`decimal(18,10)` em MySQL** guarda `0.000000343` sem perda, mas somas em `decimal` são mais
  lentas que em `double`. Precisão vale mais que velocidade num ledger.
