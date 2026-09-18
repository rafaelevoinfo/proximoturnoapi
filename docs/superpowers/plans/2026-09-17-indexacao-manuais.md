# Indexação de manuais: ciclo de vida, cache por hash e revisor — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fazer o pipeline de indexação de manuais continuar correto ao longo do tempo (PDF trocado, link removido, jogo desativado, PDF repetido, falhas) e melhorar o texto indexado com uma etapa de revisão barata.

**Architecture:** O job da fila passa a carregar só ids; um use case (`SincronizarManual`) lê o estado atual do link no banco e decide indexar, reindexar, remover vetores ou marcar duplicado. Uma tabela nova (`JOGO_LINK_INDEXACAO`) guarda status, tentativas e metadados da extração, substituindo o bool `JOGO_LINK.INDEXADO`. Extração e revisão ficam em cache por SHA-256 do PDF, em arquivos na pasta de uploads.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core + MySQL, Microsoft.Extensions.AI sobre OpenRouter, Qdrant.Client 1.19, xUnit.

**Spec:** `docs/superpowers/specs/2026-09-15-indexacao-manuais-design.md`

## Global Constraints

- Build: `dotnet build ProximoTurnoApi.slnx`. Testes: `dotnet test Tests/Tests.csproj`. Migrações: `dotnet ef migrations add <Nome> --project Src/ProximoTurnoApi.csproj`.
- Código, nomes e comentários em português, seguindo o estilo do projeto: comentários explicam **por quê**, não o quê, e ficam sem acento dentro do corpo dos métodos (como no código atual).
- Use cases ficam em `Src/Application/UseCases`, herdam `UseCaseBasico` e são registrados como `Scoped` no `Program.cs`. Acesso a dados só por repositório em `Src/Infrastructure/Repositories`.
- `OperationCanceledException` é sempre relançada, nunca virada em log de erro.
- Modelos: OCR `["google/gemini-3.6-flash", "anthropic/claude-opus-5"]`; revisor `meta-llama/llama-3.1-8b-instruct`; embedding `openai/text-embedding-3-small` (inalterado).
- Constantes fixadas pelo spec: `MaxTentativas = 3`, `TamanhoBloco = 4000`, distância de edição aceita de 1 a 2, mínimo de 4 letras por palavra alterada, termo repetido 3 ou mais vezes é intocável, `limit` do facet do Qdrant 10.000 com `exact: true`.
- Arquivos derivados moram na raiz de `uploads/` (`{hash}.raw.md` e `{hash}.md`), porque o backup só sincroniza a raiz dessa pasta.
- Um commit por task, com a mensagem indicada no último passo.

## File Structure

**Criados:**
- `Src/Infrastructure/Models/JogoLinkIndexacao.cs` — entidade e enum `StatusIndexacao`.
- `Src/Infrastructure/Repositories/IndexacaoManualRepository.cs` — interface e implementação das consultas de indexação.
- `Src/Application/UseCases/RAG/ContextoManual.cs` — nome do jogo + título do manual, usado pelo chunking e pelo revisor.
- `Src/Application/UseCases/RAG/EstadoLinkManual.cs` — retrato do link no momento da sincronização.
- `Src/Application/UseCases/RAG/RevisaoMarkdown.cs` — lógica pura da revisão (blocos, distância de edição, travas).
- `Src/Application/UseCases/RAG/IRevisorMarkdown.cs` — contrato e `ResultadoRevisao`.
- `Src/Infrastructure/RAG/LlmMarkdownRevisor.cs` — revisor que fala com o modelo.
- `Src/Application/UseCases/RAG/SincronizarManual.cs` — o use case que decide e executa.
- `Tests/Fakes/IndexacaoManualFakes.cs` — fakes de ambiente, repositório, vector store, extrator, revisor e embedding.
- `Tests/Domain/RevisaoMarkdownTests.cs`, `Tests/Domain/LlmMarkdownRevisorTests.cs`, `Tests/Domain/SincronizarManualTests.cs`.

**Modificados:**
- `Src/Infrastructure/Repositories/DatabaseContext.cs` — `DbSet` e mapeamento da entidade nova.
- `Src/Infrastructure/Models/JogoLink.cs` — sai o `Indexado`.
- `Src/Infrastructure/Repositories/JogoRepository.cs` — saem `GetJogosNaoIndexadosAsync` e `MarcarIndexadoAsync`.
- `Src/Application/UseCases/RAG/ChunkingExtractor.cs` — contexto no caminho de títulos.
- `Src/Application/UseCases/RAG/ITextExtractor.cs` e `Src/Infrastructure/RAG/PdfTextExtractor.cs` — novo retorno, sem gravar arquivo.
- `Src/Application/UseCases/RAG/IManualVectorStore.cs` e `Src/Infrastructure/RAG/QdrantManualVectorStore.cs` — remover e listar; sai o `PortaGrpc`.
- `Src/Application/UseCases/RAG/ManualJob.cs` e `ManualQueue.cs` — job só com ids, nova extensão de enfileiramento.
- `Src/Application/Workers/IndexacaoManuaisWorker.cs` — laço fino, carga inicial e reconciliação.
- `Src/Application/UseCases/Jogo/CadastroJogo.cs`, `AtualizarJogo.cs`, `Src/Application/Controllers/JogosController.cs` — produtores.
- `Src/Domain/IAModels.cs`, `Src/Program.cs`.
- `Tests/Domain/ChunkingExtractorTests.cs`, `Tests/Domain/CadastroJogoTests.cs`, `Tests/Fakes/PedidoUseCaseFakes.cs`, `Tests/Domain/ValidarCupomTests.cs`.

**Removido:** `Src/Application/UseCases/RAG/MarkdownExtractor.cs`.

---

### Task 1: Tabela de indexação (entidade, mapeamento e migração)

**Files:**
- Create: `Src/Infrastructure/Models/JogoLinkIndexacao.cs`
- Modify: `Src/Infrastructure/Repositories/DatabaseContext.cs` (método `ConfigureJogo`, por volta da linha 77, e a lista de `DbSet`, por volta da linha 238)
- Create: `Src/Migrations/<timestamp>_criando_tabela_jogo_link_indexacao.cs` (gerado pelo CLI)

**Interfaces:**
- Consumes: `BaseModel` (`Src/Infrastructure/Models/BaseModel.cs`), que já dá `Id` mapeado na coluna `ID`.
- Produces: `JogoLinkIndexacao` (propriedades `IdJogoLink`, `Url`, `HashPdf`, `Status`, `Tentativas`, `UltimoErro`, `ModeloExtracao`, `ConfiabilidadeExtracao`, `ModeloRevisao`, `CorrecoesAplicadas`, `CorrecoesDescartadas`, `RevisaoCompleta`, `QuantidadeChunks`, `DataAtualizacao`, `DataIndexacao`), o enum `StatusIndexacao` e `DatabaseContext.JogoLinkIndexacoes`.

- [ ] **Step 1: Criar a entidade**

`Src/Infrastructure/Models/JogoLinkIndexacao.cs`:

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProximoTurnoApi.Infrastructure.Models;

public enum StatusIndexacao : short {
    /// <summary>Sincronização em andamento. Encontrado no start, significa que a aplicação caiu no meio.</summary>
    Processando = 0,
    Indexado = 1,
    Falhou = 2,
    /// <summary>O link existe mas não deve ter vetores: virou vídeo, ficou sem URL ou o jogo foi desativado.</summary>
    Removido = 3,
    /// <summary>Outro link do mesmo jogo já indexou este mesmo PDF.</summary>
    Duplicado = 4
}

/// <summary>
/// Situação da indexação de um manual. Uma linha por link de regra: o bool que existia
/// no JOGO_LINK não tinha onde guardar tentativa, erro, hash nem modelo usado.
/// </summary>
[Table("JOGO_LINK_INDEXACAO")]
public class JogoLinkIndexacao : BaseModel {

    [Column("ID_JOGO_LINK")]
    public int IdJogoLink { get; set; }

    /// <summary>URL processada na última sincronização. É o que denuncia a troca de PDF no link.</summary>
    [Column("URL"), MaxLength(300)]
    public required string Url { get; set; }

    [Column("HASH_PDF"), MaxLength(64)]
    public string? HashPdf { get; set; }

    [Column("STATUS")]
    public StatusIndexacao Status { get; set; }

    /// <summary>Falhas consecutivas na URL atual. Zera no sucesso e quando a URL muda.</summary>
    [Column("TENTATIVAS")]
    public int Tentativas { get; set; }

    [Column("ULTIMO_ERRO"), MaxLength(1000)]
    public string? UltimoErro { get; set; }

    [Column("MODELO_EXTRACAO"), MaxLength(100)]
    public string? ModeloExtracao { get; set; }

    [Column("CONFIABILIDADE_EXTRACAO")]
    public short? ConfiabilidadeExtracao { get; set; }

    [Column("MODELO_REVISAO"), MaxLength(100)]
    public string? ModeloRevisao { get; set; }

    [Column("CORRECOES_APLICADAS")]
    public int? CorrecoesAplicadas { get; set; }

    [Column("CORRECOES_DESCARTADAS")]
    public int? CorrecoesDescartadas { get; set; }

    /// <summary>Falso quando algum bloco não chegou a ser revisado; serve para refazer depois.</summary>
    [Column("REVISAO_COMPLETA")]
    public bool? RevisaoCompleta { get; set; }

    [Column("QUANTIDADE_CHUNKS")]
    public int? QuantidadeChunks { get; set; }

    [Column("DATA_ATUALIZACAO")]
    public DateTime DataAtualizacao { get; set; }

    [Column("DATA_INDEXACAO")]
    public DateTime? DataIndexacao { get; set; }
}
```

- [ ] **Step 2: Mapear no DatabaseContext**

Em `Src/Infrastructure/Repositories/DatabaseContext.cs`, no fim do método `ConfigureJogo` (depois do bloco que configura `Copias`, por volta da linha 109):

```csharp
        modelBuilder.Entity<JogoLinkIndexacao>(b => {
            // Uma linha por link, e apagar o link leva a linha junto: indexacao sem link
            // nao significa nada e so atrapalharia a reconciliacao.
            b.HasIndex(i => i.IdJogoLink).IsUnique();
            b.HasIndex(i => i.HashPdf);
            b.HasOne<JogoLink>()
             .WithOne()
             .HasForeignKey<JogoLinkIndexacao>(i => i.IdJogoLink)
             .OnDelete(DeleteBehavior.Cascade);
        });
```

E na lista de `DbSet`, junto de `JogoLinks` (por volta da linha 240):

```csharp
    public DbSet<JogoLinkIndexacao> JogoLinkIndexacoes { get; set; }
```

- [ ] **Step 3: Compilar**

Run: `dotnet build ProximoTurnoApi.slnx`
Expected: build sem erros.

- [ ] **Step 4: Gerar a migração**

Run: `dotnet ef migrations add criando_tabela_jogo_link_indexacao --project Src/ProximoTurnoApi.csproj`
Expected: cria `Src/Migrations/<timestamp>_criando_tabela_jogo_link_indexacao.cs`.

- [ ] **Step 5: Conferir o SQL gerado**

Run: `dotnet ef migrations script --project Src/ProximoTurnoApi.csproj --idempotent --output C:\Users\rafae\AppData\Local\Temp\claude\migracao.sql`
Expected: o arquivo contém `CREATE TABLE ... JOGO_LINK_INDEXACAO`, a FK para `JOGO_LINK` com `ON DELETE CASCADE` e os dois índices (único em `ID_JOGO_LINK`, comum em `HASH_PDF`). Nenhum `DROP` de coluna existente.

- [ ] **Step 6: Aplicar no banco de dev**

Run: `docker-compose up -d` (se o MySQL não estiver no ar) e depois `dotnet ef database update --project Src/ProximoTurnoApi.csproj`
Expected: "Done." e a tabela criada.

- [ ] **Step 7: Rodar os testes**

Run: `dotnet test Tests/Tests.csproj`
Expected: tudo verde (nada dependia da tabela ainda).

- [ ] **Step 8: Commit**

```bash
git add Src/Infrastructure/Models/JogoLinkIndexacao.cs Src/Infrastructure/Repositories/DatabaseContext.cs Src/Migrations
git commit -m "feat: tabela de indexacao de manuais"
```

---

### Task 2: Repositório da indexação

**Files:**
- Create: `Src/Application/UseCases/RAG/EstadoLinkManual.cs`
- Create: `Src/Infrastructure/Repositories/IndexacaoManualRepository.cs`
- Modify: `Src/Program.cs` (bloco "Indexacao de manuais (RAG)", por volta da linha 81)

**Interfaces:**
- Consumes: `JogoLinkIndexacao`, `StatusIndexacao` (Task 1); `ManualJob` (hoje `record ManualJob(int IdJogoLink, int IdJogo, string Url)`, que vira `record ManualJob(int IdJogoLink, int IdJogo)` na Task 9); `BaseRepository.SaveChangesAsync<T>(DbSet<T>, T, bool)`.
- Produces: `IIndexacaoManualRepository` com `GetElegiveisAsync(int)`, `GetEstadoAsync(int)`, `ExisteIndexadoComHashAsync(int, string, int)`, `GetIdsDuplicadosAsync(int, int)`, `GetMetadadosPorHashAsync(string)`, `GetIdsComVetoresEsperadosAsync()`, `GetIdsLinksAsync(int)` e `SalvarAsync(JogoLinkIndexacao)`; e o record `EstadoLinkManual`.

> Nesta task o `ManualJob` ainda tem `Url`. Para a projeção compilar, use `new ManualJob(jl.Id, jl.IdJogo, jl.Url)` no `GetElegiveisAsync` e troque para `new ManualJob(jl.Id, jl.IdJogo)` na Task 9, que é onde o record muda.

- [ ] **Step 1: Criar o retrato do link**

`Src/Application/UseCases/RAG/EstadoLinkManual.cs`:

```csharp
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.UseCases.RAG;

/// <summary>
/// Retrato do link no instante da sincronização. Vem em uma consulta só porque a decisão
/// do <see cref="SincronizarManual"/> depende de todos estes campos ao mesmo tempo.
/// </summary>
public sealed record EstadoLinkManual(
    int IdJogoLink,
    int IdJogo,
    TipoLink Tipo,
    string Url,
    string TituloLink,
    string NomeJogo,
    bool JogoAtivo,
    JogoLinkIndexacao? Indexacao);
```

- [ ] **Step 2: Criar o repositório**

`Src/Infrastructure/Repositories/IndexacaoManualRepository.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Application.UseCases.RAG;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Infrastructure.Repositories;

public interface IIndexacaoManualRepository : IBaseRepository {
    Task<List<ManualJob>> GetElegiveisAsync(int maxTentativas);
    Task<EstadoLinkManual?> GetEstadoAsync(int idJogoLink);
    Task<bool> ExisteIndexadoComHashAsync(int idJogo, string hash, int idJogoLinkExceto);
    Task<List<int>> GetIdsDuplicadosAsync(int idJogo, int idJogoLinkExceto);
    Task<JogoLinkIndexacao?> GetMetadadosPorHashAsync(string hash);
    Task<HashSet<int>> GetIdsComVetoresEsperadosAsync();
    Task<List<int>> GetIdsLinksAsync(int idJogo);
    Task SalvarAsync(JogoLinkIndexacao indexacao);
}

public class IndexacaoManualRepository(DatabaseContext context) : BaseRepository(context), IIndexacaoManualRepository {

    /// <summary>
    /// Links que ainda têm algo a fazer: sem linha, com a URL trocada, no meio de um
    /// processamento interrompido ou com falha que ainda não esgotou as tentativas.
    /// Jogo desativado fica de fora: indexar gastaria embedding e poluiria a busca.
    /// </summary>
    public async Task<List<ManualJob>> GetElegiveisAsync(int maxTentativas) {
        return await _dbContext.JogoLinks
            .Where(jl => jl.Tipo == TipoLink.Regra && jl.Url != "")
            .Where(jl => _dbContext.JogoCopias.Any(jc => jc.IdJogo == jl.IdJogo && jc.Status != StatusJogo.Desativado))
            .Where(jl => !_dbContext.JogoLinkIndexacoes.Any(i =>
                i.IdJogoLink == jl.Id &&
                i.Url == jl.Url &&
                (i.Status == StatusIndexacao.Indexado ||
                 (i.Status == StatusIndexacao.Falhou && i.Tentativas >= maxTentativas))))
            .Select(jl => new ManualJob(jl.Id, jl.IdJogo, jl.Url))
            .ToListAsync();
    }

    public async Task<EstadoLinkManual?> GetEstadoAsync(int idJogoLink) {
        return await _dbContext.JogoLinks
            .Where(jl => jl.Id == idJogoLink)
            .Select(jl => new EstadoLinkManual(
                jl.Id,
                jl.IdJogo,
                jl.Tipo,
                jl.Url,
                jl.Titulo,
                _dbContext.Jogos.Where(j => j.Id == jl.IdJogo).Select(j => j.Nome).FirstOrDefault() ?? "",
                _dbContext.JogoCopias.Any(jc => jc.IdJogo == jl.IdJogo && jc.Status != StatusJogo.Desativado),
                _dbContext.JogoLinkIndexacoes.FirstOrDefault(i => i.IdJogoLink == jl.Id)))
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Diz se outro link do mesmo jogo já indexou este conteúdo. Dois links com o mesmo
    /// PDF encheriam o topo da busca do jogo com o mesmo trecho repetido.
    /// </summary>
    public async Task<bool> ExisteIndexadoComHashAsync(int idJogo, string hash, int idJogoLinkExceto) {
        return await _dbContext.JogoLinkIndexacoes
            .AnyAsync(i => i.Status == StatusIndexacao.Indexado &&
                           i.HashPdf == hash &&
                           i.IdJogoLink != idJogoLinkExceto &&
                           _dbContext.JogoLinks.Any(jl => jl.Id == i.IdJogoLink && jl.IdJogo == idJogo));
    }

    public async Task<List<int>> GetIdsDuplicadosAsync(int idJogo, int idJogoLinkExceto) {
        return await _dbContext.JogoLinkIndexacoes
            .Where(i => i.Status == StatusIndexacao.Duplicado &&
                        i.IdJogoLink != idJogoLinkExceto &&
                        _dbContext.JogoLinks.Any(jl => jl.Id == i.IdJogoLink && jl.IdJogo == idJogo))
            .Select(i => i.IdJogoLink)
            .ToListAsync();
    }

    /// <summary>
    /// Metadados são do conteúdo, não do link: quem reaproveita o cache de um hash herda
    /// o modelo e a nota de quem pagou a extração.
    /// </summary>
    public async Task<JogoLinkIndexacao?> GetMetadadosPorHashAsync(string hash) {
        return await _dbContext.JogoLinkIndexacoes
            .Where(i => i.HashPdf == hash && i.ModeloExtracao != null)
            .OrderByDescending(i => i.DataAtualizacao)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Links que legitimamente têm vetores no Qdrant. O que estiver lá fora desta lista
    /// é órfão: link apagado, jogo desativado ou link que virou vídeo.
    /// </summary>
    public async Task<HashSet<int>> GetIdsComVetoresEsperadosAsync() {
        var ids = await _dbContext.JogoLinkIndexacoes
            .Where(i => i.Status == StatusIndexacao.Indexado)
            .Where(i => _dbContext.JogoLinks.Any(jl =>
                jl.Id == i.IdJogoLink &&
                jl.Tipo == TipoLink.Regra &&
                jl.Url != "" &&
                _dbContext.JogoCopias.Any(jc => jc.IdJogo == jl.IdJogo && jc.Status != StatusJogo.Desativado)))
            .Select(i => i.IdJogoLink)
            .ToListAsync();

        return [.. ids];
    }

    public async Task<List<int>> GetIdsLinksAsync(int idJogo) {
        return await _dbContext.JogoLinks
            .Where(jl => jl.IdJogo == idJogo)
            .Select(jl => jl.Id)
            .ToListAsync();
    }

    public async Task SalvarAsync(JogoLinkIndexacao indexacao) {
        indexacao.DataAtualizacao = DateTime.Now;
        await SaveChangesAsync(_dbContext.JogoLinkIndexacoes, indexacao);
    }
}
```

- [ ] **Step 3: Registrar no DI**

Em `Src/Program.cs`, no bloco "Indexacao de manuais (RAG)", depois de `builder.Services.AddSingleton<IManualQueue, ManualQueue>();`:

```csharp
builder.Services.AddScoped<IIndexacaoManualRepository, IndexacaoManualRepository>();
```

- [ ] **Step 4: Compilar e rodar os testes**

Run: `dotnet build ProximoTurnoApi.slnx` e depois `dotnet test Tests/Tests.csproj`
Expected: build limpo e testes verdes. Estas consultas viram SQL e não têm teste unitário; elas são exercidas na Task 11.

- [ ] **Step 5: Commit**

```bash
git add Src/Application/UseCases/RAG/EstadoLinkManual.cs Src/Infrastructure/Repositories/IndexacaoManualRepository.cs Src/Program.cs
git commit -m "feat: repositorio de indexacao de manuais"
```

---

### Task 3: Lógica pura da revisão (blocos, distância e travas)

**Files:**
- Create: `Src/Application/UseCases/RAG/ContextoManual.cs`
- Create: `Src/Application/UseCases/RAG/RevisaoMarkdown.cs`
- Test: `Tests/Domain/RevisaoMarkdownTests.cs`

**Interfaces:**
- Consumes: nada de tasks anteriores.
- Produces: `ContextoManual(string NomeJogo, string TituloManual)` com a propriedade `Prefixo`; `CorrecaoRevisao(string Original, string Corrigido, string Motivo)`; `ResultadoBloco(string Texto, List<CorrecaoRevisao> Aplicadas, List<(CorrecaoRevisao Correcao, string Motivo)> Descartadas)`; e os estáticos `RevisaoMarkdown.DividirEmBlocos(string)`, `RevisaoMarkdown.ValidarEAplicar(string bloco, IReadOnlyList<CorrecaoRevisao>, string documento, ContextoManual)`, `RevisaoMarkdown.Distancia(string, string)`, `RevisaoMarkdown.Ocorrencias(string, string)` e as constantes `TamanhoBloco`, `DistanciaMaxima`, `MinimoDeLetras`, `MaximoDePalavras`, `RepeticoesParaTermoDoJogo`.

- [ ] **Step 1: Escrever os testes que falham**

`Tests/Domain/RevisaoMarkdownTests.cs`:

```csharp
using ProximoTurnoApi.Application.UseCases.RAG;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class RevisaoMarkdownTests {

    private static readonly ContextoManual Contexto = new("Balde de Caranguejo", "Modos extras");

    private static ResultadoBloco Aplicar(string bloco, params CorrecaoRevisao[] correcoes) =>
        RevisaoMarkdown.ValidarEAplicar(bloco, correcoes, bloco, Contexto);

    private static CorrecaoRevisao Correcao(string original, string corrigido) =>
        new(original, corrigido, "erro de leitura");

    [Fact]
    public void DividirEmBlocos_ConcatenacaoReproduzOOriginal() {
        var markdown = string.Join("\n\n", Enumerable.Range(0, 40).Select(i => $"# Seção {i}\n\nTexto da seção {i}. " + new string('x', 300)));

        var blocos = RevisaoMarkdown.DividirEmBlocos(markdown);

        // Se a concatenacao nao bate, o revisor devolveria um documento com o espacamento
        // mexido: o chunking depende de linha em branco para separar blocos.
        Assert.True(blocos.Count > 1);
        Assert.Equal(markdown, string.Concat(blocos));
    }

    [Fact]
    public void DividirEmBlocos_RespeitaOTamanhoQuandoOsParagrafosCabem() {
        var paragrafo = new string('a', 500);
        var markdown = string.Join("\n\n", Enumerable.Repeat(paragrafo, 20));

        var blocos = RevisaoMarkdown.DividirEmBlocos(markdown);

        Assert.All(blocos, bloco => Assert.True(bloco.Length <= RevisaoMarkdown.TamanhoBloco + paragrafo.Length));
        Assert.Equal(markdown, string.Concat(blocos));
    }

    [Fact]
    public void DividirEmBlocos_ParagrafoMaiorQueOLimite_VaiSozinhoEInteiro() {
        var gigante = new string('b', RevisaoMarkdown.TamanhoBloco * 2);
        var markdown = $"Curto.\n\n{gigante}\n\nOutro curto.";

        var blocos = RevisaoMarkdown.DividirEmBlocos(markdown);

        // Cortar um paragrafo ao meio faria o revisor julgar um texto truncado.
        Assert.Contains(blocos, bloco => bloco.Contains(gigante));
        Assert.Equal(markdown, string.Concat(blocos));
    }

    [Fact]
    public void ValidarEAplicar_ErroDeUmaLetra_Aplica() {
        var resultado = Aplicar("# MUDOS EXTRAS\n\nJogue.", Correcao("MUDOS", "MODOS"));

        Assert.Equal("# MODOS EXTRAS\n\nJogue.", resultado.Texto);
        Assert.Single(resultado.Aplicadas);
        Assert.Empty(resultado.Descartadas);
    }

    [Fact]
    public void ValidarEAplicar_AcentoTrocado_Aplica() {
        var resultado = Aplicar("# Modo Forrá\n\nJogado em times.", Correcao("Forrá", "Forró"));

        Assert.Contains("Modo Forró", resultado.Texto);
    }

    [Fact]
    public void ValidarEAplicar_TrechoQueNaoExisteNoBloco_Descarta() {
        var resultado = Aplicar("Texto sem erro.", Correcao("MUDOS", "MODOS"));

        Assert.Equal("Texto sem erro.", resultado.Texto);
        Assert.Single(resultado.Descartadas);
    }

    [Fact]
    public void ValidarEAplicar_MudarNumero_Descarta() {
        // Numero lido errado so o PDF resolve. Deixar um modelo de 8B "corrigir" numero
        // e trocar um erro de leitura por uma regra inventada.
        var resultado = Aplicar("Pegue 4 moedas.", Correcao("4 moedas", "6 moedas"));

        Assert.Equal("Pegue 4 moedas.", resultado.Texto);
        Assert.Single(resultado.Descartadas);
    }

    [Fact]
    public void ValidarEAplicar_ReescritaLonga_Descarta() {
        var resultado = Aplicar("Modo Avexado é rápido.", Correcao("Avexado", "Apressado"));

        Assert.Contains("Avexado", resultado.Texto);
        Assert.Single(resultado.Descartadas);
    }

    [Fact]
    public void ValidarEAplicar_PalavraCurta_Descarta() {
        var resultado = Aplicar("Carta de cor azul.", Correcao("de cor", "da cor"));

        Assert.Contains("de cor", resultado.Texto);
        Assert.Single(resultado.Descartadas);
    }

    [Fact]
    public void ValidarEAplicar_PalavraDoNomeDoJogo_Descarta() {
        var resultado = Aplicar("Organize o Balde do jogo.", Correcao("Balde", "Balda"));

        Assert.Contains("Balde", resultado.Texto);
        Assert.Single(resultado.Descartadas);
    }

    [Fact]
    public void ValidarEAplicar_TermoRepetidoNoDocumento_Descarta() {
        // Uso consistente e sinal de termo inventado pelo jogo, nao de erro de OCR.
        // A palavra precisa ser longa: uma curta como "Oxi!" ja seria barrada antes,
        // pela trava de minimo de letras, e o teste nao exercitaria esta regra.
        var documento = "Modo Avexado é rápido. No Avexado você grita. Vence quem sair do Avexado primeiro.";

        var resultado = RevisaoMarkdown.ValidarEAplicar(documento, [Correcao("Avexado", "Avexada")], documento, Contexto);

        Assert.Equal(documento, resultado.Texto);
        Assert.Single(resultado.Descartadas);
    }

    [Fact]
    public void ValidarEAplicar_SubstituiTodasAsOcorrenciasDoBloco() {
        var resultado = Aplicar("MUDOS de jogo. Os MUDOS são três.", Correcao("MUDOS", "MODOS"));

        Assert.Equal("MODOS de jogo. Os MODOS são três.", resultado.Texto);
    }

    [Theory]
    [InlineData("MUDOS", "MODOS", 1)]
    [InlineData("Forrá", "Forró", 1)]
    [InlineData("Cornponentes", "Componentes", 2)]
    [InlineData("casa", "casa", 0)]
    public void Distancia_ContaOperacoesDeUmCaractere(string a, string b, int esperado) {
        Assert.Equal(esperado, RevisaoMarkdown.Distancia(a, b));
    }

    [Fact]
    public void Ocorrencias_ContaPalavraInteiraIgnorandoMaiusculas() {
        var documento = "Modo ligeiro. No MODO selvagem, o modojogo não conta.";

        Assert.Equal(2, RevisaoMarkdown.Ocorrencias(documento, "modo"));
    }
}
```

- [ ] **Step 2: Rodar os testes e ver falhar**

Run: `dotnet test Tests/Tests.csproj --filter FullyQualifiedName~RevisaoMarkdownTests`
Expected: erro de compilação (`ContextoManual`, `RevisaoMarkdown` e `CorrecaoRevisao` não existem).

- [ ] **Step 3: Criar o contexto do manual**

`Src/Application/UseCases/RAG/ContextoManual.cs`:

```csharp
namespace ProximoTurnoApi.Application.UseCases.RAG;

/// <summary>
/// De que manual de que jogo o texto veio. Serve ao chunking, que precisa disso no
/// caminho de títulos, e ao revisor, que precisa saber o que não pode corrigir.
/// </summary>
public sealed record ContextoManual(string NomeJogo, string TituloManual) {

    public string Prefixo =>
        string.Join(" > ", new[] { NomeJogo, TituloManual }.Where(parte => !string.IsNullOrWhiteSpace(parte)));
}
```

- [ ] **Step 4: Implementar a lógica pura**

`Src/Application/UseCases/RAG/RevisaoMarkdown.cs`:

```csharp
using System.Text;

namespace ProximoTurnoApi.Application.UseCases.RAG;

/// <summary>Uma troca proposta pelo revisor.</summary>
public sealed record CorrecaoRevisao(string Original, string Corrigido, string Motivo);

/// <summary>Bloco depois de aplicadas as correções que passaram nas travas.</summary>
public sealed record ResultadoBloco(string Texto,
                                    List<CorrecaoRevisao> Aplicadas,
                                    List<(CorrecaoRevisao Correcao, string Motivo)> Descartadas);

/// <summary>
/// Parte determinística da revisão: corta o manual em blocos e decide o que pode entrar
/// no texto. O prompt não segura um modelo de 8B, então quem manda é este arquivo.
/// </summary>
public static class RevisaoMarkdown {

    // Blocos pequenos porque um modelo de 8B perde atencao em texto longo, e porque
    // alguns provedores do OpenRouter servem esse modelo com contexto curto.
    public const int TamanhoBloco = 4000;

    // Erro de OCR quase sempre custa uma ou duas letras. Acima disso o modelo esta
    // reescrevendo, e reescrita e exatamente o que nao queremos aqui.
    public const int DistanciaMaxima = 2;
    public const int MinimoDeLetras = 4;
    public const int MaximoDePalavras = 3;
    public const int RepeticoesParaTermoDoJogo = 3;

    private static readonly char[] SeparadoresDePalavra = [' ', '\n', '\r', '\t'];
    private static readonly char[] PontuacaoDasPontas = ['.', ',', ';', ':', '!', '?', '"', '\'', '(', ')', '[', ']', '*', '_', '-', '#', '>'];

    /// <summary>
    /// Corta o markdown em blocos de até <see cref="TamanhoBloco"/> caracteres, sempre em
    /// linha em branco. A concatenação dos blocos reproduz o original caractere a caractere.
    /// </summary>
    public static List<string> DividirEmBlocos(string markdown) {
        var blocos = new List<string>();
        if (string.IsNullOrEmpty(markdown)) {
            return blocos;
        }

        var atual = new StringBuilder();
        foreach (var paragrafo in SepararParagrafos(markdown)) {
            if (atual.Length > 0 && atual.Length + paragrafo.Length > TamanhoBloco) {
                blocos.Add(atual.ToString());
                atual.Clear();
            }

            atual.Append(paragrafo);
        }

        if (atual.Length > 0) {
            blocos.Add(atual.ToString());
        }

        return blocos;
    }

    /// <summary>
    /// Separa os parágrafos deixando a linha em branco colada no fim de cada um, para que
    /// juntar os pedaços de volta devolva o documento como ele era.
    /// </summary>
    private static IEnumerable<string> SepararParagrafos(string markdown) {
        var inicio = 0;
        var i = 0;

        while (i < markdown.Length) {
            if (markdown[i] != '\n') {
                i++;
                continue;
            }

            var fim = i + 1;
            var quebras = 1;
            while (fim < markdown.Length && (markdown[fim] is '\n' or '\r' or ' ' or '\t')) {
                if (markdown[fim] == '\n') {
                    quebras++;
                }
                fim++;
            }

            if (quebras >= 2) {
                yield return markdown[inicio..fim];
                inicio = fim;
            }

            i = fim;
        }

        if (inicio < markdown.Length) {
            yield return markdown[inicio..];
        }
    }

    /// <summary>
    /// Aplica no bloco as correções que passam nas travas. O documento inteiro vem junto
    /// porque uma das travas olha quantas vezes a palavra aparece no manual todo.
    /// </summary>
    public static ResultadoBloco ValidarEAplicar(string bloco,
                                                 IReadOnlyList<CorrecaoRevisao> correcoes,
                                                 string documento,
                                                 ContextoManual contexto) {
        var texto = bloco;
        var aplicadas = new List<CorrecaoRevisao>();
        var descartadas = new List<(CorrecaoRevisao, string)>();

        foreach (var correcao in correcoes) {
            var motivo = Barrar(texto, correcao, documento, contexto);
            if (motivo is not null) {
                descartadas.Add((correcao, motivo));
                continue;
            }

            texto = texto.Replace(correcao.Original, correcao.Corrigido, StringComparison.Ordinal);
            aplicadas.Add(correcao);
        }

        return new ResultadoBloco(texto, aplicadas, descartadas);
    }

    /// <summary>
    /// Diz por que a correção não pode entrar, ou null quando ela passa.
    /// </summary>
    private static string? Barrar(string bloco, CorrecaoRevisao correcao, string documento, ContextoManual contexto) {
        if (string.IsNullOrEmpty(correcao.Original) || !bloco.Contains(correcao.Original, StringComparison.Ordinal)) {
            return "trecho não encontrado no bloco";
        }

        if (Digitos(correcao.Original) != Digitos(correcao.Corrigido)) {
            return "mudaria um número";
        }

        var distancia = Distancia(correcao.Original, correcao.Corrigido);
        if (distancia is < 1 or > DistanciaMaxima) {
            return $"distância de edição {distancia}";
        }

        var originais = Palavras(correcao.Original);
        if (originais.Length > MaximoDePalavras) {
            return "trecho longo demais";
        }

        var corrigidas = Palavras(correcao.Corrigido);
        var alteradas = originais.Where(p => !corrigidas.Contains(p, StringComparer.Ordinal)).ToArray();
        if (alteradas.Length == 0) {
            return "nenhuma palavra muda";
        }

        foreach (var palavra in alteradas) {
            if (palavra.Count(char.IsLetter) < MinimoDeLetras) {
                return $"palavra curta demais: {palavra}";
            }

            if (EhTermoDoContexto(palavra, contexto)) {
                return $"palavra do nome do jogo ou do manual: {palavra}";
            }

            if (Ocorrencias(documento, palavra) >= RepeticoesParaTermoDoJogo) {
                return $"termo usado {RepeticoesParaTermoDoJogo}+ vezes no manual: {palavra}";
            }
        }

        return null;
    }

    /// <summary>
    /// Distância de edição: quantas inserções, remoções ou trocas de um caractere levam
    /// de um texto ao outro.
    /// </summary>
    public static int Distancia(string a, string b) {
        if (a.Length == 0) {
            return b.Length;
        }

        if (b.Length == 0) {
            return a.Length;
        }

        var anterior = new int[b.Length + 1];
        var atual = new int[b.Length + 1];

        for (var j = 0; j <= b.Length; j++) {
            anterior[j] = j;
        }

        for (var i = 1; i <= a.Length; i++) {
            atual[0] = i;
            for (var j = 1; j <= b.Length; j++) {
                var custo = a[i - 1] == b[j - 1] ? 0 : 1;
                atual[j] = Math.Min(Math.Min(atual[j - 1] + 1, anterior[j] + 1), anterior[j - 1] + custo);
            }

            (anterior, atual) = (atual, anterior);
        }

        return anterior[b.Length];
    }

    /// <summary>Quantas vezes a palavra aparece inteira no documento, ignorando maiúsculas.</summary>
    public static int Ocorrencias(string documento, string palavra) {
        if (palavra.Length == 0) {
            return 0;
        }

        var total = 0;
        var indice = documento.IndexOf(palavra, StringComparison.OrdinalIgnoreCase);

        while (indice >= 0) {
            var fim = indice + palavra.Length;
            var antes = indice == 0 || !char.IsLetterOrDigit(documento[indice - 1]);
            var depois = fim >= documento.Length || !char.IsLetterOrDigit(documento[fim]);

            if (antes && depois) {
                total++;
            }

            indice = indice + 1 < documento.Length
                ? documento.IndexOf(palavra, indice + 1, StringComparison.OrdinalIgnoreCase)
                : -1;
        }

        return total;
    }

    private static string Digitos(string texto) => new([.. texto.Where(char.IsDigit)]);

    private static string[] Palavras(string texto) =>
        [.. texto.Split(SeparadoresDePalavra, StringSplitOptions.RemoveEmptyEntries)
                 .Select(p => p.Trim(PontuacaoDasPontas))
                 .Where(p => p.Length > 0)];

    private static bool EhTermoDoContexto(string palavra, ContextoManual contexto) =>
        Palavras($"{contexto.NomeJogo} {contexto.TituloManual}")
            .Where(p => p.Count(char.IsLetter) >= 3)
            .Any(p => string.Equals(p, palavra, StringComparison.OrdinalIgnoreCase));
}
```

- [ ] **Step 5: Rodar os testes e ver passar**

Run: `dotnet test Tests/Tests.csproj --filter FullyQualifiedName~RevisaoMarkdownTests`
Expected: todos passam.

- [ ] **Step 6: Commit**

```bash
git add Src/Application/UseCases/RAG/ContextoManual.cs Src/Application/UseCases/RAG/RevisaoMarkdown.cs Tests/Domain/RevisaoMarkdownTests.cs
git commit -m "feat: logica de revisao do markdown dos manuais"
```

---

### Task 4: Revisor que fala com o modelo

**Files:**
- Create: `Src/Application/UseCases/RAG/IRevisorMarkdown.cs`
- Create: `Src/Infrastructure/RAG/LlmMarkdownRevisor.cs`
- Modify: `Src/Domain/IAModels.cs`
- Modify: `Src/Program.cs`
- Test: `Tests/Domain/LlmMarkdownRevisorTests.cs`

**Interfaces:**
- Consumes: `RevisaoMarkdown.DividirEmBlocos`, `RevisaoMarkdown.ValidarEAplicar`, `ContextoManual`, `CorrecaoRevisao` (Task 3).
- Produces: `IRevisorMarkdown.RevisarAsync(string markdown, ContextoManual contexto, CancellationToken)` devolvendo `ResultadoRevisao(string Texto, string? Modelo, int Aplicadas, int Descartadas, bool Completa)`; `LlmMarkdownRevisor.ChaveChat` (chave do `IChatClient` no DI); `LlmMarkdownRevisor.Interpretar(string?)`; `IAModel.REVISOR_MODEL`.

- [ ] **Step 1: Escrever os testes que falham**

`Tests/Domain/LlmMarkdownRevisorTests.cs`:

```csharp
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using ProximoTurnoApi.Application.UseCases.RAG;
using ProximoTurnoApi.Infrastructure.RAG;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class LlmMarkdownRevisorTests {

    private const string Falha = "__falha__";
    private static readonly ContextoManual Contexto = new("Balde de Caranguejo", "Modos extras");

    /// <summary>Devolve uma resposta pronta por chamada, na ordem em que foram configuradas.</summary>
    private sealed class ChatFalso(params string[] respostas) : IChatClient {
        private readonly Queue<string> _respostas = new(respostas);

        public List<string> Recebidos { get; } = [];

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) {
            Recebidos.Add(string.Join("\n", messages.Select(m => m.Text)));

            var resposta = _respostas.Count > 0 ? _respostas.Dequeue() : "{\"correcoes\":[]}";
            if (resposta == Falha) {
                throw new HttpRequestException("provedor fora do ar");
            }

            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, resposta)));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    private static LlmMarkdownRevisor Revisor(ChatFalso chat) =>
        new(NullLogger<LlmMarkdownRevisor>.Instance, chat);

    [Fact]
    public async Task RevisarAsync_AplicaCorrecaoDoModelo() {
        var chat = new ChatFalso("{\"correcoes\":[{\"original\":\"MUDOS\",\"corrigido\":\"MODOS\",\"motivo\":\"leitura\"}]}");

        var resultado = await Revisor(chat).RevisarAsync("# MUDOS EXTRAS\n\nJogue.", Contexto, CancellationToken.None);

        Assert.Equal("# MODOS EXTRAS\n\nJogue.", resultado.Texto);
        Assert.Equal(1, resultado.Aplicadas);
        Assert.Equal(0, resultado.Descartadas);
        Assert.True(resultado.Completa);
        Assert.NotNull(resultado.Modelo);
    }

    [Fact]
    public async Task RevisarAsync_CorrecaoBarradaPelasTravas_ContaComoDescartada() {
        var chat = new ChatFalso("{\"correcoes\":[{\"original\":\"4 moedas\",\"corrigido\":\"6 moedas\",\"motivo\":\"leitura\"}]}");

        var resultado = await Revisor(chat).RevisarAsync("Pegue 4 moedas.", Contexto, CancellationToken.None);

        Assert.Equal("Pegue 4 moedas.", resultado.Texto);
        Assert.Equal(0, resultado.Aplicadas);
        Assert.Equal(1, resultado.Descartadas);
    }

    [Fact]
    public async Task RevisarAsync_JsonComTextoEmVolta_AindaEhLido() {
        var chat = new ChatFalso("Claro! Aqui está:\n```json\n{\"correcoes\":[{\"original\":\"Forrá\",\"corrigido\":\"Forró\",\"motivo\":\"leitura\"}]}\n```");

        var resultado = await Revisor(chat).RevisarAsync("# Modo Forrá\n\nEm times.", Contexto, CancellationToken.None);

        Assert.Contains("Modo Forró", resultado.Texto);
        Assert.True(resultado.Completa);
    }

    [Fact]
    public async Task RevisarAsync_RespostaInvalida_MantemOBlocoEMarcaIncompleta() {
        var chat = new ChatFalso("desculpe, não entendi");

        var resultado = await Revisor(chat).RevisarAsync("# MUDOS EXTRAS\n\nJogue.", Contexto, CancellationToken.None);

        Assert.Equal("# MUDOS EXTRAS\n\nJogue.", resultado.Texto);
        Assert.False(resultado.Completa);
        Assert.Null(resultado.Modelo);
    }

    [Fact]
    public async Task RevisarAsync_UmBlocoFalhaEOutroNao_RevisaOQueDeuEMarcaIncompleta() {
        var grande = new string('a', RevisaoMarkdown.TamanhoBloco);
        var markdown = $"{grande}\n\n# MUDOS EXTRAS\n\nJogue.";
        var chat = new ChatFalso(Falha, "{\"correcoes\":[{\"original\":\"MUDOS\",\"corrigido\":\"MODOS\",\"motivo\":\"leitura\"}]}");

        var resultado = await Revisor(chat).RevisarAsync(markdown, Contexto, CancellationToken.None);

        Assert.Equal(2, chat.Recebidos.Count);
        Assert.Contains("MODOS EXTRAS", resultado.Texto);
        Assert.StartsWith(grande, resultado.Texto);
        Assert.False(resultado.Completa);
        // Um bloco revisado ja vale cache: o texto parcial e melhor que o do OCR cru.
        Assert.NotNull(resultado.Modelo);
    }

    [Fact]
    public async Task RevisarAsync_TodosOsBlocosFalham_DevolveOOriginalSemModelo() {
        var chat = new ChatFalso(Falha);

        var resultado = await Revisor(chat).RevisarAsync("# MUDOS EXTRAS\n\nJogue.", Contexto, CancellationToken.None);

        Assert.Equal("# MUDOS EXTRAS\n\nJogue.", resultado.Texto);
        Assert.Null(resultado.Modelo);
        Assert.False(resultado.Completa);
    }

    [Fact]
    public async Task RevisarAsync_ListaVazia_ContaComoRevisadoSemMudarNada() {
        var chat = new ChatFalso("{\"correcoes\":[]}");

        var resultado = await Revisor(chat).RevisarAsync("Texto correto.", Contexto, CancellationToken.None);

        Assert.Equal("Texto correto.", resultado.Texto);
        Assert.True(resultado.Completa);
        Assert.NotNull(resultado.Modelo);
    }
}
```

- [ ] **Step 2: Rodar os testes e ver falhar**

Run: `dotnet test Tests/Tests.csproj --filter FullyQualifiedName~LlmMarkdownRevisorTests`
Expected: erro de compilação (`LlmMarkdownRevisor` não existe).

- [ ] **Step 3: Criar o contrato**

`Src/Application/UseCases/RAG/IRevisorMarkdown.cs`:

```csharp
namespace ProximoTurnoApi.Application.UseCases.RAG;

/// <summary>
/// Resultado da revisão. <see cref="Modelo"/> nulo significa que nenhum bloco chegou a ser
/// revisado: nesse caso o texto devolvido é o original e não vale como cache.
/// </summary>
public sealed record ResultadoRevisao(string Texto, string? Modelo, int Aplicadas, int Descartadas, bool Completa);

public interface IRevisorMarkdown {
    Task<ResultadoRevisao> RevisarAsync(string markdown, ContextoManual contexto, CancellationToken cancellationToken);
}
```

- [ ] **Step 4: Adicionar o modelo do revisor**

Em `Src/Domain/IAModels.cs`, entre `OCR_MODELS` e `EMBEDDING_MODEL`:

```csharp
    // Revisao do markdown extraido: procura erro de leitura do OCR. Modelo pequeno de
    // proposito, chamado uma vez por bloco de ~4000 caracteres, e so texto.
    public const string REVISOR_MODEL = "meta-llama/llama-3.1-8b-instruct";
```

- [ ] **Step 5: Implementar o revisor**

`Src/Infrastructure/RAG/LlmMarkdownRevisor.cs`:

```csharp
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ProximoTurnoApi.Application.UseCases.RAG;
using ProximoTurnoApi.Domain;

namespace ProximoTurnoApi.Infrastructure.RAG;

/// <summary>
/// Passa o manual extraído por um modelo barato atrás de erro de leitura do OCR. O modelo
/// só sugere: o que entra no texto é decidido pelas travas de <see cref="RevisaoMarkdown"/>.
/// </summary>
public class LlmMarkdownRevisor(ILogger<LlmMarkdownRevisor> _logger,
                                [FromKeyedServices(LlmMarkdownRevisor.ChaveChat)] IChatClient _chatClient) : IRevisorMarkdown {

    public const string ChaveChat = "revisor";

    private const string Instrucoes = @"Você revisa trechos de manuais de jogos de tabuleiro transcritos de PDF por OCR.
Aponte apenas erros de leitura ou digitação: letras trocadas, faltando ou sobrando que formam uma palavra errada ou fora de contexto (ex.: ""mudos de jogo"" -> ""modos de jogo"").
Não altere números, nomes próprios, nomes de cartas, peças, modos ou termos inventados pelo jogo, regionalismos, gírias, pontuação nem formatação markdown.
Não reescreva frases nem melhore o estilo. Na dúvida, não corrija.
Jogo: {0}. Manual: {1}.
Responda somente com JSON no formato {{""correcoes"":[{{""original"":""trecho exato como está no texto"",""corrigido"":""trecho corrigido"",""motivo"":""curto""}}]}}.
Se não houver erros, responda {{""correcoes"":[]}}.";

    public async Task<ResultadoRevisao> RevisarAsync(string markdown, ContextoManual contexto, CancellationToken cancellationToken) {
        var blocos = RevisaoMarkdown.DividirEmBlocos(markdown);
        var texto = new StringBuilder(markdown.Length);
        var aplicadas = 0;
        var descartadas = 0;
        var revisados = 0;
        var falhou = false;

        foreach (var bloco in blocos) {
            var correcoes = await PedirCorrecoesAsync(bloco, contexto, cancellationToken);
            if (correcoes is null) {
                // Bloco sem revisao segue como veio: melhor um trecho nao revisado do que
                // perder o manual inteiro por causa de uma chamada que falhou.
                falhou = true;
                texto.Append(bloco);
                continue;
            }

            revisados++;
            var resultado = RevisaoMarkdown.ValidarEAplicar(bloco, correcoes, markdown, contexto);
            texto.Append(resultado.Texto);
            aplicadas += resultado.Aplicadas.Count;
            descartadas += resultado.Descartadas.Count;

            foreach (var correcao in resultado.Aplicadas) {
                _logger.LogInformation("Revisão aplicou '{Original}' -> '{Corrigido}' ({Motivo}).",
                                       correcao.Original, correcao.Corrigido, correcao.Motivo);
            }

            foreach (var (correcao, motivo) in resultado.Descartadas) {
                _logger.LogDebug("Revisão descartou '{Original}' -> '{Corrigido}': {Motivo}.",
                                 correcao.Original, correcao.Corrigido, motivo);
            }
        }

        _logger.LogInformation("Revisão de {Blocos} bloco(s): {Revisados} revisado(s), {Aplicadas} correção(ões) aplicada(s), {Descartadas} descartada(s).",
                               blocos.Count, revisados, aplicadas, descartadas);

        return new ResultadoRevisao(
            texto.ToString(),
            revisados > 0 ? IAModel.REVISOR_MODEL : null,
            aplicadas,
            descartadas,
            revisados > 0 && !falhou);
    }

    /// <summary>
    /// Uma chamada por bloco. Devolve null quando a resposta não pôde ser aproveitada.
    /// </summary>
    private async Task<IReadOnlyList<CorrecaoRevisao>?> PedirCorrecoesAsync(string bloco, ContextoManual contexto, CancellationToken cancellationToken) {
        try {
            var opcoes = new ChatOptions {
                Instructions = string.Format(Instrucoes, contexto.NomeJogo, contexto.TituloManual),
                // Revisao nao se beneficia de diversidade: cada desvio e uma correcao inventada.
                Temperature = 0f,
                ResponseFormat = ChatResponseFormat.Json,
            };

            var resposta = await _chatClient.GetResponseAsync(new ChatMessage(ChatRole.User, bloco), opcoes, cancellationToken);
            return Interpretar(resposta.Text);
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception ex) {
            _logger.LogWarning(ex, "Falha ao revisar um bloco do manual: {Mensagem}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Lê a lista de correções da resposta. Alguns provedores devolvem o JSON com texto em
    /// volta, então o primeiro objeto encontrado também serve.
    /// </summary>
    public static IReadOnlyList<CorrecaoRevisao>? Interpretar(string? resposta) {
        if (string.IsNullOrWhiteSpace(resposta)) {
            return null;
        }

        var inicio = resposta.IndexOf('{');
        var fim = resposta.LastIndexOf('}');
        if (inicio < 0 || fim <= inicio) {
            return null;
        }

        try {
            using var documento = JsonDocument.Parse(resposta[inicio..(fim + 1)]);
            if (!documento.RootElement.TryGetProperty("correcoes", out var lista) || lista.ValueKind != JsonValueKind.Array) {
                return null;
            }

            var correcoes = new List<CorrecaoRevisao>();
            foreach (var item in lista.EnumerateArray()) {
                var original = item.TryGetProperty("original", out var o) ? o.GetString() : null;
                var corrigido = item.TryGetProperty("corrigido", out var c) ? c.GetString() : null;
                var motivo = item.TryGetProperty("motivo", out var m) ? m.GetString() : null;

                if (!string.IsNullOrEmpty(original) && corrigido is not null) {
                    correcoes.Add(new CorrecaoRevisao(original, corrigido, motivo ?? ""));
                }
            }

            return correcoes;
        } catch (JsonException) {
            return null;
        }
    }
}
```

> Se `ChatResponseFormat.Json` não existir nesta versão do Microsoft.Extensions.AI, remova a linha `ResponseFormat = ...`: o prompt já exige JSON e `Interpretar` é tolerante. Não troque por JSON schema.

- [ ] **Step 6: Rodar os testes e ver passar**

Run: `dotnet test Tests/Tests.csproj --filter FullyQualifiedName~LlmMarkdownRevisorTests`
Expected: todos passam.

- [ ] **Step 7: Registrar no DI**

Em `Src/Program.cs`, depois do registro do `IEmbeddingGenerator` (por volta da linha 125):

```csharp
// Cliente do revisor do markdown. Chave propria porque o embedding ja registra um
// cliente OpenRouter, e cada um fala com um modelo diferente.
builder.Services.AddKeyedSingleton<IChatClient>(LlmMarkdownRevisor.ChaveChat, (_, _) => {
    var openRouterApiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
    if (string.IsNullOrWhiteSpace(openRouterApiKey)) {
        throw new InvalidOperationException("OPENROUTER_API_KEY não configurada.");
    }

    var openAiClient = new OpenAIClient(new ApiKeyCredential(openRouterApiKey), new OpenAIClientOptions() {
        Endpoint = new Uri("https://openrouter.ai/api/v1"),

        // Um bloco de 4000 caracteres responde em segundos; nao precisa da folga do OCR.
        NetworkTimeout = TimeSpan.FromMinutes(2),
        RetryPolicy = new ClientRetryPolicy(maxRetries: 2),
    });

    return openAiClient.GetChatClient(IAModel.REVISOR_MODEL).AsIChatClient();
});

builder.Services.AddScoped<IRevisorMarkdown, LlmMarkdownRevisor>();
```

- [ ] **Step 8: Compilar e rodar tudo**

Run: `dotnet build ProximoTurnoApi.slnx` e depois `dotnet test Tests/Tests.csproj`
Expected: build limpo, testes verdes.

- [ ] **Step 9: Commit**

```bash
git add Src/Application/UseCases/RAG/IRevisorMarkdown.cs Src/Infrastructure/RAG/LlmMarkdownRevisor.cs Src/Domain/IAModels.cs Src/Program.cs Tests/Domain/LlmMarkdownRevisorTests.cs
git commit -m "feat: revisor do markdown dos manuais"
```

---

### Task 5: Contexto do manual no chunking

**Files:**
- Modify: `Src/Application/UseCases/RAG/ChunkingExtractor.cs` (interface na linha 18, `ExtrairChunksAsync` na 52, `Dividir` na 63)
- Modify: `Src/Application/Workers/IndexacaoManuaisWorker.cs:139` (ajuste temporário da chamada)
- Test: `Tests/Domain/ChunkingExtractorTests.cs`

**Interfaces:**
- Consumes: `ContextoManual` (Task 3).
- Produces: `IChunkingExtractor.ExtrairChunksAsync(string markdownFilePath, ContextoManual? contexto, CancellationToken)` e `ChunkingExtractor.Dividir(string markdown, ContextoManual? contexto = null)`, com o `Titulo` do chunk no formato `Jogo > Manual > caminho de títulos`.

- [ ] **Step 1: Escrever os testes que falham**

Acrescentar em `Tests/Domain/ChunkingExtractorTests.cs`:

```csharp
    [Fact]
    public void Dividir_ComContexto_PrefixaOJogoEOManualNoTitulo() {
        var markdown = $"""
            # Modo Forrá

            ## Como jogar

            {Corpo("Apenas o jogador da frente da fila move cartas.")}
            """;

        var chunks = ChunkingExtractor.Dividir(markdown, new ContextoManual("Balde de Caranguejo", "Modos extras"));

        // Sem o nome do jogo o vetor nao sabe de que jogo fala: o manual de modos extras
        // do Balde de Caranguejo nao cita o nome do jogo em lugar nenhum do corpo.
        Assert.Equal("Balde de Caranguejo > Modos extras > Modo Forrá > Como jogar", Assert.Single(chunks).Titulo);
    }

    [Fact]
    public void Dividir_ComContexto_SecaoSemTitulo_FicaComOPrefixo() {
        var markdown = Corpo("Neste jogo cooperativo os jogadores organizam os caranguejos.");

        var chunks = ChunkingExtractor.Dividir(markdown, new ContextoManual("Balde de Caranguejo", "Manual"));

        Assert.Equal("Balde de Caranguejo > Manual", Assert.Single(chunks).Titulo);
    }

    [Fact]
    public void Dividir_ContextoSemTituloDeManual_UsaSoONomeDoJogo() {
        var markdown = $"## Preparação\n\n{Corpo("Separe as cartas.")}";

        var chunks = ChunkingExtractor.Dividir(markdown, new ContextoManual("Azul", ""));

        Assert.Equal("Azul > Preparação", Assert.Single(chunks).Titulo);
    }

    [Fact]
    public void Dividir_SemContexto_MantemApenasOCaminhoDeTitulos() {
        var markdown = $"## Preparação\n\n{Corpo("Separe as cartas.")}";

        var chunks = ChunkingExtractor.Dividir(markdown);

        Assert.Equal("Preparação", Assert.Single(chunks).Titulo);
    }
```

- [ ] **Step 2: Rodar os testes e ver falhar**

Run: `dotnet test Tests/Tests.csproj --filter FullyQualifiedName~ChunkingExtractorTests`
Expected: erro de compilação (`Dividir` não aceita `ContextoManual`).

- [ ] **Step 3: Implementar**

Em `Src/Application/UseCases/RAG/ChunkingExtractor.cs`, trocar a interface:

```csharp
public interface IChunkingExtractor {
    Task<IReadOnlyList<ManualChunk>> ExtrairChunksAsync(string markdownFilePath, ContextoManual? contexto, CancellationToken cancellationToken);
}
```

O método de leitura:

```csharp
    public async Task<IReadOnlyList<ManualChunk>> ExtrairChunksAsync(string markdownFilePath, ContextoManual? contexto, CancellationToken cancellationToken) {
        var markdown = await File.ReadAllTextAsync(markdownFilePath, cancellationToken);
        var chunks = Dividir(markdown, contexto);

        _logger.LogInformation("Markdown {MarkdownFilePath} dividido em {Quantidade} chunks.", markdownFilePath, chunks.Count);
        return chunks;
    }
```

E a divisão, com o prefixo:

```csharp
    public static IReadOnlyList<ManualChunk> Dividir(string markdown, ContextoManual? contexto = null) {
        if (string.IsNullOrWhiteSpace(markdown)) {
            return [];
        }

        var prefixo = contexto?.Prefixo ?? "";
        var chunks = new List<ManualChunk>();
        foreach (var secao in Fundir(LerSecoes(markdown.Replace("\r\n", "\n")))) {
            var caminho = Combinar(prefixo, secao.Caminho);
            foreach (var texto in DividirCorpo(secao.Corpo)) {
                chunks.Add(new ManualChunk(chunks.Count, caminho, texto));
            }
        }

        return chunks;
    }

    /// <summary>
    /// Junta o contexto do manual ao caminho de títulos. Um chunk isolado precisa dizer de
    /// que jogo e de que manual ele saiu, senão a busca sem filtro não tem como acertar.
    /// </summary>
    private static string Combinar(string prefixo, string caminho) =>
        string.Join(SeparadorTitulo, new[] { prefixo, caminho }.Where(parte => parte.Length > 0));
```

- [ ] **Step 4: Ajustar a chamada do worker (temporário)**

Em `Src/Application/Workers/IndexacaoManuaisWorker.cs:139`, passar `null` por enquanto. A Task 9 troca o worker inteiro:

```csharp
            var chunks = await chunkingExtractor.ExtrairChunksAsync(markdownFile, null, stoppingToken);
```

- [ ] **Step 5: Rodar os testes e ver passar**

Run: `dotnet test Tests/Tests.csproj`
Expected: tudo verde, incluindo os testes antigos do chunking (que chamam `Dividir` sem contexto).

- [ ] **Step 6: Commit**

```bash
git add Src/Application/UseCases/RAG/ChunkingExtractor.cs Src/Application/Workers/IndexacaoManuaisWorker.cs Tests/Domain/ChunkingExtractorTests.cs
git commit -m "feat: nome do jogo e do manual no texto do chunk"
```

---

### Task 6: Extração devolve o rastro e começa no Gemini

**Files:**
- Modify: `Src/Application/UseCases/RAG/ITextExtractor.cs`
- Modify: `Src/Infrastructure/RAG/PdfTextExtractor.cs` (fim do `ExtractTextAsync`, linhas 111-124)
- Modify: `Src/Domain/IAModels.cs:5`
- Modify: `Src/Application/Workers/IndexacaoManuaisWorker.cs:168` (ajuste temporário)

**Interfaces:**
- Consumes: nada de tasks anteriores.
- Produces: `ResultadoExtracao(string Texto, string Modelo, int Confiabilidade)` e `ITextExtractor.ExtractTextAsync(string filePath, CancellationToken)` devolvendo esse record em vez do caminho do arquivo.

- [ ] **Step 1: Trocar o contrato**

`Src/Application/UseCases/RAG/ITextExtractor.cs`:

```csharp
namespace ProximoTurnoApi.Application.UseCases.RAG;

/// <summary>
/// Texto do manual e o rastro de quem o produziu. O arquivo é gravado por quem chama:
/// o cache é por hash do conteúdo do PDF, e o extrator não conhece essa regra.
/// </summary>
public sealed record ResultadoExtracao(string Texto, string Modelo, int Confiabilidade);

public interface ITextExtractor {
    Task<ResultadoExtracao> ExtractTextAsync(string filePath, CancellationToken cancellationToken);
}
```

- [ ] **Step 2: Ajustar o PdfTextExtractor**

Em `Src/Infrastructure/RAG/PdfTextExtractor.cs`, trocar a assinatura para `public async Task<ResultadoExtracao> ExtractTextAsync(...)` e substituir o fim do método (as linhas que gravavam o `.md`):

```csharp
        if (melhorExtracao.Confiabilidade <= ConfiabilidadeAceitavel) {
            _logger.LogWarning(
                "Todos os modelos ficaram abaixo do aceitável para {PdfFilePath}. Melhor resultado: {Modelo} com confiabilidade {Confiabilidade}.",
                pdfFilePath, melhorModelo, melhorExtracao.Confiabilidade);
        }

        return new ResultadoExtracao(melhorExtracao.Texto, melhorModelo!, melhorExtracao.Confiabilidade);
    }
```

- [ ] **Step 3: Trocar a cascata de modelos**

Em `Src/Domain/IAModels.cs`, substituir a linha do `OCR_MODELS` e o comentário de preços:

```csharp
    //Prices------------------------------------------- $0.75 / $3.75 -------------- $5 / $25 ----
    public static readonly string[] OCR_MODELS = ["google/gemini-3.6-flash", "anthropic/claude-opus-5"];
```

- [ ] **Step 4: Ajustar a chamada do worker (temporário)**

Em `Src/Application/Workers/IndexacaoManuaisWorker.cs`, dentro de `ExtrairMarkdownAsync`, trocar as duas linhas que chamavam o extrator:

```csharp
            var extracao = await textExtractor.ExtractTextAsync(caminhoArquivo, stoppingToken);
            await File.WriteAllTextAsync(markdownFile, extracao.Texto, stoppingToken);
```

- [ ] **Step 5: Compilar e rodar os testes**

Run: `dotnet build ProximoTurnoApi.slnx` e depois `dotnet test Tests/Tests.csproj`
Expected: verde. `PdfTextExtractorTests` exercita só `ProximoModelo` e `Interpretar`, que não mudaram; ele roda para confirmar que a troca não quebrou nada.

- [ ] **Step 6: Commit**

```bash
git add Src/Application/UseCases/RAG/ITextExtractor.cs Src/Infrastructure/RAG/PdfTextExtractor.cs Src/Domain/IAModels.cs Src/Application/Workers/IndexacaoManuaisWorker.cs
git commit -m "feat: extracao devolve modelo e nota, e cascata comeca no gemini"
```

---

### Task 7: Remover e listar vetores no Qdrant

**Files:**
- Modify: `Src/Application/UseCases/RAG/IManualVectorStore.cs`
- Modify: `Src/Infrastructure/RAG/QdrantManualVectorStore.cs` (remover `PortaGrpc` na linha 30 e acrescentar os dois métodos)

**Interfaces:**
- Consumes: nada de tasks anteriores.
- Produces: `IManualVectorStore.RemoverAsync(int idJogoLink, CancellationToken)` e `IManualVectorStore.ListarIdsLinksAsync(CancellationToken)` devolvendo `IReadOnlyList<int>`.

- [ ] **Step 1: Acrescentar ao contrato**

Em `Src/Application/UseCases/RAG/IManualVectorStore.cs`, dentro da interface:

```csharp
    /// <summary>
    /// Apaga os vetores de um link. Chamado antes de reindexar e sempre que o link deixa
    /// de poder responder buscas: apagado, virado vídeo ou de um jogo desativado.
    /// </summary>
    Task RemoverAsync(int idJogoLink, CancellationToken cancellationToken);

    /// <summary>
    /// Links que têm vetores gravados. É o lado do Qdrant da reconciliação: o que está
    /// aqui e não deveria estar vira job de remoção.
    /// </summary>
    Task<IReadOnlyList<int>> ListarIdsLinksAsync(CancellationToken cancellationToken);
```

- [ ] **Step 2: Implementar**

Em `Src/Infrastructure/RAG/QdrantManualVectorStore.cs`, apagar a constante `PortaGrpc` (com o comentário dela) e acrescentar, depois de `SalvarAsync`:

```csharp
    // O padrao do facet e 10: sem um teto alto a reconciliacao enxergaria so uma fatia
    // da colecao e deixaria orfao para tras.
    private const ulong LimiteFacet = 10_000;

    public async Task RemoverAsync(int idJogoLink, CancellationToken cancellationToken) {
        if (!await _client.CollectionExistsAsync(Colecao, cancellationToken)) {
            return;
        }

        await _client.DeleteAsync(Colecao, MatchInt("IdJogoLink", idJogoLink), cancellationToken: cancellationToken);
        _logger.LogInformation("Vetores do link {IdJogoLink} removidos da coleção {Colecao}.", idJogoLink, Colecao);
    }

    public async Task<IReadOnlyList<int>> ListarIdsLinksAsync(CancellationToken cancellationToken) {
        if (!await _client.CollectionExistsAsync(Colecao, cancellationToken)) {
            return [];
        }

        var facetas = await _client.FacetAsync(Colecao, "IdJogoLink", limit: LimiteFacet, exact: true, cancellationToken: cancellationToken);

        return [.. facetas.Select(faceta => (int)faceta.Value.IntegerValue)];
    }
```

> `FacetAsync` existe no Qdrant.Client 1.19 e depende do índice de payload em `IdJogoLink`, que `GarantirColecaoAsync` já cria. Se o tipo devolvido for diferente do esperado, ajuste só o mapeamento: o que interessa de cada hit é o valor inteiro (`FacetValue.IntegerValue`).

- [ ] **Step 3: Compilar e rodar os testes**

Run: `dotnet build ProximoTurnoApi.slnx` e depois `dotnet test Tests/Tests.csproj`
Expected: verde. `QdrantManualVectorStoreTests` testa `Ponto` e `NomeColecao`, que não mudaram; os dois métodos novos falam com o Qdrant e são verificados na Task 11.

- [ ] **Step 4: Commit**

```bash
git add Src/Application/UseCases/RAG/IManualVectorStore.cs Src/Infrastructure/RAG/QdrantManualVectorStore.cs
git commit -m "feat: remover e listar vetores por link no qdrant"
```

---

### Task 8: Use case `SincronizarManual`

**Files:**
- Create: `Src/Application/UseCases/RAG/SincronizarManual.cs`
- Create: `Tests/Fakes/IndexacaoManualFakes.cs`
- Test: `Tests/Domain/SincronizarManualTests.cs`
- Modify: `Src/Program.cs`

**Interfaces:**
- Consumes: `IIndexacaoManualRepository`, `EstadoLinkManual` (Task 2); `ContextoManual` (Task 3); `IRevisorMarkdown`, `ResultadoRevisao` (Task 4); `IChunkingExtractor.ExtrairChunksAsync(string, ContextoManual?, CancellationToken)` (Task 5); `ITextExtractor`, `ResultadoExtracao` (Task 6); `IManualVectorStore.RemoverAsync/ListarIdsLinksAsync/SalvarAsync` (Task 7); `IEmbeddingExtractor.GerarEmbeddingsAsync`; `UploadManual.GetUploadFolder(IWebHostEnvironment)`.
- Produces: `SincronizarManual.ExecuteAsync(ManualJob job, CancellationToken)` e `SincronizarManual.MaxTentativas = 3`.

> Nesta task o `ManualJob` ainda tem `Url` (o record muda na Task 9). Os testes criam o job com `new ManualJob(1, 99, "")` e a Task 9 remove esse terceiro argumento.

- [ ] **Step 1: Criar os fakes**

`Tests/Fakes/IndexacaoManualFakes.cs`:

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using ProximoTurnoApi.Application.UseCases.RAG;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Tests.Fakes;

/// <summary>Ambiente web apontando para uma pasta temporária que faz as vezes de wwwroot.</summary>
public sealed class FakeWebHostEnvironment : IWebHostEnvironment, IDisposable {

    public FakeWebHostEnvironment() {
        WebRootPath = Path.Combine(Path.GetTempPath(), "proximoturno-testes-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Uploads);
    }

    public string WebRootPath { get; set; }
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string ApplicationName { get; set; } = "Tests";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public string ContentRootPath { get; set; } = ".";
    public string EnvironmentName { get; set; } = "Development";

    public string Uploads => Path.Combine(WebRootPath, "uploads");

    /// <summary>Cria um "PDF" com o conteúdo pedido: o que importa nos testes é o hash dele.</summary>
    public string CriarPdf(string nome, string conteudo = "pdf") {
        var caminho = Path.Combine(Uploads, nome);
        File.WriteAllText(caminho, conteudo);
        return caminho;
    }

    public string[] Markdowns() => [.. Directory.GetFiles(Uploads, "*.md").Select(Path.GetFileName)!];

    public void Dispose() {
        try {
            Directory.Delete(WebRootPath, recursive: true);
        } catch (IOException) {
            // Pasta temporaria presa por outro processo nao e problema do teste.
        }
    }
}

public sealed class FakeIndexacaoManualRepository : IIndexacaoManualRepository {

    private int _proximoId = 1;

    public List<EstadoLinkManual> Estados { get; } = [];
    public List<JogoLinkIndexacao> Linhas { get; } = [];
    public List<ManualJob> Elegiveis { get; } = [];

    /// <summary>Monta o estado de um link, junto com a linha de indexação quando já existe.</summary>
    public EstadoLinkManual Adicionar(int idJogoLink,
                                      int idJogo = 99,
                                      string url = "https://site/uploads/manual.pdf",
                                      TipoLink tipo = TipoLink.Regra,
                                      bool jogoAtivo = true,
                                      JogoLinkIndexacao? indexacao = null,
                                      string nomeJogo = "Balde de Caranguejo",
                                      string tituloLink = "Manual") {
        if (indexacao is not null) {
            indexacao.Id = _proximoId++;
            indexacao.IdJogoLink = idJogoLink;
            Linhas.Add(indexacao);
        }

        var estado = new EstadoLinkManual(idJogoLink, idJogo, tipo, url, tituloLink, nomeJogo, jogoAtivo, indexacao);
        Estados.Add(estado);
        return estado;
    }

    public JogoLinkIndexacao? Linha(int idJogoLink) => Linhas.FirstOrDefault(l => l.IdJogoLink == idJogoLink);

    public Task<List<ManualJob>> GetElegiveisAsync(int maxTentativas) => Task.FromResult(Elegiveis.ToList());

    public Task<EstadoLinkManual?> GetEstadoAsync(int idJogoLink) =>
        Task.FromResult(Estados.FirstOrDefault(e => e.IdJogoLink == idJogoLink));

    public Task<bool> ExisteIndexadoComHashAsync(int idJogo, string hash, int idJogoLinkExceto) =>
        Task.FromResult(Linhas.Any(l => l.Status == StatusIndexacao.Indexado &&
                                        l.HashPdf == hash &&
                                        l.IdJogoLink != idJogoLinkExceto &&
                                        JogoDoLink(l.IdJogoLink) == idJogo));

    public Task<List<int>> GetIdsDuplicadosAsync(int idJogo, int idJogoLinkExceto) =>
        Task.FromResult(Linhas.Where(l => l.Status == StatusIndexacao.Duplicado &&
                                          l.IdJogoLink != idJogoLinkExceto &&
                                          JogoDoLink(l.IdJogoLink) == idJogo)
                              .Select(l => l.IdJogoLink)
                              .ToList());

    public Task<JogoLinkIndexacao?> GetMetadadosPorHashAsync(string hash) =>
        Task.FromResult(Linhas.FirstOrDefault(l => l.HashPdf == hash && l.ModeloExtracao != null));

    public Task<HashSet<int>> GetIdsComVetoresEsperadosAsync() =>
        Task.FromResult(Linhas.Where(l => l.Status == StatusIndexacao.Indexado).Select(l => l.IdJogoLink).ToHashSet());

    public Task<List<int>> GetIdsLinksAsync(int idJogo) =>
        Task.FromResult(Estados.Where(e => e.IdJogo == idJogo).Select(e => e.IdJogoLink).ToList());

    public Task SalvarAsync(JogoLinkIndexacao indexacao) {
        if (indexacao.Id == 0) {
            indexacao.Id = _proximoId++;
            Linhas.Add(indexacao);
        }

        indexacao.DataAtualizacao = DateTime.Now;
        return Task.CompletedTask;
    }

    private int JogoDoLink(int idJogoLink) => Estados.FirstOrDefault(e => e.IdJogoLink == idJogoLink)?.IdJogo ?? 0;

    public Task SaveChangesAsync() => Task.CompletedTask;
    public Task StartTransactionAsync() => Task.CompletedTask;
    public Task CommitTransactionAsync() => Task.CompletedTask;
    public Task RollbackTransactionAsync() => Task.CompletedTask;
}

public sealed class FakeManualVectorStore : IManualVectorStore {

    public List<int> Removidos { get; } = [];
    public Dictionary<int, IReadOnlyList<ChunkEmbedding>> Gravados { get; } = [];
    public List<int> IdsComVetores { get; } = [];
    public Exception? ErroAoSalvar { get; set; }

    public Task SalvarAsync(int idJogo, int idJogoLink, IReadOnlyList<ChunkEmbedding> embeddings, CancellationToken cancellationToken) {
        if (ErroAoSalvar is not null) {
            throw ErroAoSalvar;
        }

        Gravados[idJogoLink] = embeddings;
        return Task.CompletedTask;
    }

    public Task RemoverAsync(int idJogoLink, CancellationToken cancellationToken) {
        Removidos.Add(idJogoLink);
        Gravados.Remove(idJogoLink);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<int>> ListarIdsLinksAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<int>>(IdsComVetores);
}

public sealed class FakeTextExtractor : ITextExtractor {

    public int Chamadas { get; private set; }
    public string Texto { get; set; } = "# Manual\n\n" + string.Join(" ", Enumerable.Repeat("Regra do jogo.", 40));
    public Exception? Erro { get; set; }

    public Task<ResultadoExtracao> ExtractTextAsync(string filePath, CancellationToken cancellationToken) {
        Chamadas++;
        if (Erro is not null) {
            throw Erro;
        }

        return Task.FromResult(new ResultadoExtracao(Texto, "modelo/falso", 91));
    }
}

public sealed class FakeRevisorMarkdown : IRevisorMarkdown {

    public int Chamadas { get; private set; }
    public string? Modelo { get; set; } = "revisor/falso";
    public bool Completa { get; set; } = true;
    public Func<string, string>? Transformar { get; set; }

    public Task<ResultadoRevisao> RevisarAsync(string markdown, ContextoManual contexto, CancellationToken cancellationToken) {
        Chamadas++;
        var texto = Transformar is null ? markdown : Transformar(markdown);
        return Task.FromResult(new ResultadoRevisao(texto, Modelo, Aplicadas: 1, Descartadas: 0, Completa));
    }
}

public sealed class FakeEmbeddingExtractor : IEmbeddingExtractor {

    public List<ManualChunk> Recebidos { get; } = [];

    public Task<IReadOnlyList<ChunkEmbedding>> GerarEmbeddingsAsync(IReadOnlyList<ManualChunk> chunks, CancellationToken cancellationToken) {
        Recebidos.AddRange(chunks);
        return Task.FromResult<IReadOnlyList<ChunkEmbedding>>([.. chunks.Select(c => new ChunkEmbedding(c, new float[] { c.Ordem }))]);
    }
}
```

- [ ] **Step 2: Escrever os testes que falham**

`Tests/Domain/SincronizarManualTests.cs`:

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using ProximoTurnoApi.Application.UseCases.RAG;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Tests.Fakes;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class SincronizarManualTests : IDisposable {

    private readonly FakeWebHostEnvironment _env = new();
    private readonly FakeIndexacaoManualRepository _repo = new();
    private readonly FakeManualVectorStore _vetores = new();
    private readonly FakeTextExtractor _extrator = new();
    private readonly FakeRevisorMarkdown _revisor = new();
    private readonly FakeEmbeddingExtractor _embedding = new();
    private readonly FakeManualQueue _fila = new();

    private const string NomeArquivo = "manual.pdf";
    private const string Url = "https://site/uploads/" + NomeArquivo;

    public void Dispose() => _env.Dispose();

    private SincronizarManual Montar() =>
        new(_env, NullLogger<SincronizarManual>.Instance, _repo, _vetores, _extrator, _revisor,
            new ChunkingExtractor(NullLogger<ChunkingExtractor>.Instance), _embedding, _fila);

    private Task Executar(int idJogoLink = 1, int idJogo = 99) =>
        Montar().ExecuteAsync(new ManualJob(idJogoLink, idJogo, ""), CancellationToken.None);

    [Fact]
    public async Task LinkApagado_RemoveOsVetores() {
        _vetores.IdsComVetores.Add(1);

        await Executar();

        Assert.Contains(1, _vetores.Removidos);
    }

    [Fact]
    public async Task LinkApagado_ReenfileiraOsDuplicadosDoJogo() {
        // O duplicado so nao foi indexado porque outro link do jogo tinha o mesmo PDF.
        _repo.Adicionar(2, indexacao: new JogoLinkIndexacao { Url = Url, Status = StatusIndexacao.Duplicado, HashPdf = "abc" });

        await Executar(idJogoLink: 1, idJogo: 99);

        Assert.Equal(2, Assert.Single(_fila.Enfileirados).IdJogoLink);
    }

    [Fact]
    public async Task LinkDeVideo_RemoveOsVetoresEMarcaRemovido() {
        _repo.Adicionar(1, tipo: TipoLink.Video, indexacao: new JogoLinkIndexacao { Url = Url, Status = StatusIndexacao.Indexado });

        await Executar();

        Assert.Contains(1, _vetores.Removidos);
        Assert.Equal(StatusIndexacao.Removido, _repo.Linha(1)!.Status);
    }

    [Fact]
    public async Task JogoDesativado_RemoveOsVetoresEMarcaRemovido() {
        _repo.Adicionar(1, jogoAtivo: false, indexacao: new JogoLinkIndexacao { Url = Url, Status = StatusIndexacao.Indexado });

        await Executar();

        Assert.Contains(1, _vetores.Removidos);
        Assert.Equal(StatusIndexacao.Removido, _repo.Linha(1)!.Status);
    }

    [Fact]
    public async Task JaIndexadoNaMesmaUrl_NaoFazNada() {
        _env.CriarPdf(NomeArquivo);
        _repo.Adicionar(1, indexacao: new JogoLinkIndexacao { Url = Url, Status = StatusIndexacao.Indexado });

        await Executar();

        Assert.Equal(0, _extrator.Chamadas);
        Assert.Empty(_vetores.Removidos);
    }

    [Fact]
    public async Task LinkNovo_ExtraiRevisaEGrava() {
        _env.CriarPdf(NomeArquivo);
        _repo.Adicionar(1);

        await Executar();

        var linha = _repo.Linha(1)!;
        Assert.Equal(StatusIndexacao.Indexado, linha.Status);
        Assert.Equal("modelo/falso", linha.ModeloExtracao);
        Assert.Equal(91, linha.ConfiabilidadeExtracao);
        Assert.Equal("revisor/falso", linha.ModeloRevisao);
        Assert.True(linha.RevisaoCompleta);
        Assert.NotNull(linha.DataIndexacao);
        Assert.True(linha.QuantidadeChunks > 0);
        Assert.True(_vetores.Gravados.ContainsKey(1));
        Assert.Equal(2, _env.Markdowns().Length); // {hash}.raw.md e {hash}.md
    }

    [Fact]
    public async Task LinkNovo_ChunksLevamOJogoEOManualNoTitulo() {
        _env.CriarPdf(NomeArquivo);
        _repo.Adicionar(1, nomeJogo: "Balde de Caranguejo", tituloLink: "Modos extras");

        await Executar();

        Assert.All(_embedding.Recebidos, chunk => Assert.StartsWith("Balde de Caranguejo > Modos extras", chunk.Titulo));
    }

    [Fact]
    public async Task UrlTrocada_RemoveOsVetoresAntigosEReindexa() {
        _env.CriarPdf(NomeArquivo);
        _repo.Adicionar(1, url: Url, indexacao: new JogoLinkIndexacao {
            Url = "https://site/uploads/antigo.pdf",
            Status = StatusIndexacao.Indexado,
            Tentativas = 2,
            HashPdf = "hash-antigo"
        });

        await Executar();

        // Os vetores antigos saem antes da extracao: se a nova falhar, o manual trocado
        // nao pode continuar respondendo com a versao velha.
        Assert.Contains(1, _vetores.Removidos);
        var linha = _repo.Linha(1)!;
        Assert.Equal(StatusIndexacao.Indexado, linha.Status);
        Assert.Equal(Url, linha.Url);
        Assert.Equal(0, linha.Tentativas);
    }

    [Fact]
    public async Task MesmoPdfEmOutroLinkDoMesmoJogo_MarcaDuplicado() {
        _env.CriarPdf(NomeArquivo, "conteudo igual");
        _env.CriarPdf("copia.pdf", "conteudo igual");
        _repo.Adicionar(1, url: Url);
        _repo.Adicionar(2, url: "https://site/uploads/copia.pdf");

        await Executar(idJogoLink: 1);
        await Executar(idJogoLink: 2);

        Assert.Equal(StatusIndexacao.Indexado, _repo.Linha(1)!.Status);
        Assert.Equal(StatusIndexacao.Duplicado, _repo.Linha(2)!.Status);
        Assert.False(_vetores.Gravados.ContainsKey(2));
        // O segundo link nao paga extracao nem revisao de novo.
        Assert.Equal(1, _extrator.Chamadas);
        Assert.Equal(1, _revisor.Chamadas);
    }

    [Fact]
    public async Task MesmoPdfEmJogoDiferente_IndexaNormalmente() {
        _env.CriarPdf(NomeArquivo, "conteudo igual");
        _env.CriarPdf("copia.pdf", "conteudo igual");
        _repo.Adicionar(1, idJogo: 99, url: Url);
        _repo.Adicionar(2, idJogo: 100, url: "https://site/uploads/copia.pdf");

        await Executar(idJogoLink: 1, idJogo: 99);
        await Executar(idJogoLink: 2, idJogo: 100);

        Assert.Equal(StatusIndexacao.Indexado, _repo.Linha(2)!.Status);
        Assert.True(_vetores.Gravados.ContainsKey(2));
    }

    [Fact]
    public async Task ArquivoInexistente_MarcaFalhouSemChamarModelo() {
        _repo.Adicionar(1);

        await Executar();

        var linha = _repo.Linha(1)!;
        Assert.Equal(StatusIndexacao.Falhou, linha.Status);
        Assert.Equal(1, linha.Tentativas);
        Assert.Contains("manual.pdf", linha.UltimoErro);
        Assert.Equal(0, _extrator.Chamadas);
    }

    [Fact]
    public async Task FalhaNaExtracao_IncrementaTentativas() {
        _env.CriarPdf(NomeArquivo);
        _extrator.Erro = new InvalidOperationException("nenhum modelo conseguiu extrair");
        _repo.Adicionar(1, indexacao: new JogoLinkIndexacao { Url = Url, Status = StatusIndexacao.Falhou, Tentativas = 1 });

        await Executar();

        Assert.Equal(2, _repo.Linha(1)!.Tentativas);
    }

    [Fact]
    public async Task TentativasEsgotadas_NaoTentaDeNovoEGaranteQueNaoSobraVetor() {
        _env.CriarPdf(NomeArquivo);
        _repo.Adicionar(1, indexacao: new JogoLinkIndexacao {
            Url = Url,
            Status = StatusIndexacao.Falhou,
            Tentativas = SincronizarManual.MaxTentativas
        });

        await Executar();

        Assert.Equal(0, _extrator.Chamadas);
        Assert.Contains(1, _vetores.Removidos);
    }

    [Fact]
    public async Task Cancelamento_RelancaEMantemProcessando() {
        _env.CriarPdf(NomeArquivo);
        _extrator.Erro = new OperationCanceledException();
        _repo.Adicionar(1);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(Executar);

        Assert.Equal(StatusIndexacao.Processando, _repo.Linha(1)!.Status);
    }

    [Fact]
    public async Task MarkdownEmCache_NaoChamaExtratorNemRevisor() {
        _env.CriarPdf(NomeArquivo);
        _repo.Adicionar(1);

        await Executar();
        _repo.Linha(1)!.Status = StatusIndexacao.Falhou; // força uma segunda passada
        await Executar();

        Assert.Equal(1, _extrator.Chamadas);
        Assert.Equal(1, _revisor.Chamadas);
    }

    [Fact]
    public async Task MarkdownLegadoPorGuid_EhReaproveitado() {
        _env.CriarPdf(NomeArquivo);
        File.WriteAllText(Path.Combine(_env.Uploads, "manual.md"), "# Manual antigo\n\n" + string.Join(" ", Enumerable.Repeat("Regra.", 40)));
        _repo.Adicionar(1);

        await Executar();

        Assert.Equal(0, _extrator.Chamadas);
        Assert.False(File.Exists(Path.Combine(_env.Uploads, "manual.md")));
        Assert.Contains(_embedding.Recebidos, chunk => chunk.Texto.Contains("Regra."));
    }

    [Fact]
    public async Task RevisaoParcial_GravaOMdEMarcaIncompleta() {
        _env.CriarPdf(NomeArquivo);
        _revisor.Completa = false;
        _repo.Adicionar(1);

        await Executar();

        Assert.False(_repo.Linha(1)!.RevisaoCompleta);
        Assert.Equal(2, _env.Markdowns().Length);
    }

    [Fact]
    public async Task RevisaoFalhaInteira_IndexaOTextoDoOcrSemGravarMd() {
        _env.CriarPdf(NomeArquivo);
        _revisor.Modelo = null;
        _revisor.Completa = false;
        _repo.Adicionar(1);

        await Executar();

        var linha = _repo.Linha(1)!;
        Assert.Equal(StatusIndexacao.Indexado, linha.Status);
        Assert.Null(linha.ModeloRevisao);
        Assert.Single(_env.Markdowns()); // só o raw
    }

    [Fact]
    public async Task ReaproveitandoCacheDeOutroLink_HerdaOsMetadados() {
        _env.CriarPdf(NomeArquivo, "conteudo igual");
        _env.CriarPdf("copia.pdf", "conteudo igual");
        _repo.Adicionar(1, idJogo: 99, url: Url);
        _repo.Adicionar(2, idJogo: 100, url: "https://site/uploads/copia.pdf");

        await Executar(idJogoLink: 1, idJogo: 99);
        await Executar(idJogoLink: 2, idJogo: 100);

        Assert.Equal("modelo/falso", _repo.Linha(2)!.ModeloExtracao);
        Assert.Equal(91, _repo.Linha(2)!.ConfiabilidadeExtracao);
    }
}
```

- [ ] **Step 3: Rodar os testes e ver falhar**

Run: `dotnet test Tests/Tests.csproj --filter FullyQualifiedName~SincronizarManualTests`
Expected: erro de compilação (`SincronizarManual` não existe).

- [ ] **Step 4: Implementar o use case**

`Src/Application/UseCases/RAG/SincronizarManual.cs`:

```csharp
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases.RAG;

/// <summary>
/// Decide o que fazer com um link de manual e executa. O job só traz ids: o que vale é o
/// estado do banco na hora de rodar, então job repetido ou velho na fila não estraga nada.
/// </summary>
public class SincronizarManual(IWebHostEnvironment _env,
                               ILogger<SincronizarManual> _logger,
                               IIndexacaoManualRepository _repository,
                               IManualVectorStore _vectorStore,
                               ITextExtractor _textExtractor,
                               IRevisorMarkdown _revisor,
                               IChunkingExtractor _chunkingExtractor,
                               IEmbeddingExtractor _embeddingExtractor,
                               IManualQueue _queue) : UseCaseBasico {

    // Falhar tres vezes na mesma URL e sinal de problema no arquivo, nao de instabilidade.
    // Sem esse teto, cada deploy repetiria a cascata inteira de modelos pagos.
    public const int MaxTentativas = 3;

    private const int TamanhoMaximoDoErro = 1000;

    public async Task ExecuteAsync(ManualJob job, CancellationToken cancellationToken) {
        var estado = await _repository.GetEstadoAsync(job.IdJogoLink);

        if (estado is null) {
            await _vectorStore.RemoverAsync(job.IdJogoLink, cancellationToken);
            _logger.LogInformation("Link {IdJogoLink} não existe mais: vetores removidos.", job.IdJogoLink);
            await ReenfileirarDuplicadosAsync(job.IdJogo, job.IdJogoLink);
            return;
        }

        if (estado.Tipo != TipoLink.Regra || string.IsNullOrWhiteSpace(estado.Url) || !estado.JogoAtivo) {
            await RemoverAsync(estado, cancellationToken);
            return;
        }

        var indexacao = estado.Indexacao;
        if (indexacao is not null && indexacao.Url == estado.Url) {
            if (indexacao.Status == StatusIndexacao.Indexado) {
                _logger.LogDebug("Link {IdJogoLink} já indexado nesta URL. Nada a fazer.", estado.IdJogoLink);
                return;
            }

            if (indexacao.Status == StatusIndexacao.Falhou && indexacao.Tentativas >= MaxTentativas) {
                // Manual que nao conseguimos ler nao pode ficar com sobra de vetor antigo
                // respondendo busca em nome dele.
                await _vectorStore.RemoverAsync(estado.IdJogoLink, cancellationToken);
                _logger.LogWarning("Link {IdJogoLink} esgotou as {MaxTentativas} tentativas nesta URL. Só volta se o PDF mudar.",
                                   estado.IdJogoLink, MaxTentativas);
                return;
            }
        }

        await ProcessarAsync(estado, cancellationToken);
    }

    private async Task ProcessarAsync(EstadoLinkManual estado, CancellationToken cancellationToken) {
        var indexacao = estado.Indexacao ?? new JogoLinkIndexacao { IdJogoLink = estado.IdJogoLink, Url = estado.Url };
        var estavaIndexado = indexacao.Status == StatusIndexacao.Indexado && indexacao.Id != 0;

        if (indexacao.Url != estado.Url) {
            indexacao.Url = estado.Url;
            indexacao.Tentativas = 0;
        }

        indexacao.Status = StatusIndexacao.Processando;
        await _repository.SalvarAsync(indexacao);

        // O link fica sem vetores enquanto roda: manual trocado nao pode seguir respondendo
        // com a versao antiga se a nova falhar no meio.
        await _vectorStore.RemoverAsync(estado.IdJogoLink, cancellationToken);

        try {
            await IndexarAsync(estado, indexacao, cancellationToken);
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception ex) {
            indexacao.Status = StatusIndexacao.Falhou;
            indexacao.Tentativas++;
            indexacao.UltimoErro = Truncar(ex.Message);
            await _repository.SalvarAsync(indexacao);

            _logger.LogError(ex, "Falha ao indexar o manual do link {IdJogoLink} do jogo {IdJogo} (tentativa {Tentativas}).",
                             estado.IdJogoLink, estado.IdJogo, indexacao.Tentativas);
        }

        if (estavaIndexado) {
            await ReenfileirarDuplicadosAsync(estado.IdJogo, estado.IdJogoLink);
        }
    }

    private async Task IndexarAsync(EstadoLinkManual estado, JogoLinkIndexacao indexacao, CancellationToken cancellationToken) {
        var caminhoPdf = Path.Combine(UploadManual.GetUploadFolder(_env), Path.GetFileName(estado.Url));
        if (!File.Exists(caminhoPdf)) {
            throw new FileNotFoundException($"Arquivo {Path.GetFileName(estado.Url)} não encontrado na pasta de uploads.");
        }

        indexacao.HashPdf = await CalcularHashAsync(caminhoPdf, cancellationToken);

        if (await _repository.ExisteIndexadoComHashAsync(estado.IdJogo, indexacao.HashPdf, estado.IdJogoLink)) {
            indexacao.Status = StatusIndexacao.Duplicado;
            await _repository.SalvarAsync(indexacao);

            _logger.LogInformation("Link {IdJogoLink} aponta para um PDF já indexado no jogo {IdJogo}. Marcado como duplicado.",
                                   estado.IdJogoLink, estado.IdJogo);
            return;
        }

        var contexto = new ContextoManual(estado.NomeJogo, estado.TituloLink);
        var caminhoRaw = await ExtrairAsync(caminhoPdf, indexacao, cancellationToken);
        var caminhoFinal = await RevisarAsync(caminhoRaw, contexto, indexacao, cancellationToken);

        var chunks = await _chunkingExtractor.ExtrairChunksAsync(caminhoFinal, contexto, cancellationToken);
        if (chunks.Count == 0) {
            throw new InvalidOperationException("Nenhum chunk gerado a partir do markdown do manual.");
        }

        var embeddings = await _embeddingExtractor.GerarEmbeddingsAsync(chunks, cancellationToken);
        await _vectorStore.SalvarAsync(estado.IdJogo, estado.IdJogoLink, embeddings, cancellationToken);

        indexacao.Status = StatusIndexacao.Indexado;
        indexacao.Tentativas = 0;
        indexacao.UltimoErro = null;
        indexacao.QuantidadeChunks = chunks.Count;
        indexacao.DataIndexacao = DateTime.Now;
        await _repository.SalvarAsync(indexacao);

        _logger.LogInformation("Manual do link {IdJogoLink} do jogo {IdJogo} indexado com {Quantidade} chunks.",
                               estado.IdJogoLink, estado.IdJogo, chunks.Count);
    }

    /// <summary>
    /// Garante o `{hash}.raw.md`. O cache é por conteúdo: o mesmo PDF em dois links, ou
    /// reenviado com outro nome, não é extraído (nem pago) duas vezes.
    /// </summary>
    private async Task<string> ExtrairAsync(string caminhoPdf, JogoLinkIndexacao indexacao, CancellationToken cancellationToken) {
        var pasta = UploadManual.GetUploadFolder(_env);
        var caminhoRaw = Path.Combine(pasta, $"{indexacao.HashPdf}.raw.md");

        if (File.Exists(caminhoRaw)) {
            await CopiarMetadadosAsync(indexacao);
            return caminhoRaw;
        }

        // Formato antigo, nomeado pelo GUID do PDF. Aproveitado uma vez para nao repagar
        // a extracao dos manuais que ja estavam no servidor.
        var legado = Path.ChangeExtension(caminhoPdf, ".md");
        if (File.Exists(legado)) {
            File.Move(legado, caminhoRaw);
            _logger.LogInformation("Markdown legado {Legado} reaproveitado como {Raw}.", Path.GetFileName(legado), Path.GetFileName(caminhoRaw));
            await CopiarMetadadosAsync(indexacao);
            return caminhoRaw;
        }

        var extracao = await _textExtractor.ExtractTextAsync(caminhoPdf, cancellationToken);
        await File.WriteAllTextAsync(caminhoRaw, extracao.Texto, cancellationToken);

        indexacao.ModeloExtracao = extracao.Modelo;
        indexacao.ConfiabilidadeExtracao = (short)extracao.Confiabilidade;
        return caminhoRaw;
    }

    /// <summary>
    /// Garante o `{hash}.md`. Falha total do revisor não impede a indexação: o texto do OCR
    /// ainda vale, e a revisão é tentada de novo na próxima sincronização do link.
    /// </summary>
    private async Task<string> RevisarAsync(string caminhoRaw, ContextoManual contexto, JogoLinkIndexacao indexacao, CancellationToken cancellationToken) {
        var caminhoRevisado = Path.Combine(UploadManual.GetUploadFolder(_env), $"{indexacao.HashPdf}.md");
        if (File.Exists(caminhoRevisado)) {
            return caminhoRevisado;
        }

        var markdown = await File.ReadAllTextAsync(caminhoRaw, cancellationToken);
        var revisao = await _revisor.RevisarAsync(markdown, contexto, cancellationToken);

        indexacao.ModeloRevisao = revisao.Modelo;
        indexacao.CorrecoesAplicadas = revisao.Aplicadas;
        indexacao.CorrecoesDescartadas = revisao.Descartadas;
        indexacao.RevisaoCompleta = revisao.Completa;

        if (revisao.Modelo is null) {
            _logger.LogWarning("Revisão do manual do link {IdJogoLink} falhou inteira. Indexando o texto do OCR.", indexacao.IdJogoLink);
            return caminhoRaw;
        }

        await File.WriteAllTextAsync(caminhoRevisado, revisao.Texto, cancellationToken);
        return caminhoRevisado;
    }

    private async Task RemoverAsync(EstadoLinkManual estado, CancellationToken cancellationToken) {
        await _vectorStore.RemoverAsync(estado.IdJogoLink, cancellationToken);

        var indexacao = estado.Indexacao;
        if (indexacao is null) {
            return;
        }

        var estavaIndexado = indexacao.Status == StatusIndexacao.Indexado;
        indexacao.Status = StatusIndexacao.Removido;
        await _repository.SalvarAsync(indexacao);

        _logger.LogInformation("Link {IdJogoLink} não deve ter vetores (tipo {Tipo}, jogo ativo: {JogoAtivo}). Marcado como removido.",
                               estado.IdJogoLink, estado.Tipo, estado.JogoAtivo);

        if (estavaIndexado) {
            await ReenfileirarDuplicadosAsync(estado.IdJogo, estado.IdJogoLink);
        }
    }

    /// <summary>
    /// Metadados são do conteúdo, não do link: quem reaproveita o cache de um hash herda o
    /// modelo, a nota e o resultado da revisão de quem pagou a extração.
    /// </summary>
    private async Task CopiarMetadadosAsync(JogoLinkIndexacao indexacao) {
        var origem = await _repository.GetMetadadosPorHashAsync(indexacao.HashPdf!);
        if (origem is null || origem.IdJogoLink == indexacao.IdJogoLink) {
            return;
        }

        indexacao.ModeloExtracao = origem.ModeloExtracao;
        indexacao.ConfiabilidadeExtracao = origem.ConfiabilidadeExtracao;
        indexacao.ModeloRevisao = origem.ModeloRevisao;
        indexacao.CorrecoesAplicadas = origem.CorrecoesAplicadas;
        indexacao.CorrecoesDescartadas = origem.CorrecoesDescartadas;
        indexacao.RevisaoCompleta = origem.RevisaoCompleta;
    }

    /// <summary>
    /// Um `Duplicado` só não foi indexado porque outro link do jogo tinha o mesmo PDF.
    /// Quando esse outro sai de cena, os duplicados voltam para a fila e são reavaliados.
    /// </summary>
    private async Task ReenfileirarDuplicadosAsync(int idJogo, int idJogoLinkExceto) {
        if (idJogo == 0) {
            return;
        }

        foreach (var id in await _repository.GetIdsDuplicadosAsync(idJogo, idJogoLinkExceto)) {
            _queue.Enfileirar(new ManualJob(id, idJogo, ""));
        }
    }

    private static async Task<string> CalcularHashAsync(string caminho, CancellationToken cancellationToken) {
        await using var stream = File.OpenRead(caminho);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexStringLower(hash);
    }

    private static string Truncar(string mensagem) =>
        mensagem.Length <= TamanhoMaximoDoErro ? mensagem : mensagem[..TamanhoMaximoDoErro];
}
```

- [ ] **Step 5: Rodar os testes e ver passar**

Run: `dotnet test Tests/Tests.csproj --filter FullyQualifiedName~SincronizarManualTests`
Expected: todos passam.

- [ ] **Step 6: Registrar no DI**

Em `Src/Program.cs`, junto dos outros use cases de RAG:

```csharp
builder.Services.AddScoped<SincronizarManual>();
```

- [ ] **Step 7: Rodar tudo**

Run: `dotnet build ProximoTurnoApi.slnx` e depois `dotnet test Tests/Tests.csproj`
Expected: verde.

- [ ] **Step 8: Commit**

```bash
git add Src/Application/UseCases/RAG/SincronizarManual.cs Src/Program.cs Tests/Fakes/IndexacaoManualFakes.cs Tests/Domain/SincronizarManualTests.cs
git commit -m "feat: use case de sincronizacao do manual"
```

---

### Task 9: Troca do worker e dos produtores

**Files:**
- Modify: `Src/Application/UseCases/RAG/ManualJob.cs`
- Modify: `Src/Application/UseCases/RAG/ManualQueue.cs` (extensão nas linhas 11-28)
- Modify: `Src/Application/UseCases/RAG/SincronizarManual.cs` (chamada em `ReenfileirarDuplicadosAsync`)
- Modify: `Src/Application/Workers/IndexacaoManuaisWorker.cs` (arquivo inteiro)
- Modify: `Src/Application/UseCases/Jogo/CadastroJogo.cs:35`
- Modify: `Src/Application/UseCases/Jogo/AtualizarJogo.cs:36-41`
- Modify: `Src/Application/Controllers/JogosController.cs` (construtor e os 4 endpoints de ativação)
- Test: `Tests/Domain/CadastroJogoTests.cs`, `Tests/Domain/SincronizarManualTests.cs` (remover o terceiro argumento do `ManualJob`)

**Interfaces:**
- Consumes: `SincronizarManual.ExecuteAsync`/`MaxTentativas` (Task 8), `IIndexacaoManualRepository.GetElegiveisAsync`/`GetIdsComVetoresEsperadosAsync`/`GetIdsLinksAsync` (Task 2), `IManualVectorStore.ListarIdsLinksAsync` (Task 7).
- Produces: `ManualJob(int IdJogoLink, int IdJogo)` e `ManualQueueExtensions.EnfileirarSincronizacao(this IManualQueue, Jogo, IEnumerable<int>?)`.

- [ ] **Step 1: Encolher o job**

`Src/Application/UseCases/RAG/ManualJob.cs`:

```csharp
namespace ProximoTurnoApi.Application.UseCases.RAG;

/// <summary>
/// Pedido de sincronização de um manual. Só ids: a URL e o estado vêm do banco na hora de
/// executar, senão um job parado na fila carregaria uma foto velha do link.
/// O IdJogo serve para achar os duplicados quando o link já nem existe mais; vale 0 quando
/// quem enfileirou não sabe o jogo (reconciliação).
/// </summary>
public record ManualJob(int IdJogoLink, int IdJogo);
```

- [ ] **Step 2: Trocar a extensão da fila**

Em `Src/Application/UseCases/RAG/ManualQueue.cs`, substituir `EnfileirarManuaisPendentes` por:

```csharp
    /// <summary>
    /// Pede a sincronização de todos os links do jogo e dos que acabaram de ser removidos.
    /// Não filtra por tipo nem por estado de propósito: quem decide é o SincronizarManual,
    /// com o banco na mão. Chamar depois do SaveAsync, quando os links novos já têm Id.
    /// </summary>
    public static void EnfileirarSincronizacao(this IManualQueue queue, Jogo jogo, IEnumerable<int>? idsRemovidos = null) {
        foreach (var link in jogo.Links ?? []) {
            queue.Enfileirar(new ManualJob(link.Id, jogo.Id));
        }

        foreach (var id in idsRemovidos ?? []) {
            queue.Enfileirar(new ManualJob(id, jogo.Id));
        }
    }
```

- [ ] **Step 3: Ajustar as chamadas que criavam o job com URL**

Em `Src/Infrastructure/Repositories/IndexacaoManualRepository.cs`, no fim do `GetElegiveisAsync`:

```csharp
            .Select(jl => new ManualJob(jl.Id, jl.IdJogo))
```

Em `Src/Application/UseCases/RAG/SincronizarManual.cs`, dentro de `ReenfileirarDuplicadosAsync`:

```csharp
            _queue.Enfileirar(new ManualJob(id, idJogo));
```

Em `Tests/Domain/SincronizarManualTests.cs`, no helper `Executar`:

```csharp
    private Task Executar(int idJogoLink = 1, int idJogo = 99) =>
        Montar().ExecuteAsync(new ManualJob(idJogoLink, idJogo), CancellationToken.None);
```

- [ ] **Step 4: Reescrever o worker**

`Src/Application/Workers/IndexacaoManuaisWorker.cs` passa a ser:

```csharp
using ProximoTurnoApi.Application.UseCases.RAG;
using ProximoTurnoApi.Infrastructure.Logging;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.Workers;

public class IndexacaoManuaisWorker(ILogger<IndexacaoManuaisWorker> _logger,
                                    IManualQueue _queue,
                                    IServiceScopeFactory _scopeFactory) : BackgroundService {

    protected async override Task ExecuteAsync(CancellationToken stoppingToken) {
        using (RastreioBackground.Iniciar("IndexacaoManuais.Inicializacao")) {
            _logger.LogInformation("IndexacaoManuaisWorker iniciado.");

            var enfileirados = await EnfileirarElegiveisAsync();
            await ReconciliarAsync(enfileirados, stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested) {
            // Uma Activity por item da fila: assim as linhas de um manual ficam separadas
            // das do proximo pelo trace id, mesmo saindo intercaladas.
            using var rastreio = RastreioBackground.Iniciar("IndexacaoManual");

            try {
                var job = await _queue.DesenfileirarAsync(stoppingToken);
                _logger.LogInformation("Link {IdJogoLink} retirado da fila de indexação.", job.IdJogoLink);

                // Sequencial de proposito: a extracao chama LLM e nao vale disputar rate limit
                // com ela mesma.
                using var scope = _scopeFactory.CreateScope();
                var sincronizar = scope.ServiceProvider.GetRequiredService<SincronizarManual>();
                await sincronizar.ExecuteAsync(job, stoppingToken);
            } catch (OperationCanceledException) {
                break;
            } catch (Exception ex) {
                _logger.LogError(ex, "Erro no loop de indexação de manuais.");

                // Evita busy loop caso a falha seja no proprio desenfileiramento.
                try {
                    await Task.Delay(1000, stoppingToken);
                } catch (OperationCanceledException) {
                    break;
                }
            }
        }

        _logger.LogInformation("IndexacaoManuaisWorker finalizado.");
    }

    /// <summary>
    /// Carga inicial: tudo que ficou pendente enquanto a aplicação estava fora do ar.
    /// Devolve os ids enfileirados para a reconciliação não repetir os mesmos links.
    /// </summary>
    private async Task<HashSet<int>> EnfileirarElegiveisAsync() {
        try {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IIndexacaoManualRepository>();
            var elegiveis = await repository.GetElegiveisAsync(SincronizarManual.MaxTentativas);

            foreach (var job in elegiveis) {
                _queue.Enfileirar(job);
            }

            _logger.LogInformation("{Quantidade} manuais pendentes enfileirados na carga inicial.", elegiveis.Count);
            return [.. elegiveis.Select(job => job.IdJogoLink)];
        } catch (Exception ex) {
            // A carga inicial falhar nao pode impedir o worker de consumir os links novos.
            _logger.LogError(ex, "Falha ao enfileirar os manuais pendentes na carga inicial.");
            return [];
        }
    }

    /// <summary>
    /// Vetores que estão no Qdrant e não deveriam estar: link apagado enquanto a aplicação
    /// estava fora, jogo desativado, link que virou vídeo. Enfileira e deixa o use case decidir.
    /// </summary>
    private async Task ReconciliarAsync(HashSet<int> jaEnfileirados, CancellationToken stoppingToken) {
        try {
            using var scope = _scopeFactory.CreateScope();
            var vectorStore = scope.ServiceProvider.GetRequiredService<IManualVectorStore>();
            var repository = scope.ServiceProvider.GetRequiredService<IIndexacaoManualRepository>();

            var comVetores = await vectorStore.ListarIdsLinksAsync(stoppingToken);
            var esperados = await repository.GetIdsComVetoresEsperadosAsync();

            var orfaos = comVetores.Where(id => !esperados.Contains(id) && !jaEnfileirados.Contains(id)).ToList();
            foreach (var id in orfaos) {
                // IdJogo 0: o link pode nem existir mais, e sem jogo nao ha duplicado a reavaliar.
                _queue.Enfileirar(new ManualJob(id, 0));
            }

            _logger.LogInformation("Reconciliação: {Quantidade} link(s) com vetores sobrando enfileirados.", orfaos.Count);
        } catch (Exception ex) {
            _logger.LogError(ex, "Falha ao reconciliar os vetores com o banco.");
        }
    }
}
```

- [ ] **Step 5: Ajustar os produtores**

`Src/Application/UseCases/Jogo/CadastroJogo.cs:35`:

```csharp
            _manualQueue.EnfileirarSincronizacao(jogo);
```

`Src/Application/UseCases/Jogo/AtualizarJogo.cs`, no bloco `try`:

```csharp
            // Guardado antes do update: o UpdateModel remove da colecao os links que o
            // admin tirou, e sem esta lista nao saberiamos apagar os vetores deles.
            var idsAntes = jogo.Links?.Select(l => l.Id).ToList() ?? [];

            jogoDto.UpdateModel(jogo);
            await AtualizarTags(jogo, jogoDto.Tags);
            await _jogoRepository.SaveAsync(jogo);
            _logger.LogInformation("Jogo ID {JogoId} atualizado com sucesso.", jogo.Id);

            var idsDepois = jogo.Links?.Select(l => l.Id).ToHashSet() ?? [];
            _manualQueue.EnfileirarSincronizacao(jogo, idsAntes.Where(id => !idsDepois.Contains(id)));
            return IsValid;
```

`Src/Application/Controllers/JogosController.cs`: acrescentar ao construtor `IIndexacaoManualRepository _indexacaoRepository, IManualQueue _manualQueue` (com `using ProximoTurnoApi.Application.UseCases.RAG;`), criar o helper:

```csharp
    /// <summary>
    /// Avisa a fila que os manuais deste jogo podem ter mudado de situação: desativar tira
    /// os manuais da busca, reativar devolve.
    /// </summary>
    private async Task SincronizarManuaisAsync(int idJogo) {
        foreach (var idLink in await _indexacaoRepository.GetIdsLinksAsync(idJogo)) {
            _manualQueue.Enfileirar(new ManualJob(idLink, idJogo));
        }
    }
```

E chamar depois do save em `DeleteJogo` (`await SincronizarManuaisAsync(id);`), `DesativarCopiaJogo` (`await SincronizarManuaisAsync(idJogo);`), `ReativarJogo` (`await SincronizarManuaisAsync(id);`) e `ReativarCopiaJogo` (`await SincronizarManuaisAsync(idJogo);`), sempre antes do `return Ok(...)`.

- [ ] **Step 6: Atualizar os testes de cadastro**

Em `Tests/Domain/CadastroJogoTests.cs`, substituir o teste `Cadastro_EnfileiraApenasLinksDeRegra` por:

```csharp
    [Fact]
    public async Task Cadastro_EnfileiraTodosOsLinksDoJogo() {
        var jogoRepo = new FakeJogoRepository();
        var manualQueue = new FakeManualQueue();
        var jogoDto = NovoJogo("Azul");
        jogoDto.Links = [
            new JogoLinkDTO { Url = UrlManual, Titulo = "Manual", Tipo = TipoLink.Regra },
            new JogoLinkDTO { Url = "https://youtube.com/watch?v=abc", Titulo = "Como jogar", Tipo = TipoLink.Video }
        ];

        await Montar(jogoRepo, manualQueue).ExecuteAsync(jogoDto);

        // Quem filtra por tipo e o SincronizarManual, com o estado do banco: o link que
        // deixa de ser regra tambem precisa passar por la, para perder os vetores.
        Assert.Equal(2, manualQueue.Enfileirados.Count);
        Assert.All(manualQueue.Enfileirados, job => Assert.Equal(99, job.IdJogo));
    }
```

- [ ] **Step 7: Compilar e rodar tudo**

Run: `dotnet build ProximoTurnoApi.slnx` e depois `dotnet test Tests/Tests.csproj`
Expected: verde.

- [ ] **Step 8: Commit**

```bash
git add Src/Application/UseCases/RAG/ManualJob.cs Src/Application/UseCases/RAG/ManualQueue.cs Src/Application/UseCases/RAG/SincronizarManual.cs Src/Application/Workers/IndexacaoManuaisWorker.cs Src/Application/UseCases/Jogo/CadastroJogo.cs Src/Application/UseCases/Jogo/AtualizarJogo.cs Src/Application/Controllers/JogosController.cs Tests/Domain/CadastroJogoTests.cs Tests/Domain/SincronizarManualTests.cs
git commit -m "feat: worker e produtores passam a sincronizar links"
```

---

### Task 10: Limpeza do modelo antigo

**Files:**
- Modify: `Src/Infrastructure/Models/JogoLink.cs:25-27`
- Modify: `Src/Infrastructure/Repositories/JogoRepository.cs:25-26,246-272`
- Modify: `Tests/Fakes/PedidoUseCaseFakes.cs:107-108`, `Tests/Domain/CadastroJogoTests.cs:118-119`, `Tests/Domain/ValidarCupomTests.cs:74-75`
- Delete: `Src/Application/UseCases/RAG/MarkdownExtractor.cs`
- Create: `Src/Migrations/<timestamp>_removendo_campo_indexado_jogo_link.cs`

**Interfaces:**
- Consumes: nada novo.
- Produces: `JogoLink` sem `Indexado`; `IJogoRepository` sem `GetJogosNaoIndexadosAsync` e `MarcarIndexadoAsync`.

- [ ] **Step 1: Remover o campo e os métodos antigos**

Em `Src/Infrastructure/Models/JogoLink.cs`, apagar o comentário e a propriedade `Indexado`.

Em `Src/Infrastructure/Repositories/JogoRepository.cs`, apagar as duas linhas da interface (`GetJogosNaoIndexadosAsync` e `MarcarIndexadoAsync`) e as duas implementações no fim do arquivo. O filtro de jogo desativado que estava em `GetJogosNaoIndexadosAsync` já vive em `IndexacaoManualRepository.GetElegiveisAsync`.

Em cada um dos três fakes (`Tests/Fakes/PedidoUseCaseFakes.cs`, `Tests/Domain/CadastroJogoTests.cs`, `Tests/Domain/ValidarCupomTests.cs`), apagar as duas linhas:

```csharp
    public Task<List<JogoLink>> GetJogosNaoIndexadosAsync(int? quantidade = null) => throw new NotImplementedException();
    public Task MarcarIndexadoAsync(int idJogoLink) => throw new NotImplementedException();
```

- [ ] **Step 2: Apagar o MarkdownExtractor**

Run: `git rm Src/Application/UseCases/RAG/MarkdownExtractor.cs`
Expected: arquivo removido. Ele não estava no DI nem era usado por ninguém; a lógica dele vive em `SincronizarManual.ExtrairAsync`.

- [ ] **Step 3: Compilar e rodar os testes**

Run: `dotnet build ProximoTurnoApi.slnx` e depois `dotnet test Tests/Tests.csproj`
Expected: verde. Qualquer erro aqui aponta um uso restante do campo antigo.

- [ ] **Step 4: Gerar a migração**

Run: `dotnet ef migrations add removendo_campo_indexado_jogo_link --project Src/ProximoTurnoApi.csproj`
Expected: migração com `DropColumn` de `INDEXADO` em `JOGO_LINK`.

- [ ] **Step 5: Aplicar no banco de dev**

Run: `dotnet ef database update --project Src/ProximoTurnoApi.csproj`
Expected: "Done." e a coluna some.

- [ ] **Step 6: Commit**

```bash
git add -A Src Tests
git commit -m "refactor: remove o campo indexado e o MarkdownExtractor"
```

---

### Task 11: Verificação ponta a ponta em dev

Sem código. Estas são as partes que não têm teste unitário: as consultas LINQ, as duas migrações e as chamadas ao Qdrant.

**Files:**
- Nenhum. Só execução e conferência.

- [ ] **Step 1: Subir a infraestrutura**

Run: `docker-compose up -d`
Expected: MySQL de pé na porta que o `appsettings.Development.json` deste worktree usa (3309). Confirme no `.env` que `OPENROUTER_API_KEY`, `QDRANT_URL` e `QDRANT_API_KEY` estão preenchidas e que o ambiente é `Development`, para gravar na coleção `manuais_dev`.

- [ ] **Step 2: Rodar a API e observar a carga inicial**

Run: `dotnet run --project Src/ProximoTurnoApi.csproj`
Expected: nos logs, "N manuais pendentes enfileirados na carga inicial" e "Reconciliação: N link(s) com vetores sobrando enfileirados". Depois, para cada manual, as linhas de extração, revisão ("Revisão de N bloco(s)...") e "indexado com N chunks".

- [ ] **Step 3: Conferir a tabela**

Run (no cliente MySQL): `SELECT ID_JOGO_LINK, STATUS, TENTATIVAS, HASH_PDF, MODELO_EXTRACAO, CONFIABILIDADE_EXTRACAO, MODELO_REVISAO, CORRECOES_APLICADAS, CORRECOES_DESCARTADAS, REVISAO_COMPLETA, QUANTIDADE_CHUNKS FROM JOGO_LINK_INDEXACAO;`
Expected: uma linha por link de regra, com `STATUS = 1` (Indexado), hash preenchido e `MODELO_EXTRACAO` com o Gemini nos manuais novos.

- [ ] **Step 4: Conferir o texto revisado**

Confira na pasta `Src/wwwroot/uploads` que existem `{hash}.raw.md` e `{hash}.md`, e compare os dois (por exemplo com `git diff --no-index`). As diferenças devem ser só trocas de uma ou duas letras. Confira também no log as linhas "Revisão aplicou ... -> ...".
Expected: nenhuma frase reescrita, nenhum número alterado.

- [ ] **Step 5: Trocar o PDF de um link**

Pelo admin, suba outro PDF no mesmo link de regra de um jogo e salve.
Expected: o log mostra o link entrando na fila, os vetores antigos sendo removidos e a reindexação. Na tabela, a `URL` muda e `TENTATIVAS` volta a zero.

- [ ] **Step 6: Remover um link**

Remova um link de regra de um jogo e salve.
Expected: log "Link N não existe mais: vetores removidos". No Qdrant, uma busca filtrada por aquele `IdJogoLink` não devolve nada, e a linha da tabela some junto com o link.

- [ ] **Step 7: Desativar e reativar um jogo**

Use `DELETE /api/jogos/{id}` e depois `PUT /api/jogos/{id}/reativar`.
Expected: ao desativar, `STATUS = 3` (Removido) e vetores apagados; ao reativar, volta a `Indexado` sem novas chamadas de OCR nem de revisão (o cache por hash já está no disco).

- [ ] **Step 8: PDF repetido**

Cadastre dois links de regra no mesmo jogo apontando para o mesmo PDF.
Expected: o segundo fica com `STATUS = 4` (Duplicado), sem vetores, e o log diz "aponta para um PDF já indexado no jogo".

- [ ] **Step 9: Commit final**

Se algum ajuste foi necessário durante a verificação, commit com a correção. Caso contrário, nada a fazer.
