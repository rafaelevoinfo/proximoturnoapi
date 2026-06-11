using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class ValidarCupom(
    ICupomRepository _repository,
    IJogoRepository _jogoRepository,
    ICategoriaRepository _categoriaRepository,
    ILogger<ValidarCupom> logger) : UseCaseBasico
{
    public async Task<ValidacaoCupomResultadoDTO> ExecuteAsync(ValidarCupomDTO dto)
    {
        logger.LogInformation("Iniciando validação do cupom '{Codigo}' para cliente ID {IdCliente}.", dto.Codigo, dto.IdCliente);

        var cupom = await _repository.GetByCodigoAsync(dto.Codigo);
        if (cupom == null)
        {
            logger.LogWarning("Validação falhou: Cupom com código '{Codigo}' não foi encontrado.", dto.Codigo);
            return new ValidacaoCupomResultadoDTO { Valido = false, Mensagem = "Cupom inválido." };
        }

        if (!cupom.Ativo)
        {
            logger.LogWarning("Validação falhou: Cupom '{Codigo}' não está ativo.", cupom.Codigo);
            return new ValidacaoCupomResultadoDTO { Valido = false, Mensagem = "Cupom inválido." };
        }

        var now = DateTime.Now;
        if (cupom.DataInicio.HasValue && now < cupom.DataInicio.Value)
        {
            logger.LogWarning("Validação falhou: Cupom '{Codigo}' ainda não iniciou sua vigência (início: {DataInicio}).", cupom.Codigo, cupom.DataInicio);
            return new ValidacaoCupomResultadoDTO { Valido = false, Mensagem = "Cupom inválido." };
        }
        if (cupom.DataFim.HasValue && now > cupom.DataFim.Value)
        {
            logger.LogWarning("Validação falhou: Cupom '{Codigo}' expirou (fim: {DataFim}).", cupom.Codigo, cupom.DataFim);
            return new ValidacaoCupomResultadoDTO { Valido = false, Mensagem = "Cupom inválido." };
        }

        if (cupom.LimiteUsoGlobal.HasValue)
        {
            int usosGlobal = await _repository.GetUsoCountGlobalAsync(cupom.Id);
            if (usosGlobal >= cupom.LimiteUsoGlobal.Value)
            {
                logger.LogWarning("Validação falhou: Cupom '{Codigo}' atingiu o limite de uso global ({LimiteUsoGlobal}).", cupom.Codigo, cupom.LimiteUsoGlobal);
                return new ValidacaoCupomResultadoDTO { Valido = false, Mensagem = "Cupom inválido." };
            }
        }

        if (cupom.LimiteUsoCliente.HasValue)
        {
            int usosCliente = await _repository.GetUsoCountClienteAsync(cupom.Id, dto.IdCliente);
            if (usosCliente >= cupom.LimiteUsoCliente.Value)
            {
                logger.LogWarning("Validação falhou: Cupom '{Codigo}' atingiu o limite de uso do cliente ID {IdCliente} ({LimiteUsoCliente}).", cupom.Codigo, dto.IdCliente, cupom.LimiteUsoCliente);
                return new ValidacaoCupomResultadoDTO { Valido = false, Mensagem = "Cupom inválido." };
            }
        }

        var jogoIds = dto.Itens.Select(i => i.IdJogo).ToList();
        var periodoIds = dto.Itens.Select(i => i.IdPeriodo).ToList();

        var jogos = await _jogoRepository.GetAllByIdsAsync(jogoIds);
        var categoriasLista = await _categoriaRepository.GetAllAsync(new FiltroCategoriaDTO());
        var periodos = categoriasLista
            .SelectMany(c => c.Periodos)
            .Where(p => periodoIds.Contains(p.Id))
            .ToList();

        decimal totalPedido = 0;
        var categorias = new List<int>();

        foreach (var item in dto.Itens)
        {
            var jogo = jogos.FirstOrDefault(j => j.Id == item.IdJogo);
            var periodo = periodos.FirstOrDefault(p => p.Id == item.IdPeriodo);

            if (jogo == null || periodo == null)
            {
                logger.LogWarning("Validação falhou: Jogo ID {IdJogo} ou Período ID {IdPeriodo} não encontrado no banco.", item.IdJogo, item.IdPeriodo);
                return new ValidacaoCupomResultadoDTO { Valido = false, Mensagem = "Cupom inválido." };
            }

            totalPedido += periodo.Valor;
            categorias.Add(jogo.IdCategoria);
        }

        if (!string.IsNullOrWhiteSpace(cupom.Condicao))
        {
            bool condicaoValida = ConditionEvaluator.Evaluate(cupom.Condicao, totalPedido, categorias);
            if (!condicaoValida)
            {
                logger.LogWarning("Validação falhou: Cupom '{Codigo}' não atendeu à condição '{Condicao}' para o pedido total {TotalPedido} e categorias [{Categorias}].", cupom.Codigo, cupom.Condicao, totalPedido, string.Join(",", categorias));
                return new ValidacaoCupomResultadoDTO { Valido = false, Mensagem = "Cupom inválido." };
            }
        }

        decimal valorDescontoCalculado = 0;
        if (cupom.TipoDesconto == TipoDesconto.Fixo)
        {
            valorDescontoCalculado = Math.Min(totalPedido, cupom.ValorDesconto);
        }
        else if (cupom.TipoDesconto == TipoDesconto.Percentual)
        {
            valorDescontoCalculado = Math.Round(totalPedido * (cupom.ValorDesconto / 100), 2);
        }

        logger.LogInformation("Cupom '{Codigo}' validado com sucesso. Desconto calculado: {Desconto}", cupom.Codigo, valorDescontoCalculado);

        return new ValidacaoCupomResultadoDTO
        {
            Valido = true,
            Mensagem = "Cupom aplicado com sucesso.",
            ValorDescontoCalculado = valorDescontoCalculado,
            TipoDesconto = cupom.TipoDesconto,
            ValorDescontoOriginal = cupom.ValorDesconto
        };
    }
}
