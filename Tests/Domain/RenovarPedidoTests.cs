using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Tests.Fakes;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class RenovarPedidoTests {
    private const string Pix = "pix";

    /// <summary>Devolve o resultado configurado e guarda o que recebeu, sem tocar em repositório.</summary>
    private sealed class FakeValidarCupom() : ValidarCupom(null!, null!, null!, new FakeLogger<ValidarCupom>()) {
        public ValidacaoCupomResultadoDTO Resultado { get; set; } = new() { Valido = true, IdCupom = 7, ValorDescontoCalculado = 10m };
        public List<ValidarCupomDTO> Recebidos { get; } = [];

        public override Task<ValidacaoCupomResultadoDTO> ExecuteAsync(ValidarCupomDTO dto) {
            Recebidos.Add(dto);
            return Task.FromResult(Resultado);
        }
    }

    private sealed record Cenario(RenovarPedido UseCase, FakePedidoRepository Repo, FakeContratoQueue Queue, FakeValidarCupom Cupom);

    /// <summary>Período 10: 7 dias, categoria 1. Período 20: 14 dias, categoria 1. Período 30: categoria 2.</summary>
    private static FakeCategoriaPeriodoCache Cache() =>
        new FakeCategoriaPeriodoCache().Adicionar(10, 7, 50m, 1).Adicionar(20, 14, 80m, 1).Adicionar(30, 7, 60m, 2);

    private static Cenario Criar(FakeCategoriaPeriodoCache cache) {
        var repo = new FakePedidoRepository();
        var queue = new FakeContratoQueue();
        var cupom = new FakeValidarCupom();
        return new Cenario(new RenovarPedido(repo, cache, cupom, queue, new FakeLogger<RenovarPedido>()), repo, queue, cupom);
    }

    private static Pedido PedidoEntregue(FakeCategoriaPeriodoCache cache, int idPedido = 1, int idCliente = 1) {
        var copia = PedidoTestFactory.Copia(idCopia: 1, idJogo: 5, idCategoria: 1);
        var pedido = PedidoTestFactory.PedidoPendenteComItem(PedidoTestFactory.Cliente(id: idCliente), copia, idPeriodo: 10, valor: 50m, qtdeDias: 7, idItem: 1, idPedido: idPedido);
        pedido.Entregar(cache);
        return pedido;
    }

    private static Pedido PedidoEntregueComDoisItens(FakeCategoriaPeriodoCache cache, int idPedido = 1) {
        var pedido = new Pedido(PedidoTestFactory.Cliente()) { Id = idPedido };
        pedido.AdicionarItem(new ItemPedido { Id = 1, IdPeriodo = 10, Valor = 50m, JogoCopia = PedidoTestFactory.Copia(idCopia: 1, idJogo: 5, idCategoria: 1) });
        pedido.AdicionarItem(new ItemPedido { Id = 2, IdPeriodo = 10, Valor = 50m, JogoCopia = PedidoTestFactory.Copia(idCopia: 2, idJogo: 6, idCategoria: 1) });
        pedido.Entregar(cache);
        return pedido;
    }

    private static void Atrasar(Pedido pedido, int idItem = 1) =>
        pedido.Items.Single(i => i.Id == idItem).DataDevolucao = DateTime.Now.Date.AddDays(-1).AddHours(23).AddMinutes(59).AddSeconds(59);

    private static RenovarPedidoDTO Renovacao(params ItemPedidoRenovarDTO[] itens) =>
        new() { Itens = [.. itens], MetodoPagamento = Pix };

    private static Pedido NovoPedido(FakePedidoRepository repo) => repo.Pedidos.Single(p => p.PedidoOriginal != null);

    private static DateTime FimDoDia(DateTime data) => data.Date.AddHours(23).AddMinutes(59).AddSeconds(59);

    private static void AssertNadaGravado(Cenario c) {
        Assert.Equal(0, c.Repo.SaveCount);
        Assert.Empty(c.Queue.Enfileirados);
        Assert.DoesNotContain(c.Repo.Pedidos, p => p.PedidoOriginal != null);
    }

    [Fact]
    public async Task ExecuteAsync_QuandoPedidoNaoExiste_AdicionaNotificacao() {
        var c = Criar(Cache());

        await c.UseCase.ExecuteAsync(SolicitantePedido.Administrador, 999, Renovacao(new ItemPedidoRenovarDTO { Id = 1 }));

        Assert.False(c.UseCase.IsValid);
        Assert.Contains(c.UseCase.Notifications, n => n.Message == "Pedido não encontrado.");
    }

    [Fact]
    public async Task ExecuteAsync_QuandoNenhumItemInformado_AdicionaNotificacao() {
        var cache = Cache();
        var c = Criar(cache);
        c.Repo.Pedidos.Add(PedidoEntregue(cache));

        await c.UseCase.ExecuteAsync(SolicitantePedido.Administrador, 1, Renovacao());

        Assert.False(c.UseCase.IsValid);
        AssertNadaGravado(c);
    }

    [Fact]
    public async Task ExecuteAsync_QuandoValido_GeraNovoPedidoSalvaEEnfileiraContrato() {
        var cache = Cache();
        var c = Criar(cache);
        c.Repo.Pedidos.Add(PedidoEntregue(cache));

        await c.UseCase.ExecuteAsync(SolicitantePedido.Administrador, 1, Renovacao(new ItemPedidoRenovarDTO { Id = 1 }));

        Assert.True(c.UseCase.IsValid);
        // pedido original (devolvido) + novo pedido (renovado)
        Assert.Equal(2, c.Repo.SaveCount);
        var novo = NovoPedido(c.Repo);
        Assert.Equal(StatusPedido.Entregue, novo.Status);
        Assert.Equal(Pix, novo.MetodoPagamento);
        Assert.Null(novo.MetodoEntrega);
        Assert.Equal([novo.Id], c.Queue.Enfileirados);
    }

    [Fact]
    public async Task ExecuteAsync_ComPeriodoTrocado_CalculaDevolucaoPorHojeMaisDiasDoNovoPeriodo() {
        var cache = Cache();
        var c = Criar(cache);
        c.Repo.Pedidos.Add(PedidoEntregue(cache));

        await c.UseCase.ExecuteAsync(SolicitantePedido.DoCliente(1), 1, Renovacao(new ItemPedidoRenovarDTO { Id = 1, IdPeriodo = 20 }));

        Assert.True(c.UseCase.IsValid);
        var item = NovoPedido(c.Repo).Items.Single();
        Assert.Equal(20, item.IdPeriodo);
        Assert.Equal(80m, item.Valor);
        Assert.Equal(FimDoDia(DateTime.Now.AddDays(14)), item.DataDevolucao);
    }

    [Fact]
    public async Task ExecuteAsync_RenovacaoParcial_ItemNaoInformadoContinuaEntregueNoOriginal() {
        var cache = Cache();
        var c = Criar(cache);
        c.Repo.Pedidos.Add(PedidoEntregueComDoisItens(cache));

        await c.UseCase.ExecuteAsync(SolicitantePedido.Administrador, 1, Renovacao(new ItemPedidoRenovarDTO { Id = 1 }));

        Assert.True(c.UseCase.IsValid);
        var original = c.Repo.Pedidos.First(p => p.Id == 1);
        Assert.Equal(StatusPedido.Entregue, original.Status);
        Assert.Equal(StatusPedido.Devolvido, original.Items.Single(i => i.Id == 1).Status);
        Assert.Equal(StatusPedido.Entregue, original.Items.Single(i => i.Id == 2).Status);
        Assert.Single(NovoPedido(c.Repo).Items);
    }

    [Fact]
    public async Task ExecuteAsync_ItemDeOutroPedido_RejeitaENaoGrava() {
        var cache = Cache();
        var c = Criar(cache);
        c.Repo.Pedidos.Add(PedidoEntregue(cache));

        await c.UseCase.ExecuteAsync(SolicitantePedido.Administrador, 1,
            Renovacao(new ItemPedidoRenovarDTO { Id = 1 }, new ItemPedidoRenovarDTO { Id = 99 }));

        Assert.False(c.UseCase.IsValid);
        Assert.Contains(c.UseCase.Notifications, n => n.Message.Contains("item 99"));
        AssertNadaGravado(c);
    }

    [Fact]
    public async Task ExecuteAsync_ItemJaDevolvido_RejeitaENaoGrava() {
        var cache = Cache();
        var c = Criar(cache);
        var pedido = PedidoEntregueComDoisItens(cache);
        pedido.Devolver([2]);
        c.Repo.Pedidos.Add(pedido);

        await c.UseCase.ExecuteAsync(SolicitantePedido.Administrador, 1, Renovacao(new ItemPedidoRenovarDTO { Id = 2 }));

        Assert.False(c.UseCase.IsValid);
        AssertNadaGravado(c);
    }

    [Fact]
    public async Task ExecuteAsync_ItemRepetido_RejeitaENaoGrava() {
        var cache = Cache();
        var c = Criar(cache);
        c.Repo.Pedidos.Add(PedidoEntregue(cache));

        await c.UseCase.ExecuteAsync(SolicitantePedido.Administrador, 1,
            Renovacao(new ItemPedidoRenovarDTO { Id = 1 }, new ItemPedidoRenovarDTO { Id = 1, IdPeriodo = 20 }));

        Assert.False(c.UseCase.IsValid);
        AssertNadaGravado(c);
    }

    [Theory]
    [InlineData(999)] // não existe
    [InlineData(30)]  // existe, mas é de outra categoria
    public async Task ExecuteAsync_PeriodoInvalidoParaOJogo_RejeitaENaoGrava(int idPeriodo) {
        var cache = Cache();
        var c = Criar(cache);
        c.Repo.Pedidos.Add(PedidoEntregue(cache));

        await c.UseCase.ExecuteAsync(SolicitantePedido.Administrador, 1, Renovacao(new ItemPedidoRenovarDTO { Id = 1, IdPeriodo = idPeriodo }));

        Assert.False(c.UseCase.IsValid);
        AssertNadaGravado(c);
    }

    [Fact]
    public async Task ExecuteAsync_SemFormaDePagamento_RejeitaENaoGrava() {
        var cache = Cache();
        var c = Criar(cache);
        c.Repo.Pedidos.Add(PedidoEntregue(cache));

        await c.UseCase.ExecuteAsync(SolicitantePedido.Administrador, 1,
            new RenovarPedidoDTO { Itens = [new ItemPedidoRenovarDTO { Id = 1 }] });

        Assert.Contains(c.UseCase.Notifications, n => n.Message == "Informe a forma de pagamento.");
        AssertNadaGravado(c);
    }

    [Fact]
    public async Task ExecuteAsync_ClienteInformandoDataNoItem_NegaENaoGrava() {
        var cache = Cache();
        var c = Criar(cache);
        c.Repo.Pedidos.Add(PedidoEntregue(cache));

        await c.UseCase.ExecuteAsync(SolicitantePedido.DoCliente(1), 1,
            Renovacao(new ItemPedidoRenovarDTO { Id = 1, DataDevolucao = DateTime.Now.Date.AddDays(30) }));

        Assert.Contains(c.UseCase.Notifications, n => n.Type == UseCaseNotificationType.Forbid && n.Message == RenovarPedido.MensagemDataSoAdmin);
        AssertNadaGravado(c);
    }

    [Fact]
    public async Task ExecuteAsync_ClienteInformandoDataGlobal_NegaENaoGrava() {
        var cache = Cache();
        var c = Criar(cache);
        c.Repo.Pedidos.Add(PedidoEntregue(cache));
        var dto = Renovacao(new ItemPedidoRenovarDTO { Id = 1 }) with { DataDevolucao = DateTime.Now.Date.AddDays(30) };

        await c.UseCase.ExecuteAsync(SolicitantePedido.DoCliente(1), 1, dto);

        Assert.Contains(c.UseCase.Notifications, n => n.Type == UseCaseNotificationType.Forbid);
        AssertNadaGravado(c);
    }

    [Fact]
    public async Task ExecuteAsync_AdminComDataGlobalEDataPorItem_DataDoItemPrevalece() {
        var cache = Cache();
        var c = Criar(cache);
        c.Repo.Pedidos.Add(PedidoEntregueComDoisItens(cache));
        var global = DateTime.Now.Date.AddDays(20);
        var doItem = DateTime.Now.Date.AddDays(45);
        var dto = Renovacao(new ItemPedidoRenovarDTO { Id = 1, DataDevolucao = doItem }, new ItemPedidoRenovarDTO { Id = 2 }) with { DataDevolucao = global };

        await c.UseCase.ExecuteAsync(SolicitantePedido.Administrador, 1, dto);

        Assert.True(c.UseCase.IsValid);
        var novo = NovoPedido(c.Repo);
        Assert.Equal(FimDoDia(doItem), novo.Items.Single(i => i.JogoCopia.IdJogo == 5).DataDevolucao);
        Assert.Equal(FimDoDia(global), novo.Items.Single(i => i.JogoCopia.IdJogo == 6).DataDevolucao);
    }

    [Fact]
    public async Task ExecuteAsync_AdminComDataDeHojeNoItem_RejeitaENaoGrava() {
        var cache = Cache();
        var c = Criar(cache);
        c.Repo.Pedidos.Add(PedidoEntregue(cache));

        await c.UseCase.ExecuteAsync(SolicitantePedido.Administrador, 1, Renovacao(new ItemPedidoRenovarDTO { Id = 1, DataDevolucao = DateTime.Now.Date }));

        Assert.False(c.UseCase.IsValid);
        AssertNadaGravado(c);
    }

    [Fact]
    public async Task ExecuteAsync_AdminComDataGlobalNoPassado_RejeitaENaoGrava() {
        var cache = Cache();
        var c = Criar(cache);
        c.Repo.Pedidos.Add(PedidoEntregue(cache));
        var dto = Renovacao(new ItemPedidoRenovarDTO { Id = 1 }) with { DataDevolucao = DateTime.Now.Date.AddDays(-3) };

        await c.UseCase.ExecuteAsync(SolicitantePedido.Administrador, 1, dto);

        Assert.Contains(c.UseCase.Notifications, n => n.Message == "A data de devolução informada deve ser superior à data atual.");
        AssertNadaGravado(c);
    }

    [Fact]
    public async Task ExecuteAsync_ClienteComPedidoAtrasado_NegaENaoGrava() {
        var cache = Cache();
        var c = Criar(cache);
        var pedido = PedidoEntregueComDoisItens(cache);
        Atrasar(pedido, idItem: 2); // o atraso de qualquer item bloqueia o pedido inteiro
        c.Repo.Pedidos.Add(pedido);

        await c.UseCase.ExecuteAsync(SolicitantePedido.DoCliente(1), 1, Renovacao(new ItemPedidoRenovarDTO { Id = 1 }));

        Assert.Contains(c.UseCase.Notifications, n => n.Type == UseCaseNotificationType.Forbid && n.Message == RenovarPedido.MensagemAtraso);
        AssertNadaGravado(c);
    }

    [Fact]
    public async Task ExecuteAsync_AdminComPedidoAtrasado_Renova() {
        var cache = Cache();
        var c = Criar(cache);
        var pedido = PedidoEntregue(cache);
        Atrasar(pedido);
        c.Repo.Pedidos.Add(pedido);

        await c.UseCase.ExecuteAsync(SolicitantePedido.Administrador, 1, Renovacao(new ItemPedidoRenovarDTO { Id = 1 }));

        Assert.True(c.UseCase.IsValid);
        Assert.Single(c.Queue.Enfileirados);
    }

    [Fact]
    public async Task ExecuteAsync_ClienteComDevolucaoVencendoHoje_Renova() {
        var cache = Cache();
        var c = Criar(cache);
        var pedido = PedidoEntregue(cache);
        pedido.Items.Single().DataDevolucao = FimDoDia(DateTime.Now);
        c.Repo.Pedidos.Add(pedido);

        await c.UseCase.ExecuteAsync(SolicitantePedido.DoCliente(1), 1, Renovacao(new ItemPedidoRenovarDTO { Id = 1 }));

        Assert.True(c.UseCase.IsValid);
    }

    [Fact]
    public async Task ExecuteAsync_CupomValido_AplicaDescontoNoNovoPedidoComRegrasNormais() {
        var cache = Cache();
        var c = Criar(cache);
        c.Repo.Pedidos.Add(PedidoEntregue(cache, idCliente: 3));
        var dto = Renovacao(new ItemPedidoRenovarDTO { Id = 1, IdPeriodo = 20 }) with { CupomCodigo = "DESC10" };

        await c.UseCase.ExecuteAsync(SolicitantePedido.DoCliente(3), 1, dto);

        Assert.True(c.UseCase.IsValid);
        var novo = NovoPedido(c.Repo);
        Assert.Equal(7, novo.IdCupom);
        Assert.Equal(10m, novo.ValorDesconto);

        // Validado como um pedido novo do cliente: sem IdPedido, então a renovação conta como uso.
        var validado = Assert.Single(c.Cupom.Recebidos);
        Assert.Equal("DESC10", validado.Codigo);
        Assert.Equal(3, validado.IdCliente);
        Assert.Null(validado.IdPedido);
        var item = Assert.Single(validado.Itens);
        Assert.Equal((5, 20), (item.IdJogo, item.IdPeriodo));
    }

    [Fact]
    public async Task ExecuteAsync_CupomInvalido_RejeitaComMensagemDoCupomENaoGrava() {
        var cache = Cache();
        var c = Criar(cache);
        c.Repo.Pedidos.Add(PedidoEntregue(cache));
        c.Cupom.Resultado = new ValidacaoCupomResultadoDTO { Valido = false, Mensagem = "Limite de uso do cupom atingido." };
        var dto = Renovacao(new ItemPedidoRenovarDTO { Id = 1 }) with { CupomCodigo = "UNICO" };

        await c.UseCase.ExecuteAsync(SolicitantePedido.DoCliente(1), 1, dto);

        Assert.Contains(c.UseCase.Notifications, n => n.Message == "Limite de uso do cupom atingido.");
        AssertNadaGravado(c);
    }

    [Fact]
    public async Task ExecuteAsync_QuandoPedidoDeOutroCliente_NegaENaoRenova() {
        var cache = Cache();
        var c = Criar(cache);
        var pedido = PedidoEntregue(cache);
        c.Repo.Pedidos.Add(pedido);

        await c.UseCase.ExecuteAsync(SolicitantePedido.DoCliente(2), 1, Renovacao(new ItemPedidoRenovarDTO { Id = 1 }));

        Assert.Contains(c.UseCase.Notifications, n => n.Type == UseCaseNotificationType.Forbid);
        AssertNadaGravado(c);
        Assert.Equal(StatusPedido.Entregue, pedido.Status);
    }
}
