using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using ProximoTurnoApi.Application.UseCases.IA;
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
    public Exception? ErroAoListarElegiveis { get; set; }

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

    public Task<List<ManualJob>> GetElegiveisAsync(int maxTentativas) {
        if (ErroAoListarElegiveis is not null) {
            throw ErroAoListarElegiveis;
        }

        return Task.FromResult(Elegiveis.ToList());
    }

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

    /// <summary>Alvo ambiente visto durante a chamada: prova que o escopo estava aberto.</summary>
    public AlvoUsoLlm? AlvoVisto { get; private set; }

    public Task<ResultadoExtracao> ExtractTextAsync(string filePath, CancellationToken cancellationToken) {
        AlvoVisto = EscopoUsoLlm.Atual;
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
