using System;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Domain;

public enum StatusPedido : short {
    Pendente,
    Entregue,
    Devolvido,
    Cancelado
}

public class Pedido : BaseModel {
    public Cliente Cliente { get; }
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

    public bool Entregar() {
        Clear();
        if (Status != StatusPedido.Pendente) {
            AddNotification("ERRO", $"Somente um pedido no status {StatusPedido.Pendente} pode ser entregue.");
            return false;
        }
        Status = StatusPedido.Entregue;
        DataHoraEntrega = DateTime.Now;
        foreach (var item in _items) {
            item.JogoCopia.Status = StatusJogo.Alugado;
        }
        RegistrarAlteracao();
        return true;
    }

    public bool Cancelar() {
        Clear();
        Status = StatusPedido.Cancelado;
        foreach (var item in _items) {
            item.JogoCopia.Status = StatusJogo.Disponivel;
        }
        RegistrarAlteracao();
        return true;
    }

    public Pedido? Renovar(List<(int idItem, CategoriaPeriodo periodo)?> itensRenovar) {
        Clear();

        if (Status != StatusPedido.Devolvido) {
            AddNotification("ERRO", "Para renovar um pedido, ele primeiro precisa ser devolvido e então renovado");
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
            if (itemRenovar is not null) {
                item.JogoCopia.Status = StatusJogo.Disponivel; // Permite que AdicionarItem valide e reserve a cópia
                var novoItem = new ItemPedido() {
                    IdJogoCopia = item.IdJogoCopia,
                    JogoCopia = item.JogoCopia,
                    IdPeriodo = itemRenovar.Value.periodo.Id,
                    Valor = itemRenovar.Value.periodo.Valor,
                    DataDevolucao = novoPedido.CalcularDataDevolucao(itemRenovar.Value.periodo.QuantidadeDias),
                    Renovado = true
                };
                novoPedido.AdicionarItem(novoItem);
            }
        }
        novoPedido.CalcularTotal();
        novoPedido.Entregar();

        RegistrarAlteracao();
        return novoPedido;
    }

    public bool Devolver(List<int>? idsItemsDevolvidos) {
        if (Status != StatusPedido.Entregue) {
            AddNotification("ERRO", "Não é possível devolver um pedido não entregue");
            return false;
        }
        var qtdeDevolvida = 0;
        foreach (var item in _items) {
            if (idsItemsDevolvidos is null || idsItemsDevolvidos.Count == 0 || idsItemsDevolvidos.Any(idItem => idItem == item.Id)) {
                item.JogoCopia.Status = StatusJogo.Disponivel;
                qtdeDevolvida++;
            }
        }
        if (qtdeDevolvida == 0) {
            AddNotification("ERRO", "Nenhum item foi devolvido");
            return false;
        }

        Status = StatusPedido.Devolvido;

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