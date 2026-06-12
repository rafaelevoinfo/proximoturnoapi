using System.Collections.Generic;
using System.Threading.Tasks;
using ProximoTurnoApi.Application.DTOs.Filtros;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Infrastructure.Repositories;

public interface ICupomRepository : IBaseRepository
{
    Task<Cupom?> GetByIdAsync(int id);
    Task<Cupom?> GetByCodigoAsync(string codigo);
    Task<List<Cupom>> GetAllAsync(FiltroCupomDTO filtro);
    Task<int> GetUsoCountGlobalAsync(int cupomId, int? idPedidoExcluir = null);
    Task<int> GetUsoCountClienteAsync(int cupomId, int clienteId, int? idPedidoExcluir = null);
    Task SaveAsync(Cupom cupom, bool commit = true);
}
