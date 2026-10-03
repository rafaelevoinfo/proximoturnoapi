using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases.RAG;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Tests.Fakes;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class ObterMarkdownManualTests : IDisposable {

    private const string Hash = "3f9a0000000000000000000000000000000000000000000000000000000c41e0";

    private readonly FakeWebHostEnvironment _env = new();
    private readonly FakeIndexacaoManualRepository _repositorio = new();

    public void Dispose() => _env.Dispose();

    private ObterMarkdownManual UseCase() => new(_repositorio, _env);

    private void Gravar(string arquivo, string conteudo) => File.WriteAllText(Path.Combine(_env.Uploads, arquivo), conteudo);

    private void AdicionarIndexado(int idJogoLink, string hash = Hash, string url = "https://site/uploads/manual.pdf") =>
        _repositorio.Adicionar(idJogoLink, url: url, tituloLink: "FAQ", indexacao: new JogoLinkIndexacao {
            Url = url, HashPdf = hash, Status = StatusIndexacao.Indexado, QuantidadeChunks = 12,
            ModeloExtracao = "google/gemini-3.6-flash", ConfiabilidadeExtracao = 93,
        });

    [Fact]
    public async Task SemVersaoPedida_DevolveARevisada_QueFoiParaABusca() {
        AdicionarIndexado(7);
        Gravar($"{Hash}.md", "# Revisado");
        Gravar($"{Hash}.raw.md", "# Bruto");

        var r = (await UseCase().ExecuteAsync(7, null))!;

        Assert.Equal("# Revisado", r.Conteudo);
        Assert.Equal(VersaoMarkdownManual.Revisado, r.Versao);
        Assert.Equal(VersaoMarkdownManual.Revisado, r.VersaoIndexada);
        Assert.Equal([VersaoMarkdownManual.Revisado, VersaoMarkdownManual.Bruto], r.VersoesDisponiveis);
        Assert.Equal("FAQ", r.TituloManual);
        Assert.Equal((short)93, r.ConfiabilidadeExtracao);
        Assert.True(r.PdfAtualDoLink);
    }

    [Fact]
    public async Task VersaoBrutaPedida_DevolveOOcr() {
        AdicionarIndexado(7);
        Gravar($"{Hash}.md", "# Revisado");
        Gravar($"{Hash}.raw.md", "# Bruto");

        var r = (await UseCase().ExecuteAsync(7, VersaoMarkdownManual.Bruto))!;

        Assert.Equal("# Bruto", r.Conteudo);
        Assert.Equal(VersaoMarkdownManual.Revisado, r.VersaoIndexada);
    }

    [Fact]
    public async Task SemRevisado_ABrutaEhAIndexada() {
        AdicionarIndexado(7);
        Gravar($"{Hash}.raw.md", "# Bruto");

        var r = (await UseCase().ExecuteAsync(7, VersaoMarkdownManual.Revisado))!;

        Assert.Equal("# Bruto", r.Conteudo);
        Assert.Equal(VersaoMarkdownManual.Bruto, r.VersaoIndexada);
        Assert.Equal([VersaoMarkdownManual.Bruto], r.VersoesDisponiveis);
    }

    [Fact]
    public async Task SemArquivoOuSemIndexacao_DevolveNull() {
        AdicionarIndexado(7);
        _repositorio.Adicionar(8);

        Assert.Null(await UseCase().ExecuteAsync(7, null));
        Assert.Null(await UseCase().ExecuteAsync(8, null));
        Assert.Null(await UseCase().ExecuteAsync(99, null));
    }

    [Fact]
    public async Task HashQueNaoEhSha256_NaoViraCaminho() {
        AdicionarIndexado(7, hash: "../../appsettings");
        Gravar("x.md", "nada");

        Assert.Null(await UseCase().ExecuteAsync(7, null));
    }

    [Fact]
    public async Task PdfTrocadoNoLink_AvisaQueOTextoEhDoAnterior() {
        AdicionarIndexado(7, url: "https://site/uploads/antigo.pdf");
        _repositorio.Estados[0] = _repositorio.Estados[0] with { Url = "https://site/uploads/novo.pdf" };
        Gravar($"{Hash}.md", "# Antigo");

        var r = (await UseCase().ExecuteAsync(7, null))!;

        Assert.False(r.PdfAtualDoLink);
        Assert.Equal("https://site/uploads/antigo.pdf", r.UrlPdf);
    }

    [Fact]
    public async Task Listagem_TrazTodosOsManuaisIndexadosDoJogo() {
        foreach (var (id, titulo, status) in new[] {
                     (10, "Manual", StatusIndexacao.Indexado), (11, "FAQ", StatusIndexacao.Indexado), (12, "Expansão", StatusIndexacao.Falhou) }) {
            _repositorio.Adicionar(id, idJogo: 5, tituloLink: titulo,
                indexacao: new JogoLinkIndexacao { Url = "https://site/uploads/manual.pdf", Status = status });
        }
        var cards = new List<JogoCardDTO> { new() { Id = 5 }, new() { Id = 6 } };

        await new SituacaoManuais(_repositorio).PreencherAsync(cards);

        Assert.Equal(SituacaoManual.Indexado, cards[0].Manual);
        Assert.Equal([new ManualIndexadoDTO(10, "Manual"), new ManualIndexadoDTO(11, "FAQ")], cards[0].ManuaisIndexados);
        Assert.Equal(SituacaoManual.SemManual, cards[1].Manual);
        Assert.Empty(cards[1].ManuaisIndexados!);
    }
}
