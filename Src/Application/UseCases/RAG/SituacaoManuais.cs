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
            .GroupBy(l => l.IdJogo)
            .ToDictionary(g => g.Key, g => IndexacaoLinkDTO.Resumir(g.Select(l => IndexacaoLinkDTO.De(l.Url, l.Indexacao).Situacao)));

        foreach (var jogo in jogos) {
            jogo.Manual = porJogo.GetValueOrDefault(jogo.Id, SituacaoManual.SemManual);
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
