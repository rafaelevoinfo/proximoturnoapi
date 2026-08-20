using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Infrastructure.Repositories;

public interface IPedidoRepository : IBaseRepository {
    Task SaveAsync(Pedido pedido, bool commit = true);
    Task<List<Pedido>> GetAllAsync(FiltroPedidoDTO filtro);
    Task<Pedido?> GetByIdAsync(int id);
    Task<bool> DeleteAsync(int id);
}

public class PedidoRepository(DatabaseContext dbContext) : BaseRepository(dbContext), IPedidoRepository {

    public async Task<List<Pedido>> GetAllAsync(FiltroPedidoDTO filtro) {
        var query = _dbContext.Pedidos
            .Include(p => p.Cliente)
            .Include(p => p.Cupom)
            .Include(p => p.Items)!
                .ThenInclude(j => j.JogoCopia)
                    .ThenInclude(jc => jc.Jogo)
            .AsQueryable();

        if (filtro.IdCliente.HasValue) {
            query = query.Where(p => p.Cliente.Id == filtro.IdCliente.Value);
        }

        if (filtro.DataInicial.HasValue) {
            var dataInicial = filtro.DataInicial.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(p => p.DataHora >= dataInicial);
        }

        if (filtro.DataFinal.HasValue) {
            var dataFinal = filtro.DataFinal.Value.ToDateTime(new TimeOnly(23, 59, 59));
            query = query.Where(p => p.DataHora <= dataFinal);
        }

        // Cada situacao marcada vira um ramo do OU: o pedido entra no resultado se
        // atender a pelo menos uma delas. "Entregue" exclui os vencidos, que sao
        // trazidos pelo ramo de atrasados.
        // Os booleanos sao calculados aqui fora para virarem parametros do SQL,
        // em vez de depender do EF traduzir List.Contains sobre enum.
        var querPendente = filtro.Status?.Contains(StatusPedido.Pendente) == true;
        var querEntregue = filtro.Status?.Contains(StatusPedido.Entregue) == true;
        var querDevolvido = filtro.Status?.Contains(StatusPedido.Devolvido) == true;
        var querCancelado = filtro.Status?.Contains(StatusPedido.Cancelado) == true;
        var querAtrasado = filtro.Atrasados;

        if (querPendente || querEntregue || querDevolvido || querCancelado || querAtrasado) {
            // nao vou considerar horas, apenas dias
            var hoje = DateTime.Today;
            query = query.Where(p =>
                (querPendente && p.Status == StatusPedido.Pendente) ||
                (querDevolvido && p.Status == StatusPedido.Devolvido) ||
                (querCancelado && p.Status == StatusPedido.Cancelado) ||
                (querEntregue && p.Status == StatusPedido.Entregue &&
                    !p.Items!.Any(i => i.Status == StatusPedido.Entregue && i.DataDevolucao.Date < hoje)) ||
                (querAtrasado &&
                    p.Items!.Any(i => i.Status == StatusPedido.Entregue && i.DataDevolucao.Date < hoje)));
        }

        return await query
            .AsNoTracking()
            .OrderByDescending(p => p.Id)
            .ToListAsync();
    }

    public async Task<Pedido?> GetByIdAsync(int id) {
        return await _dbContext.Pedidos
            .Include(p => p.Cliente)
            .Include(p => p.Cupom)
            .Include(p => p.Items)!
                .ThenInclude(j => j.JogoCopia)
                    .ThenInclude(jc => jc.Jogo)
            .AsTracking()
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<bool> DeleteAsync(int id) {
        return await _dbContext.Pedidos
            .Where(p => p.Id == id)
            .ExecuteDeleteAsync() > 0;
    }

    public async Task SaveAsync(Pedido pedido, bool commit) {
        await SaveChangesAsync(_dbContext.Pedidos, pedido, commit);
    }
}
