using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases.Categoria;

public class AtualizarCategoria(ICategoriaRepository repository, IFaixaPrecoRepository faixaPrecoRepository) : UseCaseBasico {
    private readonly ICategoriaRepository _repository = repository;
    private readonly IFaixaPrecoRepository _faixaPrecoRepository = faixaPrecoRepository;

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

        categoria.FaixasPreco.Clear();
        if (categoriaDto.FaixasPrecoIds.Count > 0)
        {
            foreach (var faixaId in categoriaDto.FaixasPrecoIds)
            {
                var faixa = await _faixaPrecoRepository.GetByIdAsync(faixaId);
                if (faixa != null)
                {
                    categoria.FaixasPreco.Add(faixa);
                }
            }
        }

        await _repository.UpdateAsync(categoria);
        return IsValid;
    }
}
