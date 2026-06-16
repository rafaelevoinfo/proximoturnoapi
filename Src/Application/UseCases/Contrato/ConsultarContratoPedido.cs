using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;
using ProximoTurnoApi.Infrastructure.Services;

namespace ProximoTurnoApi.Application.UseCases;

public class ConsultarContratoPedido(
    IContratoRepository contratoRepository,
    IAutentiqueService autentiqueService,
    ILogger<ConsultarContratoPedido> logger) : UseCaseBasico {

    /// <summary>
    /// Consulta o contrato de um pedido. Se o contrato estiver pendente, consulta o status
    /// atualizado no Autentique e atualiza o registro local se necessário.
    /// </summary>
    public async Task<ContratoAutentique?> ExecuteAsync(int idPedido) {
        var contrato = await contratoRepository.GetByPedidoIdAsync(idPedido);
        if (contrato is null) {
            AddNotification(UseCaseNotification.Create(
                UseCaseNotificationType.NotFound,
                "Nenhum contrato encontrado para este pedido."));
            return null;
        }

        // Se já está em estado final, retorna direto
        if (contrato.Status != StatusContrato.Pendente) {
            return contrato;
        }

        // Consulta status atualizado no Autentique
        try {
            var status = await autentiqueService.ConsultarDocumentoAsync(contrato.AutentiqueDocumentId);

            if (status.SignedAt is not null) {
                contrato.Status = StatusContrato.Assinado;
                contrato.DataAssinatura = DateTime.Now;
                await contratoRepository.SaveAsync(contrato);
                logger.LogInformation("Contrato do pedido {IdPedido} marcado como assinado via consulta", idPedido);
            } else if (status.RejectedAt is not null) {
                contrato.Status = StatusContrato.Rejeitado;
                await contratoRepository.SaveAsync(contrato);
                logger.LogInformation("Contrato do pedido {IdPedido} marcado como rejeitado via consulta", idPedido);
            }

            // Atualiza o link se mudou
            if (status.SigningLink is not null && status.SigningLink != contrato.LinkAssinatura) {
                contrato.LinkAssinatura = status.SigningLink;
                await contratoRepository.SaveAsync(contrato);
            }
        } catch (Exception ex) {
            // Log mas não falha - retorna o que temos no banco
            logger.LogWarning(ex, "Não foi possível consultar status do contrato no Autentique para o pedido {IdPedido}", idPedido);
        }

        return contrato;
    }
}
