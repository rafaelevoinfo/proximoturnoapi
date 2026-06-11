using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.DTOs.Filtros;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class ListarCupons(ICupomRepository _repository, ILogger<ListarCupons> logger) : UseCaseBasico
{
    public async Task<List<CupomDTO>> ExecuteAsync(FiltroCupomDTO filtro)
    {
        logger.LogInformation("Listando cupons com filtros - Search: '{Search}', ApenasAtivos: {ApenasAtivos}", filtro.Search, filtro.ApenasAtivos);
        var cupons = await _repository.GetAllAsync(filtro);
        return cupons.Select(CupomDTO.FromModel).ToList();
    }
}
