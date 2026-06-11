using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;
using ProximoTurnoApi.Domain;

namespace ProximoTurnoApi.Application.UseCases;

public class AtualizarCupom(ICupomRepository _repository, ILogger<AtualizarCupom> logger) : UseCaseBasico
{
    public async Task<CupomDTO?> ExecuteAsync(NovoCupomDTO dto)
    {
        logger.LogInformation("Iniciando atualização do cupom ID {CupomId}.", dto.Id);

        var cupom = await _repository.GetByIdAsync(dto.Id ?? 0);
        if (cupom == null)
        {
            logger.LogWarning("Falha ao atualizar: Cupom ID {CupomId} não encontrado.", dto.Id);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.NotFound, "Cupom não encontrado."));
            return null;
        }

        string? newCodigo = dto.Codigo?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(newCodigo))
        {
            logger.LogWarning("Falha ao atualizar: O código do cupom não pode ser vazio.");
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "O código do cupom não pode ser vazio."));
        }

        if (dto.TipoDesconto == TipoDesconto.Percentual && dto.ValorDesconto > 100)
        {
            logger.LogWarning("Falha ao atualizar cupom: Desconto percentual não pode ser maior que 100%.");
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "O desconto percentual não pode ser maior que 100%."));
        }

        if (dto.DataInicio.HasValue && dto.DataFim.HasValue && dto.DataInicio.Value > dto.DataFim.Value)
        {
            logger.LogWarning("Falha ao atualizar cupom: A data de início ({DataInicio}) não pode ser maior que a data de fim ({DataFim}).", dto.DataInicio, dto.DataFim);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "A data de início não pode ser maior que a data de fim."));
        }

        if (!string.IsNullOrWhiteSpace(dto.Condicao))
        {
            if (!ConditionEvaluator.TryValidate(dto.Condicao, out var syntaxError))
            {
                logger.LogWarning("Falha ao atualizar cupom: Erro de sintaxe na condição '{Condicao}'. Erro: {Error}", dto.Condicao, syntaxError);
                AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, $"Erro de sintaxe na condição: {syntaxError}"));
            }
        }

        if (!string.IsNullOrWhiteSpace(newCodigo) && newCodigo != cupom.Codigo)
        {
            var existente = await _repository.GetByCodigoAsync(newCodigo);
            if (existente != null && existente.Id != cupom.Id)
            {
                logger.LogWarning("Falha ao atualizar cupom: Já existe outro cupom cadastrado com o código '{Codigo}'.", newCodigo);
                AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, $"Já existe um cupom cadastrado com o código '{newCodigo}'."));
            }
        }

        if (!IsValid)
        {
            return null;
        }

        if (newCodigo != null)
        {
            cupom.Codigo = newCodigo;
        }
        cupom.TipoDesconto = dto.TipoDesconto!.Value;
        cupom.ValorDesconto = dto.ValorDesconto;
        cupom.DataInicio = dto.DataInicio;
        cupom.DataFim = dto.DataFim;
        cupom.LimiteUsoGlobal = dto.LimiteUsoGlobal;
        cupom.LimiteUsoCliente = dto.LimiteUsoCliente;
        cupom.Condicao = dto.Condicao;
        cupom.Ativo = dto.Ativo;

        try
        {
            await _repository.SaveAsync(cupom);
            logger.LogInformation("Cupom ID {CupomId} atualizado com sucesso.", cupom.Id);
            return CupomDTO.FromModel(cupom);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro fatal ao salvar a atualização do cupom ID {CupomId}.", cupom.Id);
            throw;
        }
    }
}
