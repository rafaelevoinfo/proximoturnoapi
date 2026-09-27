using System.Text;
using System.Text.Json;

namespace ProximoTurnoApi.Infrastructure.IA;

/// <summary>
/// O que uma resposta em streaming (SSE) disse sobre o próprio uso, juntado pedaço a pedaço.
/// A OpenRouter manda tokens e custo em <c>usage</c> no último pedaço; modelo, provider e id
/// vêm em todos, e o <c>finish_reason</c> no penúltimo.
/// </summary>
public sealed class UsoSse {
    public string? Modelo { get; private set; }
    public string? Provider { get; private set; }
    public string? IdGeracao { get; private set; }
    public string? FinishReason { get; private set; }
    public bool TemUso { get; private set; }
    public int TokensEntrada { get; private set; }
    public int TokensSaida { get; private set; }
    public int TokensRaciocinio { get; private set; }
    public int TokensCache { get; private set; }
    public decimal? CustoUsd { get; private set; }

    /// <summary>Terminou com <c>data: [DONE]</c>: a resposta chegou inteira.</summary>
    public bool Concluido { get; private set; }

    /// <summary>Lê uma linha do SSE. Linha que não é JSON de dados é ignorada, nunca lança.</summary>
    public void LerLinha(string linha) {
        if (!linha.StartsWith("data:", StringComparison.Ordinal)) {
            return;
        }

        var dados = linha[5..].Trim();
        if (dados.Length == 0) {
            return;
        }

        if (dados == "[DONE]") {
            Concluido = true;
            return;
        }

        try {
            using var documento = JsonDocument.Parse(dados);
            var raiz = documento.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object) {
                return;
            }

            Modelo = Texto(raiz, "model") ?? Modelo;
            Provider = Texto(raiz, "provider") ?? Provider;
            IdGeracao = Texto(raiz, "id") ?? IdGeracao;

            if (raiz.TryGetProperty("choices", out var escolhas) && escolhas.ValueKind == JsonValueKind.Array) {
                foreach (var escolha in escolhas.EnumerateArray()) {
                    FinishReason = Texto(escolha, "finish_reason") ?? FinishReason;
                }
            }

            if (raiz.TryGetProperty("usage", out var uso) && uso.ValueKind == JsonValueKind.Object) {
                TemUso = true;
                TokensEntrada = Inteiro(uso, "prompt_tokens");
                TokensSaida = Inteiro(uso, "completion_tokens");
                TokensRaciocinio = Inteiro(Filho(uso, "completion_tokens_details"), "reasoning_tokens");
                TokensCache = Inteiro(Filho(uso, "prompt_tokens_details"), "cached_tokens");
                CustoUsd = uso.TryGetProperty("cost", out var custo) && custo.ValueKind == JsonValueKind.Number
                           && custo.TryGetDecimal(out var valor)
                    ? valor
                    : CustoUsd;
            }
        } catch (JsonException) {
            // Linha quebrada nao pode derrubar a resposta que o usuario esta lendo.
        }
    }

    private static JsonElement Filho(JsonElement pai, string propriedade) =>
        pai.ValueKind == JsonValueKind.Object && pai.TryGetProperty(propriedade, out var valor) ? valor : default;

    private static string? Texto(JsonElement objeto, string propriedade) =>
        objeto.ValueKind == JsonValueKind.Object
        && objeto.TryGetProperty(propriedade, out var valor)
        && valor.ValueKind == JsonValueKind.String
            ? valor.GetString()
            : null;

    private static int Inteiro(JsonElement objeto, string propriedade) =>
        objeto.ValueKind == JsonValueKind.Object
        && objeto.TryGetProperty(propriedade, out var valor)
        && valor.ValueKind == JsonValueKind.Number
        && valor.TryGetInt32(out var numero)
            ? numero
            : 0;
}

/// <summary>
/// Envolve o corpo de uma resposta em streaming: repassa cada byte ao SDK sem alterar nada e,
/// no caminho, lê as linhas SSE para saber quanto a chamada custou. Quando o corpo acaba — lido
/// até o fim ou descartado antes disso — entrega o <see cref="UsoSse"/> uma única vez.
/// <para>
/// Só guarda a linha em andamento, nunca o corpo inteiro: uma resposta longa não vira memória.
/// </para>
/// </summary>
public sealed class StreamUsoLlm(Stream _interno, Action<UsoSse> _aoTerminar, Func<UsoSse, ValueTask> _aoTerminarAsync) : Stream {

    private readonly UsoSse _uso = new();
    private readonly Decoder _decodificador = Encoding.UTF8.GetDecoder();
    private readonly StringBuilder _linha = new();
    private int _entregue;

    public UsoSse Uso => _uso;

    public override int Read(byte[] buffer, int offset, int count) {
        var lidos = _interno.Read(buffer, offset, count);
        Observar(buffer.AsSpan(offset, lidos));
        if (lidos == 0) {
            Entregar();
        }

        return lidos;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
        var lidos = await _interno.ReadAsync(buffer, cancellationToken);
        Observar(buffer.Span[..lidos]);
        if (lidos == 0) {
            await EntregarAsync();
        }

        return lidos;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    private void Observar(ReadOnlySpan<byte> bytes) {
        if (bytes.IsEmpty) {
            return;
        }

        Span<char> caracteres = stackalloc char[Math.Min(bytes.Length, 1024)];
        while (!bytes.IsEmpty) {
            var pedaco = bytes[..Math.Min(bytes.Length, 1024)];
            var quantidade = _decodificador.GetChars(pedaco, caracteres, flush: false);
            foreach (var c in caracteres[..quantidade]) {
                if (c == '\n') {
                    _uso.LerLinha(_linha.ToString().TrimEnd('\r'));
                    _linha.Clear();
                } else {
                    _linha.Append(c);
                }
            }

            bytes = bytes[pedaco.Length..];
        }
    }

    private void FecharUltimaLinha() {
        if (_linha.Length > 0) {
            _uso.LerLinha(_linha.ToString().TrimEnd('\r'));
            _linha.Clear();
        }
    }

    private void Entregar() {
        if (Interlocked.Exchange(ref _entregue, 1) == 0) {
            FecharUltimaLinha();
            _aoTerminar(_uso);
        }
    }

    private async ValueTask EntregarAsync() {
        if (Interlocked.Exchange(ref _entregue, 1) == 0) {
            FecharUltimaLinha();
            await _aoTerminarAsync(_uso);
        }
    }

    protected override void Dispose(bool disposing) {
        // Descartado antes do fim: o usuario fechou a tela ou o SDK parou de ler. O que chegou
        // ate aqui ainda precisa virar linha no ledger.
        if (disposing) {
            Entregar();
            _interno.Dispose();
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync() {
        await EntregarAsync();
        await _interno.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    public override bool CanRead => _interno.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
