using System.Globalization;
using System.Text.Json;
using ProximoTurnoApi.Application.UseCases.IA;

namespace ProximoTurnoApi.Infrastructure.IA;

/// <summary>
/// Cotação da AwesomeAPI: gratuita, sem chave e mantida no Brasil. Singleton com cache em
/// memória, porque a cotação muda devagar e uma pergunta no chat não pode esperar uma
/// chamada externa a cada vez.
/// </summary>
public class CotacaoDolarAwesomeApi : ICotacaoDolar {

    public const string NomeHttpClient = "cotacao-dolar";
    public const string Endereco = "https://economia.awesomeapi.com.br/json/last/USD-BRL";

    public static readonly TimeSpan Validade = TimeSpan.FromHours(6);

    // Depois de uma falha a API nao e consultada de novo por um tempo: sem isso, com ela fora
    // do ar, toda pergunta do chat pagaria o timeout inteiro.
    public static readonly TimeSpan EsperaAposFalha = TimeSpan.FromMinutes(10);

    private readonly ILogger<CotacaoDolarAwesomeApi> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly decimal _padrao;
    private readonly Func<DateTime> _agora;
    private readonly SemaphoreSlim _trava = new(1, 1);

    private CotacaoUsdBrl? _ultimaDaApi;
    private DateTime _proximaTentativa = DateTime.MinValue;

    public CotacaoDolarAwesomeApi(ILogger<CotacaoDolarAwesomeApi> logger,
                                  IHttpClientFactory httpClientFactory,
                                  decimal padrao,
                                  Func<DateTime>? agora = null) {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _padrao = padrao;
        _agora = agora ?? (() => DateTime.Now);
    }

    public CotacaoUsdBrl Atual => _ultimaDaApi ?? new CotacaoUsdBrl(_padrao, FonteCotacao.Padrao, _agora());

    public async Task<CotacaoUsdBrl> ObterAsync(CancellationToken cancellationToken = default) {
        if (Valida(_ultimaDaApi) || _agora() < _proximaTentativa) {
            return Reaproveitar();
        }

        await _trava.WaitAsync(cancellationToken);
        try {
            // Quem esperou a trava pode encontrar o trabalho feito por quem estava na frente.
            if (Valida(_ultimaDaApi) || _agora() < _proximaTentativa) {
                return Reaproveitar();
            }

            var valor = await ConsultarAsync(cancellationToken);
            if (valor is not null) {
                _ultimaDaApi = new CotacaoUsdBrl(valor.Value, FonteCotacao.Api, _agora());
                _logger.LogInformation("Cotação do dólar atualizada: R$ {Valor}.", valor.Value);
                return _ultimaDaApi;
            }

            _proximaTentativa = _agora() + EsperaAposFalha;
            return Reaproveitar();
        } finally {
            _trava.Release();
        }
    }

    private bool Valida(CotacaoUsdBrl? cotacao) => cotacao is not null && _agora() - cotacao.Momento < Validade;

    private CotacaoUsdBrl Reaproveitar() {
        if (_ultimaDaApi is null) {
            return new CotacaoUsdBrl(_padrao, FonteCotacao.Padrao, _agora());
        }

        return Valida(_ultimaDaApi) ? _ultimaDaApi : _ultimaDaApi with { Fonte = FonteCotacao.UltimaConhecida };
    }

    /// <summary>Devolve null em qualquer falha: a cotação nunca derruba o chat.</summary>
    private async Task<decimal?> ConsultarAsync(CancellationToken cancellationToken) {
        try {
            var cliente = _httpClientFactory.CreateClient(NomeHttpClient);
            using var resposta = await cliente.GetAsync(Endereco, cancellationToken);
            if (!resposta.IsSuccessStatusCode) {
                _logger.LogWarning("AwesomeAPI respondeu HTTP {Status}; usando cotação anterior ou padrão.", (int)resposta.StatusCode);
                return null;
            }

            var corpo = await resposta.Content.ReadAsStringAsync(cancellationToken);
            var valor = Interpretar(corpo);
            if (valor is null) {
                _logger.LogWarning("Resposta da AwesomeAPI sem cotação válida; usando cotação anterior ou padrão.");
            }

            return valor;
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception ex) {
            _logger.LogWarning(ex, "Falha ao consultar a cotação do dólar: {Mensagem}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Lê o <c>ask</c> de <c>{"USDBRL":{"ask":"5.4321",...}}</c>. O valor vem como texto com
    /// ponto decimal. Fora de uma faixa plausível é tratado como resposta quebrada.
    /// </summary>
    public static decimal? Interpretar(string? corpo) {
        if (string.IsNullOrWhiteSpace(corpo)) {
            return null;
        }

        try {
            using var documento = JsonDocument.Parse(corpo);
            if (documento.RootElement.ValueKind != JsonValueKind.Object
                || !documento.RootElement.TryGetProperty("USDBRL", out var par)
                || par.ValueKind != JsonValueKind.Object
                || !par.TryGetProperty("ask", out var ask)) {
                return null;
            }

            decimal valor;
            if (ask.ValueKind == JsonValueKind.String) {
                if (!decimal.TryParse(ask.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out valor)) {
                    return null;
                }
            } else if (ask.ValueKind != JsonValueKind.Number || !ask.TryGetDecimal(out valor)) {
                return null;
            }

            return valor is > 0.5m and < 50m ? valor : null;
        } catch (JsonException) {
            return null;
        }
    }
}
