using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases.RAG;

/// <summary>
/// Preenche, nas telas do admin, se o assistente de regras já tem o manual de cada jogo e
/// a situação de cada link de regra.
/// </summary>
public class SituacaoManuais(IIndexacaoManualRepository _repository) : UseCaseBasico {

    public async Task PreencherAsync(IReadOnlyList<JogoCardDTO> jogos) {
        var porJogo = (await _repository.GetIndexacoesAsync())
            .Select(l => (Link: l, IndexacaoLinkDTO.De(l.Url, l.Indexacao).Situacao))
            .GroupBy(l => l.Link.IdJogo)
            .ToDictionary(g => g.Key, g => (
                Situacao: IndexacaoLinkDTO.Resumir(g.Select(l => l.Situacao)),
                Indexados: g.Where(l => l.Situacao == SituacaoManual.Indexado)
                            .OrderBy(l => l.Link.IdJogoLink)
                            .Select(l => new ManualIndexadoDTO(l.Link.IdJogoLink, l.Link.Titulo))
                            .ToList()));

        foreach (var jogo in jogos) {
            if (porJogo.TryGetValue(jogo.Id, out var resumo)) {
                jogo.Manual = resumo.Situacao;
                jogo.ManuaisIndexados = resumo.Indexados;
            } else {
                jogo.Manual = SituacaoManual.SemManual;
                jogo.ManuaisIndexados = [];
            }
        }
    }

    public async Task PreencherAsync(JogoDTO jogo) {
        var porLink = (await _repository.GetIndexacoesAsync(jogo.Id)).ToDictionary(l => l.IdJogoLink);

        foreach (var link in jogo.Links ?? []) {
            if (link.Tipo == TipoLink.Regra && porLink.TryGetValue(link.Id, out var linha)) {
                link.Indexacao = IndexacaoLinkDTO.De(linha.Url, linha.Indexacao);
            }
        }
    }
}
