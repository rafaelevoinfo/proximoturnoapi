using Microsoft.Extensions.Logging.Abstractions;
using ProximoTurnoApi.Application.UseCases.IA;
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
        Montar().ExecuteAsync(new ManualJob(idJogoLink, idJogo), CancellationToken.None);

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
    public async Task MarcadoRemovido_ZeraTentativas() {
        // Sem isso, um link que ja tinha falhado antes de virar video chega ao teto de
        // tentativas na primeira falha depois de voltar a ser de regra.
        _repo.Adicionar(1, tipo: TipoLink.Video, indexacao: new JogoLinkIndexacao { Url = Url, Status = StatusIndexacao.Falhou, Tentativas = 2 });

        await Executar();

        Assert.Equal(StatusIndexacao.Removido, _repo.Linha(1)!.Status);
        Assert.Equal(0, _repo.Linha(1)!.Tentativas);
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
        Assert.Equal((short)91, linha.ConfiabilidadeExtracao);
        Assert.Equal("revisor/falso", linha.ModeloRevisao);
        Assert.True(linha.RevisaoCompleta);
        Assert.NotNull(linha.DataIndexacao);
        Assert.True(linha.QuantidadeChunks > 0);
        Assert.True(_vetores.Gravados.ContainsKey(1));
        Assert.Equal(2, _env.Markdowns().Length); // {hash}.raw.md e {hash}.md
    }

    [Fact]
    public async Task LinkNovo_AbreOEscopoDeAlvoAntesDeChamarLlm() {
        _env.CriarPdf(NomeArquivo);
        _repo.Adicionar(1);

        await Executar();

        // Valores do fixture: Executar() usa idJogo 99 e idJogoLink 1, e Adicionar() usa
        // "Balde de Caranguejo" / "Manual", que ContextoManual.Prefixo junta com " > ".
        Assert.Equal(99, _extrator.AlvoVisto?.IdJogo);
        Assert.Equal(1, _extrator.AlvoVisto?.IdJogoLink);
        Assert.Equal("Balde de Caranguejo > Manual", _extrator.AlvoVisto?.Alvo);
        Assert.Null(EscopoUsoLlm.Atual);
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
    public async Task MarcadoDuplicado_ZeraTentativas() {
        // Sem isso, um link que falhou antes de virar duplicado chega ao teto de tentativas
        // assim que o outro link com o mesmo PDF sai de cena e ele volta a ser processado.
        _env.CriarPdf(NomeArquivo, "conteudo igual");
        _env.CriarPdf("copia.pdf", "conteudo igual");
        _repo.Adicionar(1, url: Url);
        _repo.Adicionar(2, url: "https://site/uploads/copia.pdf",
                        indexacao: new JogoLinkIndexacao { Url = "https://site/uploads/copia.pdf", Status = StatusIndexacao.Falhou, Tentativas = 2 });

        await Executar(idJogoLink: 1);
        await Executar(idJogoLink: 2);

        Assert.Equal(StatusIndexacao.Duplicado, _repo.Linha(2)!.Status);
        Assert.Equal(0, _repo.Linha(2)!.Tentativas);
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
        Assert.Contains(1, _vetores.Removidos);
    }

    [Fact]
    public async Task ProcessandoDeExecucaoAnterior_ContaComoTentativaAntesDeRetomar() {
        // Sem o arquivo, a extracao falha e soma mais uma tentativa no catch: assim da para
        // separar, no total, a tentativa da retomada da tentativa da falha.
        _repo.Adicionar(1, indexacao: new JogoLinkIndexacao { Url = Url, Status = StatusIndexacao.Processando, Tentativas = 1 });

        await Executar();

        // 1 (estado inicial) + 1 (retomada de Processando) + 1 (falha por arquivo ausente) = 3
        Assert.Equal(3, _repo.Linha(1)!.Tentativas);
    }

    [Fact]
    public async Task FalhaAoGravarVetores_JaTinhaRemovidoOsAntigos() {
        // A remocao acontece ao entrar no pipeline, nao depois que a gravacao da certo:
        // mesmo falhando no ultimo passo, os vetores antigos ja foram embora.
        _env.CriarPdf(NomeArquivo);
        _vetores.ErroAoSalvar = new InvalidOperationException("qdrant fora do ar");
        _repo.Adicionar(1);

        await Executar();

        var linha = _repo.Linha(1)!;
        Assert.Equal(StatusIndexacao.Falhou, linha.Status);
        Assert.Equal(1, linha.Tentativas);
        Assert.Contains(1, _vetores.Removidos);
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

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Executar());

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
        Assert.Equal((short)91, _repo.Linha(2)!.ConfiabilidadeExtracao);
    }
}
