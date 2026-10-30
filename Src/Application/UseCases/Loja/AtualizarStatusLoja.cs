using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class AtualizarStatusLoja(IConfiguracaoLojaRepository _repository, ILogger<AtualizarStatusLoja> logger) : UseCaseBasico {
    public async Task<StatusLojaDTO?> ExecuteAsync(AtualizarStatusLojaDTO dto) {
        var mensagem = dto.Mensagem?.Trim();
        if (mensagem?.Length > 500) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "A mensagem pode ter no máximo 500 caracteres."));
            return null;
        }

        var configuracao = await _repository.GetAsync() ?? new ConfiguracaoLoja();
        configuracao.LojaFechada = dto.LojaFechada;
        configuracao.MensagemLojaFechada = string.IsNullOrWhiteSpace(mensagem) ? null : mensagem;
        configuracao.DataHoraAlteracao = DateTime.Now;

        await _repository.SalvarAsync(configuracao);
        logger.LogInformation("Loja {Situacao} pelo admin.", configuracao.LojaFechada ? "fechada" : "aberta");
        return StatusLojaDTO.FromModel(configuracao);
    }
}
