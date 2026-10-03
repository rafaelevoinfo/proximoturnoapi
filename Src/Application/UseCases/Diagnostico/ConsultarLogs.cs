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

    // Ordem dos níveis do Serilog, para o filtro "deste nível para cima".
    private static readonly string[] Niveis = ["Verbose", "Debug", "Information", "Warning", "Error", "Fatal"];

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

    /// <param name="arquivo">Nome do arquivo; null usa o mais recente.</param>
    /// <param name="nivelMinimo">Nível do Serilog (ex.: "Warning"); null traz todos.</param>
    /// <param name="busca">Texto procurado, sem diferenciar maiúsculas, na mensagem, detalhes e origem.</param>
    public async Task<ConsultaLogsDTO> ExecuteAsync(string? arquivo, string? nivelMinimo, string? busca,
        string? traceId, int? limite, CancellationToken ct = default) {

        var nome = string.IsNullOrWhiteSpace(arquivo) ? ListarArquivos().FirstOrDefault()?.Nome : arquivo.Trim();
        if (nome is null) return new ConsultaLogsDTO();

        var indiceNivel = -1;
        if (!string.IsNullOrWhiteSpace(nivelMinimo)) {
            indiceNivel = Array.FindIndex(Niveis, n => string.Equals(n, nivelMinimo.Trim(), StringComparison.OrdinalIgnoreCase));
            if (indiceNivel < 0) throw new InvalidOperationException($"Nível de log inválido: {nivelMinimo}");
        }

        var max = Math.Clamp(limite ?? LimitePadrao, 1, LimiteMaximo);
        busca = string.IsNullOrWhiteSpace(busca) ? null : busca.Trim();
        traceId = string.IsNullOrWhiteSpace(traceId) ? null : traceId.Trim();

        // Guarda só as últimas `max` entradas que passam no filtro: o arquivo pode ter
        // até 10 MB e não precisa ficar inteiro em memória.
        var ultimas = new Queue<EntradaLogDTO>(max);
        var total = 0;

        using var leitor = new StreamReader(Abrir(nome), Encoding.UTF8);
        await foreach (var entrada in LerEntradas(leitor, ct)) {
            if (!Passa(entrada, indiceNivel, busca, traceId)) continue;
            total++;
            if (ultimas.Count == max) ultimas.Dequeue();
            ultimas.Enqueue(entrada);
        }

        return new ConsultaLogsDTO {
            Arquivo = nome,
            TotalEncontrado = total,
            Entradas = ultimas.Reverse().ToList(),
        };
    }

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

    private static bool Passa(EntradaLogDTO e, int indiceNivel, string? busca, string? traceId) {
        if (indiceNivel >= 0 && Array.IndexOf(Niveis, e.Nivel) < indiceNivel) return false;
        if (traceId is not null && !string.Equals(e.TraceId, traceId, StringComparison.OrdinalIgnoreCase)) return false;
        if (busca is not null
            && !e.Mensagem.Contains(busca, StringComparison.OrdinalIgnoreCase)
            && !(e.Detalhes?.Contains(busca, StringComparison.OrdinalIgnoreCase) ?? false)
            && !(e.Origem?.Contains(busca, StringComparison.OrdinalIgnoreCase) ?? false)) return false;
        return true;
    }

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
}
