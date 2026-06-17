using System.Collections.Immutable;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class CadastroPedido(IPedidoRepository pedidoRepository,
    IJogoRepository _jogoRepository,
    IClienteRepository _clienteRepository,
    ICategoriaRepository _categoriaRepository,
    UserManager<Usuario> _userManager,
    ValidarCupom _validarCupom,
    IContratoQueue _contratoQueue,
    ILogger<CadastroPedido> logger) : PedidoUseCaseBasico(pedidoRepository) {

    public async Task<int> ExecuteAsync(ClaimsPrincipal userClaim, NovoPedidoDTO novoPedidoDto) {
        logger.LogInformation("Iniciando cadastro de novo pedido.");
        var user = await _userManager.GetUserAsync(userClaim);
        Cliente? cliente = null;

        if (user != null && await _userManager.IsInRoleAsync(user, Roles.Admin) && novoPedidoDto.IdCliente.HasValue) {
            cliente = await _clienteRepository.GetByIdAsync(novoPedidoDto.IdCliente.Value);
            if (cliente is null) {
                logger.LogWarning("Falha ao cadastrar pedido: Cliente de ID {ClienteId} não encontrado.", novoPedidoDto.IdCliente.Value);
                AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, $"Cliente de ID {novoPedidoDto.IdCliente.Value} não encontrado."));
                return 0;
            }
        } else {
            cliente = await _clienteRepository.GetByEmailAsync(user?.Email ?? "");
            if (cliente is null) {
                logger.LogWarning("Falha ao cadastrar pedido: Usuário {UserEmail} não está vinculado a nenhum cliente.", user?.Email ?? "desconhecido");
                AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Forbid, "Usuário não logado ou não vinculado a nenhum cliente."));
                return 0;
            }
        }

        var pedido = new Pedido(cliente, novoPedidoDto.MetodoPagamento, novoPedidoDto.MetodoEntrega);
        foreach (var item in novoPedidoDto.Items) {
            var resultValidacao = await ValidarAdicionarItem(item, _jogoRepository, _categoriaRepository);
            if (!IsValid) {
                logger.LogWarning("Falha na validação do item do pedido para o jogo ID {JogoId}.", item.IdJogo);
                return 0;
            }

            var itemPedido = new ItemPedido() {
                JogoCopia = resultValidacao.Value.copia!,
                IdPeriodo = resultValidacao.Value.periodo.Id,
                Valor = resultValidacao.Value.periodo.Valor,
                DataDevolucao = pedido.CalcularDataDevolucao(resultValidacao.Value.periodo.QuantidadeDias)
            };

            if (!pedido.AdicionarItem(itemPedido)) {
                logger.LogWarning("Regra de negócio impediu adição de item ao pedido: {Errors}", string.Join(", ", pedido.Notifications.Select(n => n.Message)));
                IReadOnlyCollection<UseCaseNotification> notifications = pedido.Notifications.Select(n => UseCaseNotification.Create(UseCaseNotificationType.BadRequest, n.Message)).ToList();
                AddNotifications(notifications);
                return 0;
            }
        }

        if (!string.IsNullOrWhiteSpace(novoPedidoDto.CupomCodigo)) {
            var validacao = await _validarCupom.ExecuteAsync(new ValidarCupomDTO {
                Codigo = novoPedidoDto.CupomCodigo,
                IdCliente = cliente.Id,
                Itens = novoPedidoDto.Items.Select(i => new ItemCupomValidacaoDTO {
                    IdJogo = i.IdJogo,
                    IdPeriodo = i.IdPeriodo
                }).ToList()
            });

            if (!validacao.Valido) {
                logger.LogWarning("Falha ao aplicar cupom no pedido: {Mensagem}", validacao.Mensagem);
                AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, validacao.Mensagem ?? "Cupom inválido."));
                return 0;
            }

            if (validacao.IdCupom.HasValue) {
                pedido.AplicarCupom(validacao.IdCupom.Value, validacao.ValorDescontoCalculado);

                if (!pedido.IsValid) {
                    logger.LogWarning("Falha ao aplicar regras de negócio do cupom: {Errors}", string.Join(", ", pedido.Notifications.Select(n => n.Message)));
                    IReadOnlyCollection<UseCaseNotification> notifications = pedido.Notifications.Select(n => UseCaseNotification.Create(UseCaseNotificationType.BadRequest, n.Message)).ToList();
                    AddNotifications(notifications);
                    return 0;
                }
            } else {
                logger.LogWarning("Validação do cupom retornou sucesso mas ID do cupom está ausente.");
                AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Erro ao associar o cupom."));
                return 0;
            }
        }

        try {
            await _pedidoRepository.SaveAsync(pedido);
            logger.LogInformation("Pedido {PedidoId} cadastrado com sucesso para o cliente {ClienteId}.", pedido.Id, cliente.Id);
            
            // Enfileira a geração de contrato de forma assíncrona
            _contratoQueue.Enfileirar(pedido.Id);
            
            return pedido.Id;
        } catch (Exception ex) {
            logger.LogError(ex, "Erro fatal ao salvar o pedido no banco de dados.");
            throw;
        }
    }

    private async Task<int?> BuscarIdClienteLogado(string email) {
        return await _clienteRepository.GetIdByEmailAsync(email);
    }
}