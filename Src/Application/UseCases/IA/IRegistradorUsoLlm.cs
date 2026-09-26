using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.UseCases.IA;

/// <summary>
/// O que uma chamada de LLM gastou, do ponto de vista de quem mediu o fio. O alvo, o trace
/// id e o momento não entram aqui: vêm do ambiente na hora de gravar.
/// </summary>
public sealed record RegistroUsoLlm(
    OperacaoLlm Operacao,
    string ModeloPedido,
    string? ModeloRespondeu,
    string? Provider,
    int TokensEntrada,
    int TokensSaida,
    int TokensRaciocinio,
    int TokensCache,
    decimal? CustoUsd,
    int DuracaoMs,
    DesfechoLlm Desfecho,
    string? Detalhe,
    string? IdGeracao);

public interface IRegistradorUsoLlm {

    /// <summary>
    /// Nunca lança: falhar aqui não pode derrubar uma chamada paga que já aconteceu. Não
    /// recebe <c>CancellationToken</c> de propósito — o gasto existe mesmo que a aplicação
    /// esteja desligando, e um desligamento não pode apagar o registro dele.
    /// </summary>
    Task RegistrarAsync(RegistroUsoLlm registro);

    /// <summary>Caminho síncrono, para o override sync da policy. Nunca lança.</summary>
    void Registrar(RegistroUsoLlm registro);
}
