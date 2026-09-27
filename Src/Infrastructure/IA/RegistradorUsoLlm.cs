using System.Diagnostics;
using ProximoTurnoApi.Application.UseCases.IA;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Infrastructure.IA;

/// <summary>
/// Junta o que a policy mediu no fio com o que só o ambiente sabe — a que manual o gasto
/// pertence — e grava. Singleton, porque quem o chama são os clientes de LLM, que também são.
/// </summary>
public class RegistradorUsoLlm(ILogger<RegistradorUsoLlm> _logger,
                               IServiceScopeFactory _scopeFactory,
                               ICotacaoDolar _cotacao) : IRegistradorUsoLlm {

    public async Task RegistrarAsync(RegistroUsoLlm registro) {
        try {
            // Escopo proprio porque o DbContext e scoped e quem chama e singleton, igual ao
            // que o IndexacaoManuaisWorker faz por item da fila.
            using var scope = _scopeFactory.CreateScope();
            var repositorio = scope.ServiceProvider.GetRequiredService<IUsoLlmRepository>();

            await repositorio.RegistrarAsync(Montar(registro, _cotacao.Atual.Valor));
            Logar(registro);
        } catch (Exception excecao) {
            Falhou(excecao, registro);
        }
    }

    public void Registrar(RegistroUsoLlm registro) {
        try {
            using var scope = _scopeFactory.CreateScope();
            var repositorio = scope.ServiceProvider.GetRequiredService<IUsoLlmRepository>();

            repositorio.Registrar(Montar(registro, _cotacao.Atual.Valor));
            Logar(registro);
        } catch (Exception excecao) {
            Falhou(excecao, registro);
        }
    }

    /// <summary>
    /// Monta a linha. Todo texto é cortado no tamanho da coluna: no MySQL em modo estrito,
    /// um corpo de erro comprido derrubaria o insert e perderíamos justamente a linha do erro.
    /// <para>
    /// Com usuário no escopo, o gasto é convertido para reais agora, com a cotação recebida:
    /// é o que sai do crédito dele. A cotação vem por parâmetro, e não é buscada aqui, porque
    /// este caminho não pode fazer I/O externo nem lançar.
    /// </para>
    /// </summary>
    public static UsoLlm Montar(RegistroUsoLlm registro, decimal? cotacaoUsdBrl = null) {
        var alvo = EscopoUsoLlm.Atual;
        var comUsuario = alvo?.IdUsuario is not null && cotacaoUsdBrl is not null;

        return new UsoLlm {
            Momento = DateTime.Now,
            TraceId = Activity.Current?.TraceId.ToString(),
            Operacao = registro.Operacao,
            IdUsuario = Cortar(alvo?.IdUsuario, 255),
            IdJogo = alvo?.IdJogo,
            IdJogoLink = alvo?.IdJogoLink,
            Alvo = Cortar(alvo?.Alvo, 200),
            ModeloPedido = Cortar(registro.ModeloPedido, 100)!,
            ModeloRespondeu = Cortar(registro.ModeloRespondeu, 100),
            Provider = Cortar(registro.Provider, 60),
            TokensEntrada = registro.TokensEntrada,
            TokensSaida = registro.TokensSaida,
            TokensRaciocinio = registro.TokensRaciocinio,
            TokensCache = registro.TokensCache,
            CustoUsd = registro.CustoUsd,
            CustoBrl = comUsuario ? CustoUsdEfetivo(registro) * cotacaoUsdBrl : null,
            CotacaoUsdBrl = comUsuario ? cotacaoUsdBrl : null,
            DuracaoMs = registro.DuracaoMs,
            Desfecho = registro.Desfecho,
            Detalhe = Cortar(registro.Detalhe, 300),
            IdGeracao = Cortar(registro.IdGeracao, 80),
        };
    }

    /// <summary>
    /// O custo que a OpenRouter informou ou, sem ele, a estimativa pelos tokens. Estima pelo
    /// modelo pedido: o que respondeu vem com sufixo de versão e não bate com a tabela.
    /// </summary>
    public static decimal CustoUsdEfetivo(RegistroUsoLlm registro) =>
        registro.CustoUsd
        ?? IAModel.EstimarCustoUsd(registro.ModeloPedido, registro.TokensEntrada, registro.TokensSaida);

    private static string? Cortar(string? valor, int tamanho) =>
        valor is null || valor.Length <= tamanho ? valor : valor[..tamanho];

    private void Logar(RegistroUsoLlm registro) =>
        _logger.LogInformation(
            "LLM {Operacao} em {Modelo}: {Entrada}+{Saida} tokens, US$ {Custo}, {Duracao}ms, {Desfecho}.",
            registro.Operacao, registro.ModeloRespondeu ?? registro.ModeloPedido,
            registro.TokensEntrada, registro.TokensSaida, registro.CustoUsd,
            registro.DuracaoMs, registro.Desfecho);

    private void Falhou(Exception excecao, RegistroUsoLlm registro) =>
        _logger.LogWarning(excecao, "Falha ao registrar uso de LLM ({Operacao}, {Modelo}): {Mensagem}",
                           registro.Operacao, registro.ModeloPedido, excecao.Message);
}
