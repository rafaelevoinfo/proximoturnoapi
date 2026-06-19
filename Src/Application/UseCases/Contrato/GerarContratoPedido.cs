using System;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;
using ProximoTurnoApi.Infrastructure.Services;

namespace ProximoTurnoApi.Application.UseCases;

public class GerarContratoPedido(
    IPedidoRepository pedidoRepository,
    IContratoRepository contratoRepository,
    IContratoPdfService contratoPdfService,
    IAutentiqueService autentiqueService,
    IConfiguration configuration,
    UserManager<Usuario> userManager,
    IClienteRepository clienteRepository,
    ILogger<GerarContratoPedido> logger) : UseCaseBasico {

    public async Task<ContratoAutentique?> ExecuteAsync(ClaimsPrincipal userClaim, int idPedido) {
        // 1. Verificar se já existe contrato para este pedido
        var contratoExistente = await contratoRepository.GetByPedidoIdAsync(idPedido);
        if (contratoExistente is not null) {
            // Validação de proprietário mesmo que já exista contrato
            if (!userClaim.IsInRole(Roles.Admin)) {
                var user = await userManager.GetUserAsync(userClaim);
                var idCliente = await clienteRepository.GetIdByEmailAsync(user?.Email ?? "");
                
                var pedidoExistente = contratoExistente.Pedido ?? await pedidoRepository.GetByIdAsync(idPedido);
                if (pedidoExistente == null || idCliente is null || idCliente.Value != pedidoExistente.Cliente?.Id) {
                    logger.LogWarning("Acesso negado: Usuário {UserEmail} tentou gerar contrato para o pedido {PedidoId}.", user?.Email, idPedido);
                    AddNotification(UseCaseNotification.Create(
                        UseCaseNotificationType.Forbid,
                        "Acesso negado: você não tem permissão para visualizar o contrato deste pedido."));
                    return null;
                }
            }
            logger.LogWarning("Tentativa de gerar contrato para o pedido {IdPedido}, mas já existe um contrato com ID {IdContrato}", idPedido, contratoExistente.Id);
            AddNotification(UseCaseNotification.Create(
                UseCaseNotificationType.BadRequest,
                "Já existe um contrato gerado para este pedido."));
            return contratoExistente;
        }

        // 2. Buscar o pedido completo
        var pedido = await pedidoRepository.GetByIdAsync(idPedido);
        if (pedido is null) {
            logger.LogWarning("Pedido com ID {IdPedido} não encontrado para geração de contrato.", idPedido);
            AddNotification(UseCaseNotification.Create(
                UseCaseNotificationType.NotFound,
                "Pedido não encontrado."));
            return null;
        }

        // Validação de proprietário do pedido
        if (!userClaim.IsInRole(Roles.Admin)) {
            var user = await userManager.GetUserAsync(userClaim);
            var idCliente = await clienteRepository.GetIdByEmailAsync(user?.Email ?? "");
            if (idCliente is null || idCliente.Value != pedido.Cliente?.Id) {
                logger.LogWarning("Acesso negado: Usuário {UserEmail} tentou gerar contrato para o pedido {PedidoId}.", user?.Email, idPedido);
                AddNotification(UseCaseNotification.Create(
                    UseCaseNotificationType.Forbid,
                    "Acesso negado: você não tem permissão para visualizar o contrato deste pedido."));
                return null;
            }
        }

        if (pedido.Status != StatusPedido.Pendente && pedido.Status != StatusPedido.Entregue) {
            logger.LogWarning("Pedido com ID {IdPedido} tem status {Status}, não é permitido gerar contrato.", idPedido, pedido.Status);
            AddNotification(UseCaseNotification.Create(
                UseCaseNotificationType.BadRequest,
                "Contrato só pode ser gerado para pedidos com status 'Pendente' ou 'Entregue'."));
            return null;
        }

        if (pedido.Items.Count == 0) {
            logger.LogWarning("Pedido com ID {IdPedido} não possui itens, não é possível gerar contrato.", idPedido);
            AddNotification(UseCaseNotification.Create(
                UseCaseNotificationType.BadRequest,
                "Não é possível gerar contrato para um pedido sem itens."));
            return null;
        }

        // 3. Gerar o PDF do contrato
        logger.LogInformation("Gerando PDF do contrato para o pedido {IdPedido}", idPedido);
        byte[] pdfBytes;
        try {
            pdfBytes = await contratoPdfService.GerarPdfAsync(pedido);
        } catch (Exception ex) {
            logger.LogError(ex, "Erro ao gerar PDF do contrato para o pedido {IdPedido}", idPedido);
            AddNotification(UseCaseNotification.Create(
                UseCaseNotificationType.Error,
                "Erro ao gerar o PDF do contrato."));
            return null;
        }

        // 4. Enviar para o Autentique
        var sandbox = string.Equals(configuration["AUTENTIQUE_SANDBOX"] ?? "true", "true", StringComparison.OrdinalIgnoreCase);
        var nomeDocumento = $"Contrato Aluguel - Pedido #{idPedido}";
        var nomeSignatario = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(pedido.Cliente.Nome);

        AutentiqueDocumentResult resultado;
        try {
            resultado = await autentiqueService.CriarDocumentoAsync(pdfBytes, nomeDocumento, nomeSignatario, sandbox);
        } catch (Exception ex) {
            logger.LogError(ex, "Erro ao enviar contrato ao Autentique para o pedido {IdPedido}", idPedido);
            AddNotification(UseCaseNotification.Create(
                UseCaseNotificationType.Error,
                "Erro ao enviar o contrato para assinatura digital."));
            return null;
        }

        // 5. Salvar no banco
        var contrato = new ContratoAutentique {
            IdPedido = idPedido,
            AutentiqueDocumentId = resultado.DocumentId,
            AutentiquePublicId = resultado.PublicId,
            LinkAssinatura = resultado.SigningLink,
            Status = StatusContrato.Pendente,
            DataCriacao = DateTime.Now
        };

        await contratoRepository.SaveAsync(contrato);

        logger.LogInformation("Contrato criado com sucesso para o pedido {IdPedido}: DocId={DocId}", idPedido, resultado.DocumentId);
        return contrato;
    }
}
