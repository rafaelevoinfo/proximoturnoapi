using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Models;

using ProximoTurnoApi.Infrastructure.Repositories;

public interface IPedidoAluguelRepository {
    Task<List<Pedido>> GetAllAsync();
    Task<Pedido?> GetByIdAsync(int id);
    Task AddAsync(Pedido pedidoAluguel);
    Task UpdateAsync(Pedido pedidoAluguel);
    Task DeleteAsync(int id);
    Task DevolverJogosPedidoAsync(int pedidoId);
    Task DevolverJogoAvulsoAsync(int pedidoId, int jogoId);

}

public class PedidoAluguelRepository : IPedidoAluguelRepository {
    private readonly DatabaseContext _context;

    public PedidoAluguelRepository(DatabaseContext context) {
        _context = context;
    }

    public async Task<List<Pedido>> GetAllAsync() {
        return await _context.Pedidos
            .Include(p => p.Cliente)
            .Include(p => p.Jogos)!
            .ThenInclude(j => j.Jogo)
            .ToListAsync();
    }

    public async Task<Pedido?> GetByIdAsync(int id) {
        return await _context.Pedidos
            .Include(p => p.Cliente)
            .Include(p => p.Jogos)!
            .ThenInclude(j => j.Jogo)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task AddAsync(Pedido pedidoAluguel) {
        _context.Pedidos.Add(pedidoAluguel);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Pedido pedidoAluguel) {
        _context.Entry(pedidoAluguel).State = EntityState.Modified;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id) {
        var pedidoAluguel = await _context.Pedidos.FindAsync(id);
        if (pedidoAluguel != null) {
            _context.Pedidos.Remove(pedidoAluguel);
            await _context.SaveChangesAsync();
        }
    }

    public async Task DevolverJogosPedidoAsync(int pedidoId) {
        var pedido = await GetByIdAsync(pedidoId);
        if (pedido != null && pedido.Jogos != null) {
            foreach (var pedidoJogo in pedido.Jogos) {
                pedidoJogo.Status = PedidoJogoStatus.Devolvido;
                var jogo = await _context.Jogos.FindAsync(pedidoJogo.IdJogo);
                if (jogo != null) {
                    jogo.Status = JogoStatus.Disponivel;
                }
            }
            await _context.SaveChangesAsync();
        }
    }

    public async Task DevolverJogoAvulsoAsync(int pedidoId, int jogoId) {
        // var pedidoJogo = await _context.Pedidos
        //     .FirstOrDefaultAsync(pj => pj.IdPedido == pedidoId && pj.IdJogo == jogoId);

        // if (pedidoJogo != null) {
        //     pedidoJogo.Status = PedidoJogoStatus.Devolvido;
        //     var jogo = await _context.Jogos.FindAsync(pedidoJogo.IdJogo);
        //     if (jogo != null) {
        //         jogo.Status = JogoStatus.Disponivel;
        //     }
        //     await _context.SaveChangesAsync();
        // }
    }

    // public async Task<Pedido?> RenovarPedidoAsync(RenovarPedidoRequest request) {
    //     var pedidoOriginal = await GetByIdAsync(request.PedidoId);
    //     if (pedidoOriginal == null) {
    //         return null;
    //     }

    //     var novoPedido = new Pedido {
    //         IdCliente = pedidoOriginal.IdCliente,
    //         Data = DateTime.Now,
    //         Jogos = new List<PedidoJogo>()
    //     };

    //     if (pedidoOriginal.Jogos != null) {
    //         var jogosParaRenovar = request.Jogos ?? pedidoOriginal.Jogos.Select(j => new JogoRenovacao { JogoId = j.IdJogo, DataDevolucao = j.DataDevolucao, Valor = j.Valor }).ToList();

    //         foreach (var jogoRenovacao in jogosParaRenovar) {
    //             var jogoOriginal = pedidoOriginal.Jogos.FirstOrDefault(j => j.IdJogo == jogoRenovacao.JogoId);
    //             if (jogoOriginal != null) {
    //                 jogoOriginal.Status = PedidoJogoStatus.Renovado;

    //                 var novoJogo = new PedidoJogo {
    //                     IdJogo = jogoRenovacao.JogoId,
    //                     Valor = jogoRenovacao.Valor,
    //                     DataDevolucao = jogoRenovacao.DataDevolucao,
    //                     Status = PedidoJogoStatus.Alugado
    //                 };
    //                 novoPedido.Jogos.Add(novoJogo);
    //             }
    //         }
    //     }

    //     novoPedido.ValorTotal = novoPedido.Jogos.Sum(j => j.Valor);
    //     _context.PedidosAlugueis.Add(novoPedido);
    //     await _context.SaveChangesAsync();

    //     return novoPedido;
    // }
}
