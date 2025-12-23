using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases.Categoria;

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

        categoriaDto.UpdateModel(categoria);
        await _repository.UpdateAsync(categoria);
        return IsValid;
    }
}
