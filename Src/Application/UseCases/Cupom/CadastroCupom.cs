using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;
using ProximoTurnoApi.Domain;

namespace ProximoTurnoApi.Application.UseCases;

public class CadastroCupom(ICupomRepository _repository, ILogger<CadastroCupom> logger) : UseCaseBasico
{
    public async Task<CupomDTO?> ExecuteAsync(NovoCupomDTO dto)
    {
        logger.LogInformation("Iniciando cadastro de novo cupom. Código fornecido: '{Codigo}'", dto.Codigo);

        string codigo;
        if (string.IsNullOrWhiteSpace(dto.Codigo))
        {
            codigo = await GerarCodigoUnicoAsync();
            logger.LogInformation("Nenhum código fornecido. Gerado automaticamente: '{Codigo}'", codigo);
        }
        else
        {
            codigo = dto.Codigo.Trim().ToUpperInvariant();
        }

        if (dto.TipoDesconto == TipoDesconto.Percentual && dto.ValorDesconto > 100)
        {
            logger.LogWarning("Falha ao cadastrar cupom: Desconto percentual não pode ser maior que 100%.");
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "O desconto percentual não pode ser maior que 100%."));
        }

        if (dto.DataInicio.HasValue && dto.DataFim.HasValue && dto.DataInicio.Value > dto.DataFim.Value)
        {
            logger.LogWarning("Falha ao cadastrar cupom: A data de início ({DataInicio}) não pode ser maior que a data de fim ({DataFim}).", dto.DataInicio, dto.DataFim);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "A data de início não pode ser maior que a data de fim."));
        }

        if (!string.IsNullOrWhiteSpace(dto.Condicao))
        {
            if (!ConditionEvaluator.TryValidate(dto.Condicao, out var syntaxError))
            {
                logger.LogWarning("Falha ao cadastrar cupom: Erro de sintaxe na condição '{Condicao}'. Erro: {Error}", dto.Condicao, syntaxError);
                AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, $"Erro de sintaxe na condição: {syntaxError}"));
            }
        }

        var existente = await _repository.GetByCodigoAsync(codigo);
        if (existente != null)
        {
            logger.LogWarning("Falha ao cadastrar cupom: Já existe um cupom cadastrado com o código '{Codigo}'.", codigo);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, $"Já existe um cupom cadastrado com o código '{codigo}'."));
        }

        if (!IsValid)
        {
            return null;
        }

        var cupom = new Cupom
        {
            Codigo = codigo,
            TipoDesconto = dto.TipoDesconto!.Value,
            ValorDesconto = dto.ValorDesconto,
            DataInicio = dto.DataInicio,
            DataFim = dto.DataFim,
            LimiteUsoGlobal = dto.LimiteUsoGlobal,
            LimiteUsoCliente = dto.LimiteUsoCliente,
            Condicao = dto.Condicao,
            Ativo = dto.Ativo
        };

        try
        {
            await _repository.SaveAsync(cupom);
            logger.LogInformation("Cupom ID {CupomId} ({Codigo}) cadastrado com sucesso.", cupom.Id, cupom.Codigo);
            return CupomDTO.FromModel(cupom);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro fatal ao salvar o cupom '{Codigo}' no banco de dados.", codigo);
            throw;
        }
    }

    private async Task<string> GerarCodigoUnicoAsync()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        string codigo;
        do
        {
            var randomPart = new char[6];
            for (int i = 0; i < 6; i++)
            {
                randomPart[i] = chars[Random.Shared.Next(chars.Length)];
            }
            codigo = $"PT-{new string(randomPart)}";
        } while (await _repository.GetByCodigoAsync(codigo) != null);

        return codigo;
    }
}
