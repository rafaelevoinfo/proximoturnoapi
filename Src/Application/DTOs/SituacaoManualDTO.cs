using ProximoTurnoApi.Application.UseCases.RAG;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.DTOs;

/// <summary>Situação de um manual de regras no assistente, do ponto de vista do admin.</summary>
public enum SituacaoManual : short {
    /// <summary>Jogo sem link de regra com PDF.</summary>
    SemManual = 0,
    /// <summary>O manual está na busca do assistente.</summary>
    Indexado = 1,
    /// <summary>Ainda não processado, ou o PDF do link mudou e espera a vez na fila.</summary>
    Pendente = 2,
    Processando = 3,
    /// <summary>A última tentativa falhou; veja o erro.</summary>
    Falhou = 4,
    /// <summary>Mesmo PDF de outro link do jogo, que é o que está indexado.</summary>
    Duplicado = 5,
    /// <summary>Fora da busca porque o jogo está desativado.</summary>
    Removido = 6,
}

public record IndexacaoLinkDTO {
    public SituacaoManual Situacao { get; init; }
    public DateTime? DataIndexacao { get; init; }
    public int? QuantidadeTrechos { get; init; }
    public int Tentativas { get; init; }
    /// <summary>Esgotou as tentativas nesta URL: só volta com reindexação ou troca do PDF.</summary>
    public bool TentativasEsgotadas { get; init; }
    public string? UltimoErro { get; init; }

    public static IndexacaoLinkDTO De(string urlLink, JogoLinkIndexacao? indexacao) {
        // Linha de outra URL e de antes da troca do PDF: o link novo ainda nao foi processado.
        if (indexacao is null || indexacao.Url != urlLink) {
            return new IndexacaoLinkDTO { Situacao = SituacaoManual.Pendente };
        }

        return new IndexacaoLinkDTO {
            Situacao = indexacao.Status switch {
                StatusIndexacao.Indexado => SituacaoManual.Indexado,
                StatusIndexacao.Processando => SituacaoManual.Processando,
                StatusIndexacao.Falhou => SituacaoManual.Falhou,
                StatusIndexacao.Duplicado => SituacaoManual.Duplicado,
                _ => SituacaoManual.Removido,
            },
            DataIndexacao = indexacao.DataIndexacao,
            QuantidadeTrechos = indexacao.QuantidadeChunks,
            Tentativas = indexacao.Tentativas,
            TentativasEsgotadas = indexacao.Status == StatusIndexacao.Falhou && indexacao.Tentativas >= SincronizarManual.MaxTentativas,
            UltimoErro = indexacao.Status == StatusIndexacao.Falhou ? indexacao.UltimoErro : null,
        };
    }

    /// <summary>
    /// Resumo do jogo na listagem: basta um manual indexado para o assistente responder, então
    /// ele vence; depois o que ainda vai mudar sozinho (processando, na fila) e por último as falhas.
    /// </summary>
    public static SituacaoManual Resumir(IEnumerable<SituacaoManual> situacoes) {
        SituacaoManual[] prioridade = [SituacaoManual.Indexado, SituacaoManual.Processando, SituacaoManual.Pendente,
                                       SituacaoManual.Falhou, SituacaoManual.Removido, SituacaoManual.Duplicado];
        var lista = situacoes.ToList();
        foreach (var situacao in prioridade) {
            if (lista.Contains(situacao)) {
                return situacao;
            }
        }

        return SituacaoManual.SemManual;
    }
}
