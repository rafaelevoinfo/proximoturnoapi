using System;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Services;

namespace ProximoTurnoApi.Domain;

public enum StatusPedido : short {
    Pendente,
    Entregue,
    Devolvido,
    Cancelado
}

public class Pedido : BaseModel {
    public Cliente Cliente { get; }
    public int? IdPedidoOriginal { get; init; }
    //Em caso de renovações, estara preenchido com o pedido que gerou a renovacao
    public Pedido? PedidoOriginal { get; init; }
    public DateTime DataHora { get; init; }
    public DateTime? DataHoraEntrega { get; private set; }
    private decimal _valorTotal;
    public decimal ValorTotal {
        get => _valorTotal;
        private set => _valorTotal = value;
    }

    public int? IdCupom { get; private set; }
    public Cupom? Cupom { get; private set; }

    public decimal ValorDesconto { get; private set; } = 0;
    public StatusPedido Status { get; private set; }
    public string? MetodoPagamento { get; private set; }
    public string? MetodoEntrega { get; private set; }
    public DateTime? DataHoraAlteracao { get; private set; }
    private List<ItemPedido> _items = [];
    public IReadOnlyList<ItemPedido> Items {
        get => _items.AsReadOnly();
        private set => _items = [.. value];
    }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    private Pedido() {

    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    public Pedido(Cliente cliente, string? metodoPagamento = null, string? metodoEntrega = null) {
        if (cliente is null) {
            throw new Exception("Não é possível criar um pedido sem um cliente");
        }
        Cliente = cliente;
        DataHora = DateTime.Now;
        DataHoraAlteracao = DataHora;
        MetodoPagamento = metodoPagamento;
        MetodoEntrega = metodoEntrega;
    }

    public DateTime CalcularDataDevolucao(int qtdeDias) {
        var dataBase = DataHoraEntrega ?? DataHora;
        return dataBase.Date
            .AddDays(qtdeDias)
            .AddHours(23)
            .AddMinutes(59)
            .AddSeconds(59);
    }

    public bool AdicionarItem(ItemPedido item) {
        Clear();
        if (Status != StatusPedido.Pendente) {
            AddNotification("ERRO", $"Não é possivel adicionar novos items a um pedido no status {Status}");
        }
        if (item.JogoCopia is null) {
            AddNotification("ERRO", $"O jogo não foi selecionado");
        } else {
            if (item.JogoCopia.Status != StatusJogo.Disponivel) {
                AddNotification("ERRO", $"O jogo {item.JogoCopia.Jogo?.Nome} não esta disponível para aluguel");
            }
            if (_items.Any(i => i.JogoCopia.IdJogo == item.JogoCopia.IdJogo)) {
                AddNotification("ERRO", $"O jogo {item.JogoCopia.Jogo?.Nome} já foi adicionado a este pedido");
            }

            item.JogoCopia.Status = StatusJogo.Reservado;
        }

        if (!IsValid) {
            return false;
        }
        item.Status = StatusPedido.Pendente;
        _items.Add(item);
        CalcularTotal();
        RegistrarAlteracao();

        return true;
    }


    public bool RemoverItem(int idItemPedido) {
        Clear();
        if (Status != StatusPedido.Pendente) {
            AddNotification("ERRO", $"Não é possivel remover items de um pedido no status {Status}");
        }
        for (var i = _items.Count - 1; i >= 0; i--) {
            if (_items[i].Id == idItemPedido) {
                _items[i].JogoCopia.Status = StatusJogo.Disponivel;
                _items.RemoveAt(i);
                CalcularTotal();
                RegistrarAlteracao();
                return true;
            }
        }
        CalcularTotal();
        return false;
    }

    public bool RemoverItem(ItemPedido item) {
        Clear();
        if (Status != StatusPedido.Pendente) {
            AddNotification("ERRO", $"Não é possivel remover items de um pedido no status {Status}");
        }
        item.JogoCopia.Status = StatusJogo.Disponivel;
        var result = _items.Remove(item);
        CalcularTotal();
        if (result) {
            RegistrarAlteracao();
        }
        return result;
    }

    public bool Entregar(ICategoriaPeriodoCache cache, DateTime? dataDevolucao = null) {
        Clear();
        if (Status != StatusPedido.Pendente) {
            AddNotification("ERRO", $"Somente um pedido no status {StatusPedido.Pendente} pode ser entregue.");
            return false;
        }
        if (dataDevolucao.HasValue && dataDevolucao.Value.Date <= DateTime.Now.Date) {
            AddNotification("ERRO", "A data de devolução informada deve ser superior à data atual.");
            return false;
        }

        Status = StatusPedido.Entregue;
        DataHoraEntrega = DateTime.Now;

        foreach (var item in _items) {
            item.Status = StatusPedido.Entregue;
            item.DataDevolucao = dataDevolucao.HasValue
                ? dataDevolucao.Value.Date.AddHours(23).AddMinutes(59).AddSeconds(59)
                : CalcularDataDevolucao(cache.GetQuantidadeDias(item.IdPeriodo));
            item.JogoCopia.Status = StatusJogo.Alugado;
        }
        RegistrarAlteracao();
        return true;
    }

    public bool Cancelar() {
        Clear();
        if (Status != StatusPedido.Pendente) {
            AddNotification("ERRO", $"Somente um pedido no status {StatusPedido.Pendente} pode ser cancelado.");
            return false;
        }
        Status = StatusPedido.Cancelado;
        foreach (var item in _items) {
            item.Status = StatusPedido.Cancelado;
            item.JogoCopia.Status = StatusJogo.Disponivel;
        }
        RegistrarAlteracao();
        return true;
    }

    public Pedido? Renovar(List<(int idItem, CategoriaPeriodoInfo periodo)?> itensRenovar, ICategoriaPeriodoCache cache) {
        Clear();
        if (Status != StatusPedido.Entregue) {
            AddNotification("ERRO", "Para renovar, o pedido precisa estar entregue");
            return null;
        }

        var novoPedido = new Pedido(Cliente) {
            DataHora = DateTime.Now,
            Status = StatusPedido.Pendente,
            DataHoraEntrega = DateTime.Now,
            PedidoOriginal = this
        };

        foreach (var item in _items) {
            var itemRenovar = itensRenovar.FirstOrDefault(i => i.HasValue && i.Value.idItem == item.Id);
            if (itemRenovar is not null && item.Status == StatusPedido.Entregue) {
                item.JogoCopia.Status = StatusJogo.Disponivel; // libera para AdicionarItem revalidar e reservar
                var novoItem = new ItemPedido() {
                    IdJogoCopia = item.IdJogoCopia,
                    JogoCopia = item.JogoCopia,
                    IdPeriodo = itemRenovar.Value.periodo.IdPeriodo,
                    Valor = itemRenovar.Value.periodo.Valor,
                    DataDevolucao = novoPedido.CalcularDataDevolucao(itemRenovar.Value.periodo.QuantidadeDias)
                };
                if (novoPedido.AdicionarItem(novoItem)) {
                    item.Status = StatusPedido.Devolvido; // fecha a perna antiga do item renovado
                }
            }
        }

        if (novoPedido.Items.Count == 0) {
            AddNotification("ERRO", "Nenhum item válido para renovação");
            return null;
        }

        novoPedido.CalcularTotal();
        novoPedido.Entregar(cache);

        RecalcularStatus();
        RegistrarAlteracao();
        return novoPedido;
    }

    public bool Devolver(List<int>? idsItemsDevolvidos) {
        Clear();
        if (Status != StatusPedido.Entregue) {
            AddNotification("ERRO", "Não é possível devolver um pedido não entregue");
            return false;
        }
        var qtdeDevolvida = 0;
        foreach (var item in _items) {
            var deveDevolver = idsItemsDevolvidos is null || idsItemsDevolvidos.Count == 0 || idsItemsDevolvidos.Contains(item.Id);
            if (deveDevolver && item.Status == StatusPedido.Entregue) {
                item.Status = StatusPedido.Devolvido;
                item.JogoCopia.Status = StatusJogo.Disponivel;
                qtdeDevolvida++;
            }
        }
        if (qtdeDevolvida == 0) {
            AddNotification("ERRO", "Nenhum item foi devolvido");
            return false;
        }
        RecalcularStatus();
        RegistrarAlteracao();
        return true;
    }


    public void DefinirMetodos(string? metodoPagamento, string? metodoEntrega) {
        MetodoPagamento = metodoPagamento;
        MetodoEntrega = metodoEntrega;
        CalcularTotal();
        RegistrarAlteracao();
    }

    private void RegistrarAlteracao() {
        DataHoraAlteracao = DateTime.Now;
    }

    private void RecalcularStatus() {
        if (_items.Count == 0) {
            return;
        }
        if (_items.All(i => i.Status == StatusPedido.Cancelado)) {
            Status = StatusPedido.Cancelado;
            return;
        }
        var ativos = _items.Where(i => i.Status != StatusPedido.Cancelado);
        if (ativos.Any(i => i.Status == StatusPedido.Pendente)) {
            Status = StatusPedido.Pendente;
        } else if (ativos.Any(i => i.Status == StatusPedido.Entregue)) {
            Status = StatusPedido.Entregue;
        } else {
            Status = StatusPedido.Devolvido;
        }
    }

    public void AplicarCupom(int idCupom, decimal valorDesconto) {
        if (IdCupom != null) {
            AddNotification("PEDIDO", "Já existe um cupom aplicado a este pedido.");
            return;
        }
        IdCupom = idCupom;
        ValorDesconto = valorDesconto;
        CalcularTotal();
        RegistrarAlteracao();
    }

    public void RemoverCupom() {
        IdCupom = null;
        ValorDesconto = 0;
        CalcularTotal();
        RegistrarAlteracao();
    }

    public const decimal TAXA_ENTREGA = 8.0m;

    public void CalcularTotal() {
        decimal taxa = string.Equals(MetodoEntrega, "entrega", StringComparison.OrdinalIgnoreCase) ? TAXA_ENTREGA : 0;
        _valorTotal = Math.Max(0, _items.Sum(i => i.Valor) + taxa - ValorDesconto);
    }
}