
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class CadastroCategoria(ICategoriaRepository repository) : UseCaseBasico {
    private readonly ICategoriaRepository _repository = repository;

    public async Task<int> ExecuteAsync(CategoriaDTO categoriaDto) {
        var filtro = new FiltroCategoriaDTO {
            Descricao = categoriaDto.Descricao
        };

        var categoriasExistentes = await _repository.GetAllAsync(filtro);
        if (categoriasExistentes.Count > 0) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, "Já existe uma categoria com a mesma descrição."));
        }

        if (!IsValid)
            return 0;

        var categoria = new Categoria() {
            Descricao = categoriaDto.Descricao,
            Periodos = categoriaDto.Periodos.Select(cp => new CategoriaPeriodo() {
                QuantidadeDias = cp.QtdeDias,
                Valor = cp.Valor
            }).ToList()
        };

        await _repository.SaveAsync(categoria);
        return categoria.Id;
    }
}
