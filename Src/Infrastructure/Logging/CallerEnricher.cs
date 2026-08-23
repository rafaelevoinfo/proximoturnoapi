using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Serilog.Core;
using Serilog.Events;

namespace ProximoTurnoApi.Infrastructure.Logging;

/// <summary>
/// Preenche a propriedade <c>Caller</c> com a origem do log, em dois niveis de
/// precisao conforme a severidade do evento.
/// <para>
/// A partir de <see cref="NivelPadrao"/> (Warning) vale a pena descobrir
/// <c>Classe.Metodo</c> caminhando a pilha de chamadas: e onde se esta depurando
/// algo, e o metodo exato economiza a busca. Abaixo disso, so a classe, tirada do
/// <c>SourceContext</c> que o <c>ILogger&lt;T&gt;</c> ja preencheu — uma fatia de
/// string, sem pilha nenhuma.
/// </para>
/// <para>
/// A divisao existe por custo medido: a captura da pilha custa entre 4us/5KB
/// (pilha rasa) e 25us/18KB (pilha profunda de request) por evento, e uma pilha
/// de request ASP.NET Core fica na ponta cara. Como o EF Core loga cada
/// DbCommand em Information, pagar isso em todo evento sairia caro em alocacao.
/// </para>
/// <para>
/// O <c>[CallerMemberName]</c> seria mais barato e mais preciso, mas so pode ser
/// aplicado em parametro de metodo proprio: exigiria um wrapper em todos os 300+
/// pontos de log e, ainda assim, nao alcancaria nada do que vem de dentro do
/// EF Core, do ASP.NET Core ou do Identity — que e a maior parte do arquivo.
/// </para>
/// </summary>
public sealed class CallerEnricher : ILogEventEnricher {
    public const string NomePropriedade = "Caller";

    /// <summary>
    /// Severidade a partir da qual o nome do metodo compensa o custo da pilha.
    /// </summary>
    public const LogEventLevel NivelPadrao = LogEventLevel.Warning;

    private readonly LogEventLevel _nivelMinimo;

    public CallerEnricher(LogEventLevel nivelMinimo = NivelPadrao) => _nivelMinimo = nivelMinimo;

    /// <summary>
    /// Namespaces que sao sempre intermediarios entre quem logou e este enricher,
    /// nunca a origem que interessa. Sem eles o log mostraria "EventDefinition.Log"
    /// em todo log do EF Core e "ExecutionContext.RunInternal" nos callbacks do host,
    /// em vez do metodo que de fato originou a mensagem.
    /// </summary>
    private static readonly string[] NamespacesIgnorados = [
        "Serilog",
        "Microsoft.Extensions.Logging",
        "Microsoft.EntityFrameworkCore.Diagnostics",
        "System.Threading",
        "System.Runtime.CompilerServices",
        "ProximoTurnoApi.Infrastructure.Logging"
    ];

    /// <summary>
    /// A resolucao envolve reflexao (atributos, tipos aninhados, formatacao), mas o
    /// resultado e fixo por metodo. O cache faz esse custo ser pago uma vez por
    /// ponto de log, e nao a cada evento.
    /// </summary>
    private static readonly ConcurrentDictionary<MethodBase, string> Cache = new();

    /// <summary>
    /// Cache do nome curto por <c>SourceContext</c>: sao poucas categorias distintas
    /// e a fatia da string se paga uma vez por categoria, nao por evento.
    /// </summary>
    private static readonly ConcurrentDictionary<string, string> CacheSourceContext = new();

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory) {
        var caller = logEvent.Level >= _nivelMinimo
            ? DescobrirOrigem() ?? ClasseDoSourceContext(logEvent)
            : ClasseDoSourceContext(logEvent);

        if (caller is null) return;

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(NomePropriedade, caller));
    }

    /// <summary>
    /// Ultimo segmento do <c>SourceContext</c>, que e o nome da classe sem o
    /// namespace. Para categorias do framework o segmento final ja basta, porque a
    /// mensagem em si diz o resto (<c>Database.Command</c> vira <c>Command</c>, e
    /// a linha continua sendo "Executed DbCommand ...").
    /// </summary>
    private static string? ClasseDoSourceContext(LogEvent logEvent) {
        if (!logEvent.Properties.TryGetValue("SourceContext", out var valor)) return null;
        if (valor is not ScalarValue { Value: string contexto } || contexto.Length == 0) return null;

        return CacheSourceContext.GetOrAdd(contexto, static c => {
            var ponto = c.LastIndexOf('.');

            return ponto >= 0 && ponto < c.Length - 1 ? c[(ponto + 1)..] : c;
        });
    }

    /// <summary>
    /// Primeiro frame da pilha que nao pertence a infraestrutura de log.
    /// </summary>
    private static string? DescobrirOrigem() {
        // fNeedFileInfo: false e o que mantem a captura barata — ler PDB para
        // arquivo/linha custaria ordens de grandeza mais.
        var pilha = new StackTrace(skipFrames: 2, fNeedFileInfo: false);

        for (var i = 0; i < pilha.FrameCount; i++) {
            var metodo = pilha.GetFrame(i)?.GetMethod();
            if (metodo?.DeclaringType is null) continue;
            if (EhInfraestruturaDeLog(metodo.DeclaringType)) continue;

            return Cache.GetOrAdd(metodo, Formatar);
        }

        return null;
    }

    private static bool EhInfraestruturaDeLog(Type tipo) {
        var ns = tipo.Namespace;
        if (ns is null) return false;

        foreach (var ignorado in NamespacesIgnorados) {
            if (ns.Length == ignorado.Length && ns == ignorado) return true;
            if (ns.Length > ignorado.Length && ns[ignorado.Length] == '.' && ns.StartsWith(ignorado, StringComparison.Ordinal)) return true;
        }

        return false;
    }

    /// <summary>
    /// Monta <c>Classe.Metodo</c> desfazendo o que o compilador gerou: metodos
    /// <c>async</c> viram <c>&lt;Metodo&gt;d__12.MoveNext()</c> e lambdas viram
    /// <c>&lt;&gt;c.&lt;Metodo&gt;b__12_0()</c>. Sem esse tratamento o log mostraria
    /// "MoveNext" em praticamente todo lugar, ja que o projeto e quase todo async.
    /// </summary>
    private static string Formatar(MethodBase metodo) {
        var tipo = metodo.DeclaringType!;
        var nomeMetodo = NomeEntreAngulares(metodo.Name) ?? metodo.Name;

        // Sobe pelos tipos aninhados gerados pelo compilador ate a classe escrita
        // a mao, aproveitando o nome do metodo embutido no nome do tipo.
        while (tipo.DeclaringType is not null && tipo.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)) {
            nomeMetodo = NomeEntreAngulares(tipo.Name) ?? nomeMetodo;
            tipo = tipo.DeclaringType;
        }

        return string.Concat(NomeSimples(tipo), ".", nomeMetodo);
    }

    /// <summary>
    /// Trecho entre <c>&lt;</c> e <c>&gt;</c>, que e onde o compilador preserva o
    /// nome original do metodo. Retorna <c>null</c> quando nao ha nada util
    /// (o caso de <c>&lt;&gt;c</c>, por exemplo).
    /// <para>
    /// O casamento precisa ser balanceado e recursivo porque lambdas assincronas
    /// aninham duas camadas: a state machine da lambda vira
    /// <c>&lt;&lt;Metodo&gt;b__4_0&gt;d</c>, e parar no primeiro <c>&gt;</c>
    /// devolveria <c>&lt;Metodo</c> em vez de <c>Metodo</c>.
    /// </para>
    /// </summary>
    private static string? NomeEntreAngulares(string nome) {
        var abre = nome.IndexOf('<');
        if (abre < 0) return null;

        var profundidade = 0;
        for (var i = abre; i < nome.Length; i++) {
            if (nome[i] == '<') {
                profundidade++;
                continue;
            }

            if (nome[i] != '>') continue;

            if (--profundidade > 0) continue;
            if (i <= abre + 1) return null;

            var conteudo = nome[(abre + 1)..i];

            return conteudo[0] == '<' ? NomeEntreAngulares(conteudo) : conteudo;
        }

        return null;
    }

    /// <summary>
    /// Nome do tipo sem a aridade generica (<c>Repository`1</c> vira <c>Repository</c>).
    /// </summary>
    private static string NomeSimples(Type tipo) {
        var nome = tipo.Name;
        var crase = nome.IndexOf('`');

        return crase > 0 ? nome[..crase] : nome;
    }
}
