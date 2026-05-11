using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class AtualizarCategoria(ICategoriaRepository _repository, ILogger<AtualizarCategoria> logger) : UseCaseBasico {
    public async Task<bool> ExecuteAsync(CategoriaDTO categoriaDto) {
        logger.LogInformation("Iniciando atualização da categoria {CategoriaId} ({Descricao}).", categoriaDto.Id, categoriaDto.Descricao);
        var categoria = await _repository.GetByIdAsync(categoriaDto.Id ?? 0);
        if (categoria == null) {
            logger.LogWarning("Falha ao atualizar: Categoria ID {CategoriaId} não encontrada.", categoriaDto.Id);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, "Categoria não encontrada."));
            return false;
        }

        var filtro = new FiltroCategoriaDTO {
            Descricao = categoriaDto.Descricao
        };

        var categoriasExistentes = await _repository.GetAllAsync(filtro);
        if (categoriasExistentes.Any(c => c.Id != categoriaDto.Id)) {
            logger.LogWarning("Falha ao atualizar categoria {CategoriaId}: Já existe outra categoria com a descrição {Descricao}.", categoriaDto.Id, categoriaDto.Descricao);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, "Já existe uma categoria com a mesma descrição."));
            return false;
        }

        if (!IsValid)
            return false;

        categoria.Periodos.RemoveAll(periodo =>
            !categoriaDto.Periodos.Any(p =>
                p.QtdeDias == periodo.QuantidadeDias &&
                p.Valor == periodo.Valor));

        foreach (var periodo in categoriaDto.Periodos) {
            if (!categoria.Periodos.Any(p => p.QuantidadeDias == periodo.QtdeDias && p.Valor == periodo.Valor)) {
                categoria.Periodos.Add(new CategoriaPeriodo() {
                    QuantidadeDias = periodo.QtdeDias,
                    Valor = periodo.Valor
                });
            }
        }

        try {
            await _repository.SaveAsync(categoria);
            logger.LogInformation("Categoria {CategoriaId} atualizada com sucesso.", categoria.Id);
            return IsValid;
        } catch (Exception ex) {
            logger.LogError(ex, "Erro fatal ao salvar atualização da categoria {CategoriaId}.", categoriaDto.Id);
            throw;
        }
    }
}
