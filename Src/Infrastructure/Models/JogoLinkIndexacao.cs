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
