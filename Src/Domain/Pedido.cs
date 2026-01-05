using System.ComponentModel.DataAnnotations.Schema;
using Flunt.Notifications;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Domain;

public enum StatusPedido : short {
    Criado,
    Entregue,
    Cancelado
}

public class Pedido : Notifiable<Notification> {
    public int Id { get; }
    public Cliente Cliente { get; }
    //Em caso de renovações, estara preenchido com o pedido que gerou a renovacao
    public Pedido? PedidoOriginal { get; init; }
    public DateTime DataHora { get; init; }
    public DateTime? DataHoraEntrega { get; private set; }
    private decimal _valorTotal;
    public decimal ValorTotal {
        get {
            _valorTotal = Items.Sum(i => i.Valor);
            return _valorTotal;
        }
        private set => _valorTotal = value;
    }
    public StatusPedido Status { get; private set; }
    private List<ItemPedido> _items = [];
    public IReadOnlyList<ItemPedido> Items {
        get => _items.AsReadOnly();
        private set => _items = [.. value];
    }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    private Pedido() {

    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    public Pedido(Cliente cliente) {
        if (cliente is null) {
            throw new Exception("Não é possível criar um pedido sem um cliente");
        }
        Cliente = cliente;
        DataHora = DateTime.UtcNow;
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
        if (Status != StatusPedido.Criado) {
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

        return true;
    }

    public bool RemoverItem(int idItemPedido) {
        Clear();
        if (Status != StatusPedido.Criado) {
            AddNotification("ERRO", $"Não é possivel remover items de um pedido no status {Status}");
        }
        for (var i = _items.Count - 1; i >= 0; i--) {
            if (_items[i].Id == idItemPedido) {
                _items.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    public bool RemoverItem(ItemPedido item) {
        Clear();
        if (Status != StatusPedido.Criado) {
            AddNotification("ERRO", $"Não é possivel remover items de um pedido no status {Status}");
        }
        return _items.Remove(item);
    }

    public bool Entregar() {
        Clear();
        if (Status != StatusPedido.Criado) {
            AddNotification("ERRO", $"Somente um pedido no status Criado pode ser entregue.");
            return false;
        }
        Status = StatusPedido.Entregue;
        DataHoraEntrega = DateTime.UtcNow;
        foreach (var item in _items) {
            item.JogoCopia.Status = StatusJogo.Alugado;
        }
        return true;
    }

    public bool Cancelar() {
        Clear();
        Status = StatusPedido.Cancelado;
        foreach (var item in _items) {
            item.JogoCopia.Status = StatusJogo.Disponivel;
        }
        return true;
    }

    public Pedido? Renovar(List<(int idItem, Periodo periodo)?> itensRenovar) {
        Clear();
        if (Status != StatusPedido.Entregue) {
            AddNotification("ERRO", "Não é possível renovar um pedido não entregue");
            return null;
        }
        var novoPedido = new Pedido(Cliente) {
            DataHora = DateTime.UtcNow,
            DataHoraEntrega = DataHora,
            PedidoOriginal = this
        };
        foreach (var item in _items) {
            var itemRenovar = itensRenovar.FirstOrDefault(i => i.HasValue && i.Value.idItem == item.Id);
            if (itemRenovar is not null) {
                var novoItem = new ItemPedido() {
                    IdJogoCopia = item.IdJogoCopia,
                    JogoCopia = item.JogoCopia,
                    Valor = itemRenovar.Value.periodo.Valor,
                    DataDevolucao = CalcularDataDevolucao(itemRenovar.Value.periodo.QuantidadeDias),
                    Renovado = true
                };
                novoPedido.AdicionarItem(novoItem);
            } else {
                item.JogoCopia.Status = StatusJogo.Disponivel;
            }
        }
        return novoPedido;
    }

    public bool Devolver(List<int>? idsItemsDevolvidos) {
        if (Status != StatusPedido.Entregue) {
            AddNotification("ERRO", "Não é possível devolver um pedido não entregue");
            return false;
        }
        var qtdeDevolvida = 0;
        foreach (var item in _items) {
            if (idsItemsDevolvidos is null || idsItemsDevolvidos.Any(idItem => idItem == item.Id)) {
                item.JogoCopia.Status = StatusJogo.Disponivel;
                qtdeDevolvida++;
            }
        }
        if (qtdeDevolvida == 0) {
            AddNotification("ERRO", "Nenhum item foi devolvido");
            return false;
        }
        return true;
    }
}