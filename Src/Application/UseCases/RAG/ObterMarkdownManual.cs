using System.Text.RegularExpressions;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases.RAG;

/// <summary>
/// Lê o markdown que a indexação gerou para um link de manual (`{hash}.md` ou `{hash}.raw.md`
/// na pasta de uploads), para o admin conferir o que o assistente usa contra o PDF.
/// </summary>
public partial class ObterMarkdownManual(IIndexacaoManualRepository _repository, IWebHostEnvironment _env) : UseCaseBasico {

    // O hash vem do banco, mas vira nome de arquivo: só hex de SHA-256, nada de caminho.
    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex HashValido();

    /// <param name="versao">Null escolhe a que foi para a busca.</param>
    /// <returns>Null quando o link não existe ou ainda não tem markdown gerado.</returns>
    public async Task<MarkdownManualDTO?> ExecuteAsync(int idJogoLink, VersaoMarkdownManual? versao, CancellationToken ct = default) {
        var estado = await _repository.GetEstadoAsync(idJogoLink);
        var indexacao = estado?.Indexacao;
        if (estado is null || indexacao?.HashPdf is not { } hash || !HashValido().IsMatch(hash)) {
            return null;
        }

        var pasta = UploadManual.GetUploadFolder(_env);
        var arquivos = new Dictionary<VersaoMarkdownManual, string> {
            [VersaoMarkdownManual.Revisado] = Path.Combine(pasta, $"{hash}.md"),
            [VersaoMarkdownManual.Bruto] = Path.Combine(pasta, $"{hash}.raw.md"),
        };
        var disponiveis = arquivos.Where(a => File.Exists(a.Value)).Select(a => a.Key).ToList();
        if (disponiveis.Count == 0) {
            return null;
        }

        // Sem o revisado, a revisão falhou inteira e a busca recebeu o texto do OCR.
        var indexada = disponiveis.Contains(VersaoMarkdownManual.Revisado) ? VersaoMarkdownManual.Revisado : VersaoMarkdownManual.Bruto;
        var escolhida = versao is { } v && disponiveis.Contains(v) ? v : indexada;

        return new MarkdownManualDTO {
            IdJogoLink = estado.IdJogoLink,
            IdJogo = estado.IdJogo,
            NomeJogo = estado.NomeJogo,
            TituloManual = estado.TituloLink,
            UrlPdf = indexacao.Url,
            PdfAtualDoLink = indexacao.Url == estado.Url,
            Situacao = IndexacaoLinkDTO.De(estado.Url, indexacao).Situacao,
            Versao = escolhida,
            VersoesDisponiveis = disponiveis,
            VersaoIndexada = indexada,
            Conteudo = await File.ReadAllTextAsync(arquivos[escolhida], ct),
            ModeloExtracao = indexacao.ModeloExtracao,
            ConfiabilidadeExtracao = indexacao.ConfiabilidadeExtracao,
            ModeloRevisao = indexacao.ModeloRevisao,
            CorrecoesAplicadas = indexacao.CorrecoesAplicadas,
            QuantidadeTrechos = indexacao.QuantidadeChunks,
            DataIndexacao = indexacao.DataIndexacao,
        };
    }
}
