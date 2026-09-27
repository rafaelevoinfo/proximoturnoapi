using System.Text.Json.Serialization;

namespace ProximoTurnoApi.Application.UseCases.IA;

/// <summary>De onde veio o valor da cotação. Vai para o log e para o dashboard.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FonteCotacao>))]
public enum FonteCotacao : short {
    /// <summary>Consultada na API agora ou dentro da validade do cache.</summary>
    Api = 0,
    /// <summary>A API falhou e o último valor bom foi reaproveitado.</summary>
    UltimaConhecida = 1,
    /// <summary>Nunca houve valor da API: veio da configuração.</summary>
    Padrao = 2
}

public sealed record CotacaoUsdBrl(decimal Valor, FonteCotacao Fonte, DateTime Momento);

/// <summary>
/// Cotação do dólar em reais, para converter o gasto de LLM (que a OpenRouter devolve em
/// US$) no crédito do usuário (que é em R$). Nunca lança: sem API, cai no último valor
/// conhecido e, sem nenhum, no valor da configuração.
/// </summary>
public interface ICotacaoDolar {

    Task<CotacaoUsdBrl> ObterAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sem I/O, para o caminho síncrono do ledger: o último valor obtido, ou o padrão da
    /// configuração se a API ainda não foi consultada.
    /// </summary>
    CotacaoUsdBrl Atual { get; }
}
