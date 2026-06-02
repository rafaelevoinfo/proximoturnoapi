using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class ObterRelatorioFaturamento(DatabaseContext dbContext) : UseCaseBasico
{
    public async Task<DashboardReportDTO> ExecuteAsync(DateOnly? dataInicial, DateOnly? dataFinal)
    {
        var now = DateTime.Today;
        var defaultStart = DateOnly.FromDateTime(now.AddDays(-30));
        var defaultEnd = DateOnly.FromDateTime(now);

        var start = dataInicial ?? defaultStart;
        var end = dataFinal ?? defaultEnd;

        var startDateTime = start.ToDateTime(TimeOnly.MinValue);
        var endDateTimeNextDay = end.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var query = dbContext.Pedidos
            .Where(p => (p.Status == StatusPedido.Entregue || p.Status == StatusPedido.Devolvido)
                     && p.DataHoraEntrega >= startDateTime
                     && p.DataHoraEntrega < endDateTimeNextDay);

        var totalRecebido = await query.SumAsync(p => p.ValorTotal);

        var faturamentoPorMesRaw = await query
            .GroupBy(p => new { Year = p.DataHoraEntrega!.Value.Year, Month = p.DataHoraEntrega!.Value.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Valor = g.Sum(p => p.ValorTotal)
            })
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .ToListAsync();

        var faturamentoPorMes = faturamentoPorMesRaw
            .Select(x => new FaturamentoMensalDTO
            {
                MesAno = $"{x.Month:D2}/{x.Year}",
                Valor = x.Valor
            })
            .ToList();

        var faturamentoPorJogo = await query
            .SelectMany(p => p.Items)
            .GroupBy(i => new { JogoId = i.JogoCopia.Jogo!.Id, NomeJogo = i.JogoCopia.Jogo!.Nome })
            .Select(g => new FaturamentoJogoDTO
            {
                JogoId = g.Key.JogoId,
                NomeJogo = g.Key.NomeJogo,
                ValorGerado = g.Sum(i => i.Valor)
            })
            .ToListAsync();

        var topClientes = await query
            .GroupBy(p => new { ClienteId = p.Cliente.Id, NomeCliente = p.Cliente.Nome })
            .Select(g => new TopClienteDTO
            {
                ClienteId = g.Key.ClienteId,
                NomeCliente = g.Key.NomeCliente,
                ValorGasto = g.Sum(p => p.ValorTotal),
                QuantidadeJogosAlugados = g.SelectMany(p => p.Items).Count()
            })
            .OrderByDescending(c => c.ValorGasto)
            .Take(5)
            .ToListAsync();

        return new DashboardReportDTO
        {
            TotalRecebido = totalRecebido,
            FaturamentoPorMes = faturamentoPorMes,
            FaturamentoPorJogo = faturamentoPorJogo,
            TopClientes = topClientes
        };
    }
}
