# Indexação de manuais: ciclo de vida, cache por hash e revisor

- **Data:** 2026-09-15
- **Branch:** `rag` (ainda não está na `main`)
- **Escopo:** `IndexacaoManuaisWorker` e tudo que ele orquestra (extração, revisão, chunking, embedding, Qdrant)

## Objetivo

Deixar o pipeline de indexação de manuais correto ao longo do tempo (manual trocado, link removido, jogo desativado, PDF repetido, falhas) e melhorar a qualidade do texto indexado com uma etapa de revisão barata.

Problemas que este design corrige:

1. Trocar o PDF de um link não reindexa: `JogoDTO.UpdateModel` troca a `Url` e mantém `Indexado = true`.
2. Vetores órfãos: link removido, link que virou vídeo ou jogo desativado deixam os pontos no Qdrant.
3. PDF repetido: o mesmo arquivo em dois links é extraído (e pago) duas vezes e duplica trechos na busca do mesmo jogo.
4. Falha sem estado: `Indexado` é bool; uma extração que falha sem gerar `.md` roda a cascata inteira (até o Opus) a cada restart, e todo push na `main` é um restart.

Também entram: OCR começando no Gemini, nome do jogo e do manual no texto do embedding, remoção de `MarkdownExtractor` e `PortaGrpc`, e a nova etapa de revisão com `deepseek/deepseek-v4-flash`.

## Decisões

| Tema | Decisão |
|---|---|
| Arquitetura | O job carrega só ids; um use case (`SincronizarManual`) lê o estado atual no banco e decide o que fazer. Produtores só avisam que um link pode ter mudado. |
| PDF repetido | Extração e revisão reaproveitadas pelo hash. No mesmo jogo, só um link com aquele hash grava vetores; os outros ficam `Duplicado`. Jogos diferentes indexam normalmente. |
| Estado | Nova tabela `JOGO_LINK_INDEXACAO`; a coluna `JOGO_LINK.INDEXADO` é removida. |
| Migração | Migração nova (remove a coluna e cria a tabela). Em produção roda logo após a que cria a coluna; é inofensivo e evita ajuste manual no banco de dev. |
| Tentativas | Teto de 3 falhas consecutivas por URL. Um `Falhou` esgotado só volta se a URL mudar. |
| OCR | `OCR_MODELS = ["google/gemini-3.6-flash", "anthropic/claude-opus-5"]`. O Qwen sai. |
| Revisor | `deepseek/deepseek-v4-flash`, só texto, por blocos, devolvendo lista de correções validada por travas no código. Não bloqueia a indexação. |
| Arquivos | Cache por hash na raiz de `uploads/`, que é a pasta coberta pelo backup (`SincronizadorUploads` só lê a raiz). |

## Modelo de dados

### Tabela `JOGO_LINK_INDEXACAO` (modelo `JogoLinkIndexacao : BaseModel`)

| Coluna | Tipo | Regra |
|---|---|---|
| `ID` | int | PK (herdado de `BaseModel`) |
| `ID_JOGO_LINK` | int | FK para `JOGO_LINK`, `ON DELETE CASCADE`, índice único |
| `URL` | varchar(300) | URL processada na última sincronização; é o que detecta troca de PDF |
| `HASH_PDF` | varchar(64) | SHA-256 do PDF em hex minúsculo; nulo até o hash ser calculado; com índice |
| `STATUS` | smallint | `StatusIndexacao` |
| `TENTATIVAS` | int | falhas consecutivas na URL atual; zera no sucesso e quando a URL muda |
| `ULTIMO_ERRO` | varchar(1000) | nulo; mensagem da última falha, truncada |
| `MODELO_EXTRACAO` | varchar(100) | nulo; modelo do OCR que venceu |
| `CONFIABILIDADE_EXTRACAO` | smallint | nulo; nota informada pelo modelo |
| `MODELO_REVISAO` | varchar(100) | nulo; nulo também quando a revisão falhou inteira |
| `CORRECOES_APLICADAS` | int | nulo |
| `CORRECOES_DESCARTADAS` | int | nulo |
| `REVISAO_COMPLETA` | bit | nulo; `false` quando algum bloco não chegou a ser revisado |
| `QUANTIDADE_CHUNKS` | int | nulo |
| `DATA_ATUALIZACAO` | datetime | toda gravação |
| `DATA_INDEXACAO` | datetime | nulo; quando chegou a `Indexado` |

### `StatusIndexacao : short`

| Valor | Nome | Significado |
|---|---|---|
| 0 | `Processando` | sincronização em andamento. Encontrado no start, significa que a aplicação caiu no meio: conta como tentativa e o link é refeito. |
| 1 | `Indexado` | vetores gravados para a `URL` atual. |
| 2 | `Falhou` | a última tentativa falhou; `ULTIMO_ERRO` diz por quê. |
| 3 | `Removido` | o link existe mas não deve ter vetores (virou vídeo, ficou sem URL ou o jogo foi desativado). |
| 4 | `Duplicado` | outro link do mesmo jogo já está `Indexado` com o mesmo hash; este não grava vetores. |

### Elegibilidade no start

Um link é elegível quando é de regra, tem URL, o jogo está ativo (existe cópia não desativada) e **não** está em nenhum destes casos:

- `Indexado` com `URL` igual à do link;
- `Falhou` com `TENTATIVAS >= 3` e `URL` igual à do link.

Sem linha na tabela, é elegível. Esta consulta substitui `IJogoRepository.GetJogosNaoIndexadosAsync` e absorve o filtro de jogo desativado que hoje está não commitado nessa consulta.

### `JogoLink`

Perde a propriedade `Indexado`. O front não usa o campo.

## Arquivos (cache por hash)

Pasta: `UploadManual.GetUploadFolder(env)`.

| Arquivo | Conteúdo |
|---|---|
| `{hash}.raw.md` | saída do OCR |
| `{hash}.md` | saída do revisor; é o que vai para o chunking quando existe |

- **Legado:** se `{hash}.raw.md` não existe e existe `Path.ChangeExtension(pdf, ".md")` (formato antigo, por GUID), o arquivo antigo é movido para `{hash}.raw.md`. Isso evita pagar de novo os manuais já extraídos.
- **Metadados em cache:** quando a extração ou a revisão vêm do cache, `MODELO_*`, `CONFIABILIDADE_EXTRACAO`, `CORRECOES_*` e `REVISAO_COMPLETA` são copiados de outra linha com o mesmo `HASH_PDF` que tenha esses campos preenchidos. Se não houver, ficam nulos.

## Fila e job

- `ManualJob(int IdJogoLink, int IdJogo)`. A `Url` sai do job: o use case lê a atual no banco.
- `IdJogo` só é usado quando o link já não existe (para achar os `Duplicado` do jogo). A reconciliação enfileira com `IdJogo = 0`, que significa "não reenfileirar irmãos".
- `IManualQueue` e `ManualQueue` não mudam.

## `SincronizarManual`

Use case em `Application/UseCases/RAG`, herda `UseCaseBasico`, registrado como `Scoped`, com a constante pública `MaxTentativas = 3`. O worker vira só o laço: desenfileira, abre um escopo, chama `SincronizarManual.ExecuteAsync(job, ct)`, e mantém uma Activity (`RastreioBackground`) por item.

### Decisão

Lê de uma vez o **estado do link**: o link (tipo, URL, título), o nome do jogo, se o jogo está ativo e a linha de `JOGO_LINK_INDEXACAO`.

1. **Link não existe:** `RemoverAsync(IdJogoLink)` no Qdrant; se `job.IdJogo != 0`, reenfileira os `Duplicado` do jogo. Fim.
2. **Link não é de regra, sem URL, ou jogo desativado:** `RemoverAsync(IdJogoLink)`; se há linha, marca `Removido`; se a linha estava `Indexado`, reenfileira os `Duplicado` do jogo. Fim.
3. **`Indexado` com a mesma URL:** nada. Fim.
4. **`Falhou` com `MaxTentativas` na mesma URL:** `RemoverAsync(IdJogoLink)`, para garantir que não sobra vetor, e não tenta de novo. Fim.
5. **Caso contrário, pipeline:**
   1. Grava `Processando` com a URL atual (se a URL mudou, `TENTATIVAS = 0` antes) e chama `RemoverAsync(IdJogoLink)`. O link fica sem vetores até o passo 7: assim um manual trocado nunca responde com a versão antiga, e vetores gravados antes desta mudança são limpos.
   2. Calcula o SHA-256 do PDF (`Path.Combine(uploads, Path.GetFileName(url))`). Arquivo inexistente é falha ("arquivo não encontrado"), sem custo de LLM.
   3. **Duplicado:** se outro link do mesmo jogo está `Indexado` com esse hash, marca `Duplicado` (os vetores já saíram no passo 1). Fim.
   4. **Extração:** se `{hash}.raw.md` não existe (após tentar o legado), chama `ITextExtractor` e grava o arquivo.
   5. **Revisão:** se `{hash}.md` não existe, chama `IRevisorMarkdown` sobre o raw e grava `{hash}.md`, desde que algum bloco tenha sido revisado. Na falha total não grava nada, para a revisão ser refeita depois.
   6. **Chunking** do `{hash}.md`, ou do `{hash}.raw.md` quando a revisão falhou inteira, com `ContextoManual(NomeJogo, TituloLink)`.
   7. **Embedding** e **Qdrant** (`SalvarAsync`: delete por `IdJogoLink` + upsert, como hoje).
   8. Marca `Indexado`, `TENTATIVAS = 0`, `DATA_INDEXACAO = agora`, grava hash, metadados e `QUANTIDADE_CHUNKS`.

   Se a linha estava `Indexado` antes desta sincronização (só acontece com URL trocada), reenfileira os `Duplicado` do jogo ao final, qualquer que seja o resultado.

### Erros

- Exceção nos passos 5.2 a 5.7: marca `Falhou`, `TENTATIVAS++`, grava `ULTIMO_ERRO` e loga em Error. Não relança; o worker segue para o próximo item.
- `OperationCanceledException`: relança sem gravar nada. A linha fica `Processando` e o link é refeito no próximo start.
- Falha ao gravar a própria linha de status: loga em Error e relança para o laço do worker, que já trata e segue.

## Extração (`PdfTextExtractor`)

- `ITextExtractor.ExtractTextAsync(string pdfFilePath, CancellationToken)` passa a devolver `ResultadoExtracao(string Texto, string Modelo, int Confiabilidade)` e **não grava arquivo**. Quem grava `{hash}.raw.md` é o use case.
- A cascata, as notas, o parse da sentinela e o tratamento de truncamento continuam como estão.
- `IAModel.OCR_MODELS = ["google/gemini-3.6-flash", "anthropic/claude-opus-5"]`, com o comentário de preços atualizado.
- Continua valendo que um resultado abaixo de `ConfiabilidadeAceitavel` é usado assim mesmo (só registra a nota). Barrar esses casos fica fora deste escopo.

## Revisor

### Componentes

- `IRevisorMarkdown` (`Application/UseCases/RAG`): `Task<ResultadoRevisao> RevisarAsync(string markdown, ContextoManual contexto, CancellationToken ct)`.
- `ResultadoRevisao(string Texto, string? Modelo, int Aplicadas, int Descartadas, bool Completa)`.
- `RevisaoMarkdown` (`Application/UseCases/RAG`, estático e puro): `DividirEmBlocos` e `ValidarEAplicar`.
- `LlmMarkdownRevisor` (`Infrastructure/RAG`): recebe `ILogger` e um `IChatClient` com chave `"revisor"`.
- `Program.cs`: `AddKeyedSingleton<IChatClient>("revisor", ...)` via OpenRouter com `IAModel.REVISOR_MODEL = "deepseek/deepseek-v4-flash"`, `NetworkTimeout` de 5 minutos e `ClientRetryPolicy(maxRetries: 2)`. `IRevisorMarkdown` é registrado como `Scoped`.

### Blocos

- O markdown é cortado em blocos de até `TamanhoBloco = 4000` caracteres, sempre em linha em branco. Um parágrafo maior que o limite vai sozinho, sem ser partido.
- Cada bloco é uma substring exata do original, separadores incluídos: a concatenação dos blocos reproduz o texto original byte a byte.
- As chamadas são sequenciais, uma por bloco, com temperatura 0.

### Prompt (instruções)

```
Você revisa trechos de manuais de jogos de tabuleiro transcritos de PDF por OCR.
Aponte apenas erros de leitura ou digitação: letras trocadas, faltando ou sobrando que
formam uma palavra errada ou fora de contexto (ex.: "mudos de jogo" -> "modos de jogo").
Não altere números, nomes próprios, nomes de cartas, peças, modos ou termos inventados
pelo jogo, regionalismos, gírias, pontuação nem formatação markdown.
Não reescreva frases nem melhore o estilo. Na dúvida, não corrija.
Jogo: {NomeJogo}. Manual: {TituloManual}.
Responda somente com JSON no formato
{"correcoes":[{"original":"trecho exato como está no texto","corrigido":"trecho corrigido","motivo":"curto"}]}.
Se não houver erros, responda {"correcoes":[]}.
```

A mensagem do usuário é o bloco. A resposta é pedida em modo JSON (`ChatResponseFormat.Json`, que vira `response_format: {"type":"json_object"}`), e não por JSON schema: o formato é simples, o prompt já o descreve e o parse precisa ser tolerante de qualquer jeito. Se o provedor devolver o JSON com texto em volta, extrai-se o primeiro objeto `{...}`. Se nem isso desserializar, o bloco conta como falho.

### Travas (em `RevisaoMarkdown.ValidarEAplicar`)

"Palavras" são as sequências separadas por espaço em branco, sem a pontuação das pontas; "letras" conta `char.IsLetter`. "Palavras alteradas" são as palavras de `original` que não aparecem em `corrigido`. Uma correção é descartada se:

1. `original` não aparece literalmente (ordinal) no bloco;
2. a sequência de dígitos de `original` difere da de `corrigido`;
3. a distância de Levenshtein entre `original` e `corrigido` (diferencia maiúsculas) não está entre 1 e 2, `original` tem mais de 3 palavras, ou alguma palavra alterada tem menos de 4 letras;
4. alguma palavra alterada coincide (sem diferenciar maiúsculas) com uma palavra de 3 ou mais letras do nome do jogo ou do título do manual;
5. alguma palavra alterada aparece 3 ou mais vezes no documento inteiro (palavra inteira, sem diferenciar maiúsculas).

Correção aceita substitui todas as ocorrências de `original` dentro do bloco em que foi reportada. Aceitas são logadas em Information (`original -> corrigido (motivo)`); descartadas, em Debug com a trava que as barrou.

### Falhas

- Bloco com erro de API ou JSON inválido: segue sem correções e `Completa = false`.
- Nenhum bloco revisado com sucesso: devolve o texto original com `Modelo = null`.
- O revisor nunca lança exceção, exceto `OperationCanceledException`.
- O use case grava `{hash}.md` sempre que `Modelo != null`, mesmo com `Completa = false`. O texto parcialmente revisado é melhor que o raw e vira cache para os outros links do mesmo PDF; a linha registra `REVISAO_COMPLETA = false` e refazer é apagar o `{hash}.md`.
- Com `Modelo == null` nada é gravado: o chunking usa o `{hash}.raw.md` e a revisão é refeita na próxima sincronização do link.

## Chunking e embedding

- `ContextoManual(string NomeJogo, string TituloManual)` com `Prefixo` = partes não vazias unidas por `" > "`.
- `IChunkingExtractor.ExtrairChunksAsync(string markdownFilePath, ContextoManual contexto, CancellationToken)` continua lendo o arquivo; só ganha o contexto. `ChunkingExtractor.Dividir(string markdown, ContextoManual? contexto = null)` continua estático e puro.
- O `Titulo` de cada chunk passa a ser `Prefixo > caminho de títulos`, ou só o `Prefixo` quando a seção não tem título. Isso vale para o `TextoParaEmbedding` e para o `Titulo` do payload no Qdrant.
- Os limites de tamanho continuam medindo só o corpo.

## Qdrant (`IManualVectorStore`)

- Novo `RemoverAsync(int idJogoLink, CancellationToken)`: delete por filtro `IdJogoLink`. Se a coleção não existe, não faz nada.
- Novo `ListarIdsLinksAsync(CancellationToken)`: `FacetAsync` em `IdJogoLink` com `limit` alto (10.000) e `exact: true`, porque o padrão é 10. Se a coleção não existe, devolve vazio.
- Remover a constante `PortaGrpc`, que não é usada.
- Mantém a coleção por ambiente (`manuais` / `manuais_dev`) da alteração não commitada atual.

## Repositório `IIndexacaoManualRepository`

Novo, em `Infrastructure/Repositories`, `Scoped`:

- `GetElegiveisAsync(int maxTentativas)`: `List<ManualJob>` conforme a regra de elegibilidade.
- `GetEstadoAsync(int idJogoLink)`: `EstadoLinkManual?` (IdJogoLink, IdJogo, Tipo, Url, TituloLink, NomeJogo, JogoAtivo, `JogoLinkIndexacao?`).
- `ExisteIndexadoComHashAsync(int idJogo, string hash, int idJogoLinkExceto)`.
- `GetIdsDuplicadosAsync(int idJogo, int idJogoLinkExceto)`.
- `GetMetadadosPorHashAsync(string hash)`: uma linha com o mesmo hash e metadados preenchidos, ou nulo.
- `GetIdsComVetoresEsperadosAsync()`: ids de links de regra, com URL, de jogo ativo e com linha `Indexado`.
- `GetIdsLinksAsync(int idJogo)`.
- `SalvarAsync(JogoLinkIndexacao)`: insere ou atualiza.

`IJogoRepository` perde `GetJogosNaoIndexadosAsync` e `MarcarIndexadoAsync`, e os três fakes nos testes são ajustados.

## Produtores

- `ManualQueueExtensions.EnfileirarManuaisPendentes` passa a ser `EnfileirarSincronizacao(this IManualQueue, Jogo jogo, IEnumerable<int>? idsRemovidos = null)`. Enfileira todos os links do jogo (qualquer tipo) e os ids removidos, com o `IdJogo` do jogo.
- `CadastroJogo`: troca a chamada.
- `AtualizarJogo`: guarda os ids de `jogo.Links` antes do `UpdateModel`; depois do `SaveAsync`, calcula os removidos e chama `EnfileirarSincronizacao(jogo, removidos)`.
- `JogosController` (`DeleteJogo`, `DesativarCopiaJogo`, `ReativarJogo`, `ReativarCopiaJogo`): depois do save, `GetIdsLinksAsync(idJogo)` e enfileira cada id.

## Worker (`IndexacaoManuaisWorker`)

No start, dentro da Activity de inicialização:

1. `GetElegiveisAsync(SincronizarManual.MaxTentativas)` e enfileira cada job.
2. **Reconciliação:** `ListarIdsLinksAsync`, menos `GetIdsComVetoresEsperadosAsync`, menos os ids já enfileirados no passo 1. Cada id que sobra é enfileirado com `IdJogo = 0`, e o `SincronizarManual` apaga os vetores.

Falha em qualquer um dos dois passos é logada e não impede o laço de consumir a fila. O laço continua sequencial.

## Limpezas

- Remover `Src/Application/UseCases/RAG/MarkdownExtractor.cs` (não está no DI nem é usado).
- Remover `QdrantManualVectorStore.PortaGrpc`.
- Remover `JogoLink.Indexado`, `IJogoRepository.GetJogosNaoIndexadosAsync` e `IJogoRepository.MarcarIndexadoAsync`.
- O worker perde os métodos por etapa (`ExtrairMarkdownAsync`, `ChunkingAsync`, `EmbeddingAsync`, `SalvarVetoresAsync`, `MarcarIndexadoAsync`), que passam para o use case.

## Testes

xUnit com fakes, sem API nem banco, no padrão atual (`Tests/Domain`, `Tests/Fakes`).

- **`SincronizarManualTests`** (pasta temporária como `uploads`, fakes de repositório, vector store, extrator, revisor, embedding e fila; `ChunkingExtractor` real):
  - link apagado remove vetores e reenfileira duplicados;
  - link que virou vídeo e jogo desativado marcam `Removido` e removem vetores;
  - `Indexado` com a mesma URL não faz nada;
  - URL trocada remove os vetores antigos antes de extrair, reindexa, zera tentativas e reenfileira os duplicados;
  - mesmo hash no mesmo jogo marca `Duplicado`, e em jogo diferente indexa;
  - falha incrementa tentativas e grava erro;
  - com 3 tentativas não tenta de novo e garante que não há vetores;
  - cancelamento relança e mantém `Processando`;
  - cache de `raw.md` e `md` não chama extrator nem revisor;
  - arquivo legado `{guid}.md` é movido;
  - revisão parcial grava `{hash}.md` com `REVISAO_COMPLETA = false`, e falha total não grava nada e o chunking usa o raw;
  - o prefixo do contexto chega nos chunks.
- **`RevisaoMarkdownTests`:** blocos reproduzem o original byte a byte; parágrafo grande vai sozinho; cada uma das 5 travas; `MUDOS -> MODOS` e `Forrá -> Forró` passam; substituição limitada ao bloco.
- **`LlmMarkdownRevisorTests`** (`IChatClient` fake): JSON válido aplica; JSON com texto em volta é extraído; JSON inválido em um bloco deixa `Completa = false` e segue; falha total devolve o original com `Modelo = null`.
- **Atualizar:**
  - `ChunkingExtractorTests`: prefixo, seção sem título e contexto vazio;
  - `PdfTextExtractorTests`: nada a mudar (só exercita `ProximoModelo` e `Interpretar`), mas roda para confirmar que o novo retorno não quebrou nada;
  - `QdrantManualVectorStoreTests`: `Titulo` com prefixo no payload;
  - `CadastroJogoTests`: nova extensão da fila.
- **Sem teste unitário, verificados no dev:** consulta de elegíveis, migração, `RemoverAsync` e `ListarIdsLinksAsync`. Rodar contra o MySQL do docker e a coleção `manuais_dev`, e conferir:
  - a linha criada por link;
  - troca de URL reindexa;
  - link removido some do Qdrant;
  - desativar marca `Removido` e reativar volta a `Indexado` sem chamar o OCR;
  - PDF repetido no mesmo jogo fica `Duplicado`.

## Fora do escopo

- Barrar a indexação quando a nota do OCR fica abaixo do aceitável (a nota passa a ser registrada, o que permite fazer isso depois).
- Marcadores de página e hierarquia de títulos no prompt do OCR.
- Revisão humana no admin.
- Busca/consulta dos manuais e busca híbrida.
- Retentativa periódica de falhas (continua só no start e nas edições).

## Riscos conhecidos

- **Revisor de 8B pode propor troca errada que passa nas travas.** Exemplo: `Casqueiro -> Caseiro` tem distância 2 e a palavra aparece uma vez. Mitigação: prompt, log de toda correção aplicada e contagens na tabela para avaliar o modelo.
- **Revisor só de texto não corrige número lido errado.** Continua dependendo do OCR.
- **Renomear o jogo ou o título do link não reindexa sozinho** (a URL não mudou). O prefixo antigo fica no embedding até a próxima reindexação do link.
- **Após a migração nenhum link tem linha, então todos são reprocessados uma vez.** Os que já têm `.md` legado custam só revisão e embedding.
