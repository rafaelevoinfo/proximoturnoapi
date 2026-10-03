using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ProximoTurnoApi.Application.DTOs;

namespace ProximoTurnoApi.Application.UseCases;

/// <summary>
/// Lê os arquivos de log do Serilog para a tela de logs do admin, sem precisar de acesso
/// ao servidor. Depende do outputTemplate do appsettings:
/// "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] [{TraceId}] [{Caller}] {Message:lj}{NewLine}{Exception}".
/// </summary>
public partial class ConsultarLogs(ArquivosLogOptions options) : UseCaseBasico {

    public const int LimitePadrao = 500;
    public const int LimiteMaximo = 5000;

    public static readonly string[] Niveis = ["Verbose", "Debug", "Information", "Warning", "Error", "Fatal"];

    private const string FormatoDataHora = "yyyy-MM-dd HH:mm:ss.fff zzz";

    [GeneratedRegex(@"^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} [+-]\d{2}:\d{2}) \[(\w+)\] \[([^\]]*)\] \[([^\]]*)\] ?(.*)$")]
    private static partial Regex Cabecalho();

    public IReadOnlyList<ArquivoLogDTO> ListarArquivos() {
        if (!Directory.Exists(options.Diretorio)) return [];

        return new DirectoryInfo(options.Diretorio)
            .EnumerateFiles($"{options.Prefixo}*{options.Extensao}")
            .Where(f => NomeValido(f.Name))
            // yyyyMMdd e o sufixo _NNN do rollOnFileSizeLimit ordenam como texto.
            .OrderByDescending(f => f.Name, StringComparer.Ordinal)
            .Select(f => new ArquivoLogDTO {
                Nome = f.Name,
                TamanhoBytes = f.Length,
                UltimaAlteracao = f.LastWriteTimeUtc,
            })
            .ToList();
    }

    public async Task<ConsultaLogsDTO> ExecuteAsync(FiltroLogs filtro, CancellationToken ct = default) {
        var criterios = Criterios.De(filtro);
        var arquivos = ArquivosDaConsulta(filtro);
        if (arquivos.Count == 0) return new ConsultaLogsDTO();

        var max = Math.Clamp(filtro.Limite ?? LimitePadrao, 1, LimiteMaximo);

        // Guarda só as últimas `max` entradas que passam no filtro: cada arquivo pode ter
        // até 10 MB e um período pode juntar vários.
        var ultimas = new Queue<EntradaLogDTO>(max);
        var total = 0;

        foreach (var nome in arquivos) {
            using var leitor = new StreamReader(Abrir(nome), Encoding.UTF8);
            await foreach (var entrada in LerEntradas(leitor, ct)) {
                if (!criterios.Passa(entrada)) continue;
                total++;
                if (ultimas.Count == max) ultimas.Dequeue();
                ultimas.Enqueue(entrada with { Arquivo = nome });
            }
        }

        return new ConsultaLogsDTO {
            Arquivos = arquivos,
            TotalEncontrado = total,
            Entradas = ultimas.Reverse().ToList(),
        };
    }

    /// <summary>
    /// Arquivos a ler, do mais antigo para o mais novo. Arquivo explícito ganha; senão um
    /// período pega os dias que ele cobre; sem nada, só o arquivo mais recente.
    /// </summary>
    private IReadOnlyList<string> ArquivosDaConsulta(FiltroLogs filtro) {
        if (!string.IsNullOrWhiteSpace(filtro.Arquivo)) return [filtro.Arquivo.Trim()];

        var todos = ListarArquivos();
        if (filtro.Inicio is null && filtro.Fim is null) {
            return todos.Count == 0 ? [] : [todos[0].Nome];
        }

        // O nome traz a data local do servidor, que pode estar em outro fuso que o do
        // filtro: um dia de folga em cada ponta e o filtro por entrada faz o corte exato.
        var de = filtro.Inicio is { } i ? DateOnly.FromDateTime(i.UtcDateTime).AddDays(-1) : DateOnly.MinValue;
        var ate = filtro.Fim is { } f ? DateOnly.FromDateTime(f.UtcDateTime).AddDays(1) : DateOnly.MaxValue;

        return todos
            .Where(a => DataDoNome(a.Nome) is { } d && d >= de && d <= ate)
            .Select(a => a.Nome)
            // "log20261003.log" vem antes de "log20261003_001.log" ('.' < '_').
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
    }

    private DateOnly? DataDoNome(string nome) =>
        DateOnly.TryParseExact(nome.AsSpan(options.Prefixo.Length, 8), "yyyyMMdd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var d) ? d : null;

    /// <summary>Stream do arquivo bruto, para download.</summary>
    public Stream Abrir(string nome) {
        if (!NomeValido(nome)) throw new InvalidOperationException("Arquivo de log inválido");

        var caminho = Path.Combine(options.Diretorio, nome);
        if (!File.Exists(caminho)) throw new InvalidOperationException($"Arquivo de log não encontrado: {nome}");

        // O Serilog mantém o arquivo do dia aberto para escrita; sem compartilhar a escrita
        // a abertura falharia justamente no arquivo mais consultado.
        return new FileStream(caminho, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
    }

    /// <summary>
    /// Só nomes no formato que o sink gera (prefixo + yyyyMMdd + _NNN opcional + extensão).
    /// Também é o que impede path traversal: barras e ".." nunca casam.
    /// </summary>
    private bool NomeValido(string nome) =>
        Regex.IsMatch(nome, $@"^{Regex.Escape(options.Prefixo)}\d{{8}}(_\d{{3,}})?{Regex.Escape(options.Extensao)}$");

    private static async IAsyncEnumerable<EntradaLogDTO> LerEntradas(TextReader leitor,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default) {

        EntradaLogDTO? atual = null;
        var detalhes = new StringBuilder();
        var numero = 0;

        EntradaLogDTO Fechar(EntradaLogDTO e) {
            var texto = detalhes.ToString().TrimEnd('\r', '\n');
            detalhes.Clear();
            return texto.Length == 0 ? e : e with { Detalhes = texto };
        }

        while (await leitor.ReadLineAsync(ct) is { } linha) {
            var m = Cabecalho().Match(linha);
            if (m.Success) {
                if (atual is not null) yield return Fechar(atual);
                atual = new EntradaLogDTO {
                    Numero = ++numero,
                    DataHora = m.Groups[1].Value,
                    Nivel = m.Groups[2].Value,
                    TraceId = m.Groups[3].Value.Length == 0 ? null : m.Groups[3].Value,
                    Origem = m.Groups[4].Value.Length == 0 ? null : m.Groups[4].Value,
                    Mensagem = m.Groups[5].Value,
                };
            } else if (atual is not null) {
                detalhes.AppendLine(linha);
            } else if (linha.Length > 0) {
                // Linha solta antes do primeiro cabeçalho (outro formato, arquivo truncado):
                // vira entrada própria em vez de sumir.
                yield return new EntradaLogDTO { Numero = ++numero, Mensagem = linha };
            }
        }

        if (atual is not null) yield return Fechar(atual);
    }

    /// <summary>Filtros já validados e normalizados, aplicados a cada entrada.</summary>
    private sealed partial class Criterios {
        private DateTimeOffset? _inicio;
        private DateTimeOffset? _fim;
        private HashSet<string>? _niveis;
        private List<string> _incluir = [];
        private List<string> _excluir = [];
        private string? _traceId;

        // Palavras soltas, "frases entre aspas" e -exclusões (inclusive -"frase").
        [GeneratedRegex(@"(-?)(?:""([^""]+)""|(\S+))")]
        private static partial Regex Termo();

        public static Criterios De(FiltroLogs filtro) {
            if (filtro.Inicio > filtro.Fim) throw new InvalidOperationException("A data inicial é posterior à final");

            var c = new Criterios {
                _inicio = filtro.Inicio,
                _fim = filtro.Fim,
                _traceId = string.IsNullOrWhiteSpace(filtro.TraceId) ? null : filtro.TraceId.Trim(),
            };

            if (filtro.Niveis is { Count: > 0 }) {
                c._niveis = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var n in filtro.Niveis.Select(n => n.Trim()).Where(n => n.Length > 0)) {
                    var canonico = Array.Find(Niveis, x => string.Equals(x, n, StringComparison.OrdinalIgnoreCase))
                        ?? throw new InvalidOperationException($"Nível de log inválido: {n}");
                    c._niveis.Add(canonico);
                }
                if (c._niveis.Count == 0) c._niveis = null;
            }

            if (!string.IsNullOrWhiteSpace(filtro.Busca)) {
                foreach (Match m in Termo().Matches(filtro.Busca)) {
                    var texto = m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value;
                    (m.Groups[1].Length > 0 ? c._excluir : c._incluir).Add(texto);
                }
            }

            return c;
        }

        public bool Passa(EntradaLogDTO e) {
            if (_niveis is not null && (e.Nivel is null || !_niveis.Contains(e.Nivel))) return false;
            if (_traceId is not null && !string.Equals(e.TraceId, _traceId, StringComparison.OrdinalIgnoreCase)) return false;

            if (_inicio is not null || _fim is not null) {
                if (!DateTimeOffset.TryParseExact(e.DataHora, FormatoDataHora, CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var momento)) return false;
                if (momento < _inicio || momento > _fim) return false;
            }

            foreach (var t in _incluir) if (!Contem(e, t)) return false;
            foreach (var t in _excluir) if (Contem(e, t)) return false;
            return true;
        }

        private static bool Contem(EntradaLogDTO e, string termo) =>
            e.Mensagem.Contains(termo, StringComparison.OrdinalIgnoreCase)
            || (e.Detalhes?.Contains(termo, StringComparison.OrdinalIgnoreCase) ?? false)
            || (e.Origem?.Contains(termo, StringComparison.OrdinalIgnoreCase) ?? false);
    }
}
