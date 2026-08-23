using System.Diagnostics;

namespace ProximoTurnoApi.Infrastructure.Logging;

/// <summary>
/// Abre um escopo de rastreamento para trabalho que roda fora de uma requisicao HTTP.
/// <para>
/// O Serilog preenche <c>{TraceId}</c> a partir de <see cref="Activity.Current"/>. Em
/// requisicoes o proprio ASP.NET Core cria essa Activity, e por isso os logs de
/// requisicao ja saem correlacionados. Inicializacao e servicos de background nao tem
/// Activity nenhuma — e e exatamente por isso que aquelas linhas saiam com
/// <c>[]</c> no lugar do trace id. Abrir uma Activity por unidade de trabalho
/// (um item da fila, uma execucao de backup) faz cada uma ganhar o seu proprio id.
/// </para>
/// </summary>
public static class RastreioBackground {
    private static readonly ActivitySource Fonte = new("ProximoTurnoApi");

    /// <summary>
    /// Inicia uma Activity para a unidade de trabalho. O retorno deve ser consumido
    /// com <c>using</c>, para que o escopo feche junto com a operacao.
    /// </summary>
    public static Activity Iniciar(string nome) {
        // Sem nenhum listener registrado (nao ha OpenTelemetry no projeto hoje) o
        // ActivitySource devolve null por otimizacao. O fallback manual garante o
        // trace id de qualquer forma, e o dia que entrar OpenTelemetry o caminho
        // de cima passa a valer sozinho, sem mexer aqui.
        return Fonte.StartActivity(nome, ActivityKind.Internal) ?? new Activity(nome).Start();
    }
}
