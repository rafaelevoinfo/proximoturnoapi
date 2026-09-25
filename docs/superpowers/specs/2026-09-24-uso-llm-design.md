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
uma tela fizer sentido, é tarefa própria. Também fica fora a reconciliação com a fatura da
OpenRouter (ver *Pontos cegos*).

## Decisões e a evidência de cada uma

**O custo vem da OpenRouter, não de uma tabela de preços nossa.** A OpenRouter devolve o custo
real cobrado em `usage.cost` — e esta conta o manda sem precisar pedir `usage.include`. Uma
tabela local erraria em dois eixos: quando os preços mudam e quando a rota vai para um provider
de preço diferente (sondado: `deepseek/deepseek-v4-flash` atendido pela `StreamLake`).

**Mas o SDK descarta esse campo.** Sondado: `UsageDetails` carrega apenas contagens de tokens,
e `AdditionalCounts` só traz campos da própria OpenAI. O `cost` não chega de nenhuma forma.

**Então o custo é buscado pelo id da chamada**, em `GET /api/v1/generation?id={id}`, que devolve
`total_cost`, `provider_name` e os tokens nativos. O `ChatResponse.ResponseId` traz o
`gen-...` necessário. A alternativa — ler o JSON bruto da resposta numa policy do pipeline
HTTP — foi avaliada e recusada pelo dono do repositório, que preferiu não acoplar o código ao
formato do `usage`.

**O embedding é a exceção, por falta de id.** Sondado: no caminho de embedding o SDK não
expõe id nenhum (`AdditionalProperties` vem nulo na coleção e no item), porque a resposta
oficial de embeddings da OpenAI não tem esse campo — a OpenRouter adiciona e o SDK descarta.
Sem id não há o que consultar. Como o `text-embedding-3-small` é modelo fixo, sem roteamento
de provider, o custo do embedding é calculado como
`TokensEntrada / 1_000_000m * IAModel.PRECO_EMBEDDING_USD_POR_MILHAO` (constante nova, `0.02m`,
ao lado do nome do modelo que ela precifica) e a linha fica marcada com `CUSTO_ESTIMADO`. Magnitude do que se abre mão: ~$0,0002 por manual, contra os
~$0,05 do OCR.

**A operação vem da identidade do cliente, não de um escopo.** Quem pede o cliente à fábrica
declara para que ele serve (`CriarChat(modelo, OperacaoLlm.Ocr, ...)`). Assim o envelope sabe
qual etapa está registrando sem que ninguém precise abrir escopo para isso, e sobra para o
escopo ambiente apenas o alvo — qual manual de qual jogo. Uma linha no `SincronizarManual`
em vez de três.

## Peças

Diretório novo `Src/Infrastructure/IA/`, com os contratos em `Src/Application/UseCases/IA/`,
no mesmo padrão que RAG já usa. O diretório não é "RAG" de propósito: um chat de regras no
futuro usa as mesmas peças.

| Peça | Arquivo | Responsabilidade |
|---|---|---|
| `IFabricaOpenRouter` | `Application/UseCases/IA/IFabricaOpenRouter.cs` | contrato da fábrica |
| `FabricaOpenRouter` | `Infrastructure/IA/FabricaOpenRouter.cs` | único lugar que lê a chave e monta `OpenAIClient`; devolve clientes já envelopados |
| `ChatComRegistro` | `Infrastructure/IA/ChatComRegistro.cs` | `DelegatingChatClient`: mede, colhe, pede custo, registra |
| `EmbeddingComRegistro` | `Infrastructure/IA/EmbeddingComRegistro.cs` | idem para embedding, com custo calculado |
| `IConsultaCustoLlm` / `ConsultaCustoOpenRouter` | `Application/UseCases/IA/`, `Infrastructure/IA/` | `GET /generation`, com tentativas curtas; nunca lança |
| `EscopoUsoLlm` | `Application/UseCases/IA/EscopoUsoLlm.cs` | `AsyncLocal` com o alvo (jogo, link, rótulo) |
| `IRegistradorUsoLlm` / `RegistradorUsoLlm` | `Application/UseCases/IA/`, `Infrastructure/IA/` | grava a linha e emite uma linha de log; nunca lança |
| `IUsoLlmRepository` / `UsoLlmRepository` | `Infrastructure/Repositories/UsoLlmRepository.cs` | persistência, no padrão `IBaseRepository` |
| `UsoLlm`, `OperacaoLlm`, `DesfechoLlm` | `Infrastructure/Models/UsoLlm.cs` | entidade e enums, como `JogoLinkIndexacao` faz |

### Contratos

```csharp
public enum OperacaoLlm : short {
    Ocr = 0,
    RevisaoMarkdown = 1,
    Embedding = 2
}

public enum DesfechoLlm : short {
    /// <summary>Resposta completa. FinishReason Stop ou ausente.</summary>
    Ok = 0,
    /// <summary>FinishReason Length: pago e descartado pelo chamador.</summary>
    Truncado = 1,
    /// <summary>Qualquer outro FinishReason, como filtro de conteúdo.</summary>
    ErroDoModelo = 2,
    /// <summary>A chamada lançou. Sem tokens e sem custo: não houve resposta.</summary>
    Excecao = 3
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
    bool CustoEstimado,
    int DuracaoMs,
    DesfechoLlm Desfecho,
    string? Detalhe,
    string? IdGeracao);

public interface IRegistradorUsoLlm {
    /// <summary>Nunca lança. O alvo e o trace id vêm do ambiente.</summary>
    Task RegistrarAsync(RegistroUsoLlm registro);
}

public interface IConsultaCustoLlm {
    /// <summary>Nunca lança. Null quando o custo não pôde ser obtido.</summary>
    Task<CustoLlm?> ObterAsync(string idGeracao, CancellationToken cancellationToken);
}

public sealed record CustoLlm(decimal Custo, string? Provider, string? Modelo);

public sealed record AlvoUsoLlm(int? IdJogo, int? IdJogoLink, string? Alvo);

public static class EscopoUsoLlm {
    public static AlvoUsoLlm? Atual { get; }
    /// <summary>Descartar restaura o escopo anterior.</summary>
    public static IDisposable Abrir(int? idJogo, int? idJogoLink, string? alvo);
}
```

`RegistrarAsync` não recebe `CancellationToken` de propósito: ver *Regra do dinheiro*.
`ModeloPedido` é o modelo que a fábrica recebeu ao criar o cliente, não um dado da resposta:
é ele que denuncia a diferença quando o provider responde com outra versão.

### De onde saem os tokens de raciocínio e de cache

`UsageDetails.InputTokenCount` e `OutputTokenCount` bastam para entrada e saída. Raciocínio e
cache **não** chegam por `AdditionalCounts`: a sonda mostrou lá apenas os campos de áudio e de
predição, com o raciocínio ausente mesmo quando a chamada o usou. Eles são lidos do objeto
tipado do SDK:

```csharp
var bruto = resposta.RawRepresentation as OpenAI.Chat.ChatCompletion;
var raciocinio = bruto?.Usage?.OutputTokenDetails?.ReasoningTokenCount ?? 0;
var cache      = bruto?.Usage?.InputTokenDetails?.CachedTokenCount ?? 0;
```

Sem cast possível, os dois vão a zero e o resto da linha segue: são detalhe de diagnóstico,
não dinheiro.

### A fábrica

Lê `OPENROUTER_API_KEY` uma vez e lança `InvalidOperationException("OPENROUTER_API_KEY não
configurada.")` se faltar, preservando a mensagem atual. Guarda os `OpenAIClient` num cache
por `(timeout, tentativas)`: o `OpenAIClient` é thread-safe e segura o pool de conexões, e
criar um por chamada desperdiçaria socket.

Preserva as configurações de hoje, que não são arbitrárias:

| Cliente | Timeout | Tentativas | Motivo já documentado no código |
|---|---|---|---|
| OCR | 10 min | 1 | transcrever um manual inteiro leva minutos; a cascata já é a nossa retentativa |
| Revisor | 5 min | 2 | cada bloco leva dezenas de segundos porque o modelo raciocina |
| Embedding | 2 min | 2 | chamada curta e idempotente |

### Registro no `Program.cs`

A fábrica passa a ser a origem dos clientes, e os três registros de hoje encurtam:

```csharp
builder.Services.AddSingleton<IFabricaOpenRouter, FabricaOpenRouter>();
builder.Services.AddSingleton<IRegistradorUsoLlm, RegistradorUsoLlm>();
builder.Services.AddHttpClient<IConsultaCustoLlm, ConsultaCustoOpenRouter>();
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

Uma linha por chamada. Um manual típico gera ~10 (1 de OCR, 8 de revisão, 1 lote de embedding).

| Coluna | Tipo | Nulo | Por que existe |
|---|---|---|---|
`ID` | int | não | chave, via `BaseModel`
`MOMENTO` | datetime(6) | não | quando a chamada terminou
`TRACE_ID` | varchar(32) | sim | o `RastreioBackground` dá um trace id por item da fila e o Serilog o imprime em toda linha; guardado aqui, uma linha cara leva direto ao trecho do log
`OPERACAO` | smallint | não | `OperacaoLlm`
`ID_JOGO` | int | sim | **sem foreign key de propósito**
`ID_JOGO_LINK` | int | sim | **sem foreign key de propósito**: apagar um link não pode apagar o registro do que ele custou
`ALVO` | varchar(200) | sim | `"Balde De Caranguejo / FAQ"`, para ler sem join
`MODELO_PEDIDO` | varchar(100) | não | o que pedimos
`MODELO_RESPONDEU` | varchar(100) | sim | o que respondeu, que pode vir com a versão exata
`PROVIDER` | varchar(60) | sim | quem atendeu; explica preço diferente para o mesmo modelo
`TOKENS_ENTRADA` | int | não | —
`TOKENS_SAIDA` | int | não | —
`TOKENS_RACIOCINIO` | int | não | explica a conta do revisor novo, que raciocina antes de responder
`TOKENS_CACHE` | int | não | entrada que foi servida de cache
`CUSTO_USD` | decimal(18,10) | sim | uma chamada custa `0.000000343`. Nulo é "não sei", zero é "não custou"
`CUSTO_ESTIMADO` | bit | não | 1 quando veio de preço constante em vez do `/generation`; sem isso o ledger mistura número oficial com conta nossa
`DURACAO_MS` | int | não | os ~18s por bloco do revisor, medidos
`DESFECHO` | smallint | não | `DesfechoLlm`
`DETALHE` | varchar(300) | sim | `finish_reason` cru ou tipo e mensagem da exceção
`ID_GERACAO` | varchar(80) | sim | o `gen-...`: audita na OpenRouter e permite recompor o custo depois

Índices: `MOMENTO` e `ID_JOGO_LINK`.

## Fluxo

`SincronizarManual.ProcessarAsync` abre um escopo de alvo logo depois de montar o `contexto`,
envolvendo extração, revisão e embedding:

```csharp
var contexto = new ContextoManual(estado.NomeJogo, estado.TituloLink);
using var _ = EscopoUsoLlm.Abrir(estado.IdJogo, estado.IdJogoLink, contexto.Prefixo);
```

É a única mudança fora da infraestrutura. Daí em diante:

1. `PdfTextExtractor` pede à fábrica um cliente para `OperacaoLlm.Ocr` por modelo da cascata;
   `Program.cs` pede um para `RevisaoMarkdown` e o gerador de embedding.
2. O envelope cronometra, chama o interno e colhe `Usage`, `ModelId`, `ResponseId` e
   `FinishReason`.
3. Com o `ResponseId`, `ConsultaCustoOpenRouter` busca o custo. Ela recebe um `HttpClient`
   nomeado, registrado via `AddHttpClient` com `BaseAddress` em `https://openrouter.ai/api/v1/`
   e o `Authorization: Bearer` da mesma `OPENROUTER_API_KEY`, e lê `data.total_cost`,
   `data.provider_name` e `data.model`: tentativas em 200ms, 600ms e
   1200ms, porque o registro leva um instante para existir do lado da OpenRouter. Teto de ~2s
   por chamada — num manual de 9 chamadas de chat, uns 5s a mais num processo que leva 2 a 4
   minutos, dentro de um worker onde ninguém espera.
4. O envelope entrega a linha ao registrador, que grava e loga.

### Regra do dinheiro

**Dinheiro gasto é sempre gravado; o custo é enfeite opcional.** Disso decorre:

- Se a consulta de custo falha, a linha vai com `CUSTO_USD` nulo e `ID_GERACAO` preenchido.
  Recuperável depois, porque o id é a chave.
- Se a chamada terminou e o `CancellationToken` do chamador é cancelado em seguida, a linha é
  gravada de qualquer forma: o registrador escreve com `CancellationToken.None`. Um desligamento
  não apaga um gasto que já aconteceu.
- A consulta de custo, por ser opcional, **é** abortada no cancelamento: grava sem custo.
- Falha do registrador (banco fora, por exemplo) nunca derruba a chamada de LLM: ele captura,
  loga Warning e devolve.

### Desfechos

- `Truncado` — o `finish_reason: length` que fez o OCR transcrever meio manual e ser descartado.
- `ErroDoModelo` — qualquer outro `FinishReason`.
- `Excecao` — timeout, rede, 429, e também o `Unknown ChatFinishReason value ... error` que o
  SDK lança ao receber `finish_reason: error` da OpenRouter, observado em dev matando um bloco
  do revisor. Grava tipo e mensagem curta, sem tokens nem custo, e **re-lança**: o
  comportamento atual do pipeline não muda em nada.
- `OperationCanceledException` vinda de dentro da chamada re-lança sem gravar linha.

## Pontos cegos assumidos

1. **Retentativa do SDK.** O `ClientRetryPolicy` repete dentro de uma única chamada e o
   envelope vê apenas o `ResponseId` da última. Se uma tentativa anterior completou no servidor
   e foi cobrada, esse gasto não entra no ledger — é o que uma policy no pipeline HTTP pegaria,
   e é o preço da via escolhida. Reconciliação futura possível com `GET /api/v1/activity`, que
   dá o agregado diário por modelo e denunciaria a diferença. Não faz parte deste trabalho.
2. **Streaming.** Ninguém usa hoje. `GetStreamingResponseAsync` repassa sem registrar e loga um
   Debug dizendo isso, para não abrir buraco silencioso se alguém usar depois.
3. **Custo do embedding** é calculado, não cobrado; marcado com `CUSTO_ESTIMADO`.
4. **Chamada interrompida por desligamento** pode ter sido cobrada e não gera linha.
5. **Cliente criado fora da fábrica** não é registrado. É por isso que o `PdfTextExtractor`
   deixa de montar o seu: com um só lugar construindo clientes, escapar exige intenção.

## Testes

Nenhum teste do projeto toca `DatabaseContext`; repositório aqui é validado em dev com SQL,
como foi feito na indexação. Os testes cobrem a lógica:

- **`EscopoUsoLlmTests`** — aninhar e descartar restaura o anterior; sem escopo, `Atual` é nulo;
  o escopo atravessa `await`.
- **`ChatComRegistroTests`** (fakes de `IChatClient`, registrador e consulta de custo) —
  sucesso grava tokens, modelos, provider, duração e `Ok`; `Length` grava `Truncado`; exceção
  grava `Excecao` e re-lança; `OperationCanceledException` re-lança sem gravar; falha do
  registrador não quebra a chamada; falha da consulta deixa custo nulo com `ID_GERACAO`
  preenchido; o alvo do escopo chega no registro.
- **`EmbeddingComRegistroTests`** — custo igual a tokens × preço, `CustoEstimado` verdadeiro,
  operação `Embedding`.
- **`ConsultaCustoOpenRouterTests`** (fake de `HttpMessageHandler`) — JSON normal devolve custo
  e provider; 404 nas duas primeiras e sucesso na terceira; 404 sempre devolve nulo; JSON sem
  `total_cost` devolve nulo; corpo malformado devolve nulo; nunca lança.
- **`FabricaOpenRouterTests`** — sem chave lança com a mensagem de hoje; o mesmo par
  `(timeout, tentativas)` reaproveita o cliente.

Verificação em dev, depois dos testes: indexar um manual e conferir as ~10 linhas em
`USO_LLM` — operações corretas, custo preenchido nas de chat, `CUSTO_ESTIMADO` na de embedding,
`TRACE_ID` batendo com o log daquele manual, e a soma conferindo com o painel da OpenRouter.

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

- **Uma linha por chamada não escala para sempre.** Com o acervo atual são ~10 linhas por
  manual indexado, algumas centenas por deploy inicial: irrelevante. Se um dia um chat de
  regras entrar no ar, o volume passa a ser por pergunta de usuário, e aí a tabela precisa de
  política de retenção. Registrado, não resolvido agora.
- **O formato do `/generation` é da OpenRouter.** Se `total_cost` mudar de nome, o custo vira
  nulo e o resto da linha continua. O ledger degrada, não quebra.
- **`decimal(18,10)` em MySQL** guarda `0.000000343` sem perda, mas somas de milhões de linhas
  em `decimal` são mais lentas que em `double`. Precisão vale mais que velocidade num ledger.
