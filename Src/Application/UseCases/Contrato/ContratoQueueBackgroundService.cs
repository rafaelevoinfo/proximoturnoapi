using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class ContratoQueueBackgroundService : BackgroundService
{
    private readonly IContratoQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ContratoQueueBackgroundService> _logger;
    private readonly TimeSpan[] _delaysRetentativa;

    public ContratoQueueBackgroundService(
        IContratoQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<ContratoQueueBackgroundService> logger,
        TimeSpan[]? delaysRetentativa = null)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _delaysRetentativa = delaysRetentativa ?? new[]
        {
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(15)
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ContratoQueueBackgroundService iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var job = await _queue.DesenfileirarAsync(stoppingToken);
                _logger.LogInformation("Job de geração de contrato para o pedido {IdPedido} retirado da fila.", job.IdPedido);

                // Executa de forma sequencial (concorrência controlada = 1) para proteção de conexões e rate limits
                await ProcessarJobAsync(job, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no loop principal do serviço de fila de contratos.");
                
                // Evita busy loop (consumo excessivo de CPU) em caso de falha persistente imprevista
                try
                {
                    await Task.Delay(1000, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("ContratoQueueBackgroundService finalizado.");
    }

    private async Task ProcessarJobAsync(ContratoJob job, CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            
            if (job.InativarExistente)
            {
                var contratoRepository = scope.ServiceProvider.GetRequiredService<IContratoRepository>();
                _logger.LogInformation("Inativando contratos ativos antigos do pedido {IdPedido} devido a atualização.", job.IdPedido);
                await contratoRepository.InativarContratosPorPedidoIdAsync(job.IdPedido);
            }

            var gerarContratoUseCase = scope.ServiceProvider.GetRequiredService<GerarContratoPedido>();

            var adminClaims = new ClaimsPrincipal(
                new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.Role, Roles.Admin) },
                    "SystemAuth"
                )
            );
            var contrato = await gerarContratoUseCase.ExecuteAsync(adminClaims, job.IdPedido);

            if (!gerarContratoUseCase.IsValid)
            {
                var erroNotificacao = gerarContratoUseCase.Notifications.FirstOrDefault();
                
                // Se o erro for de validação (ex: BadRequest/NotFound), não re-enfileiramos
                if (erroNotificacao?.Type == UseCaseNotificationType.BadRequest || 
                    erroNotificacao?.Type == UseCaseNotificationType.NotFound)
                {
                    _logger.LogWarning("Falha de validação ao gerar contrato do pedido {IdPedido}: {Erro}. Nenhuma retentativa será agendada.", 
                        job.IdPedido, gerarContratoUseCase.AggregateErrors());
                    return;
                }

                throw new Exception($"Falha de integração ao gerar contrato: {gerarContratoUseCase.AggregateErrors()}");
            }

            _logger.LogInformation("Contrato gerado e enviado com sucesso para o pedido {IdPedido}.", job.IdPedido);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar job de geração de contrato do pedido {IdPedido}. Tentativa atual: {Tentativas}", job.IdPedido, job.Tentativas);
            
            if (job.Tentativas < _delaysRetentativa.Length)
            {
                var delay = _delaysRetentativa[job.Tentativas];
                job.Tentativas++;

                _logger.LogInformation("Re-enfileirando geração de contrato do pedido {IdPedido} com delay de {Delay}.", job.IdPedido, delay);
                
                // Agenda o re-enfileiramento assíncrono
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(delay, stoppingToken);
                        _queue.Enfileirar(job.IdPedido, job.Tentativas, job.InativarExistente);
                    }
                    catch (OperationCanceledException)
                    {
                        // Ignorar cancelamento da aplicação
                    }
                }, stoppingToken);
            }
            else
            {
                _logger.LogError("Número máximo de tentativas de envio de contrato excedido para o pedido {IdPedido}.", job.IdPedido);
            }
        }
    }
}
