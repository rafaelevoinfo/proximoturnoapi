using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class AtualizarCategoria(ICategoriaRepository repository) : UseCaseBasico {
    private readonly ICategoriaRepository _repository = repository;

    public async Task<bool> ExecuteAsync(CategoriaDTO categoriaDto) {
        var categoria = await _repository.GetByIdAsync(categoriaDto.Id ?? 0);
        if (categoria == null) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, "Categoria não encontrada."));
            return false;
        }

        var filtro = new FiltroCategoriaDTO {
            Descricao = categoriaDto.Descricao
        };

        var categoriasExistentes = await _repository.GetAllAsync(filtro);
        if (categoriasExistentes.Any(c => c.Id != categoriaDto.Id)) {
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

        await _repository.SaveAsync(categoria);
        return IsValid;
    }
}
