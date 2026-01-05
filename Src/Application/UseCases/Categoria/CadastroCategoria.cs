using Flunt.Notifications;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases.Categoria;

public class CadastroCategoria(ICategoriaRepository repository, IPeriodoRepository faixaPrecoRepository) : UseCaseBasico {
    private readonly ICategoriaRepository _repository = repository;
    private readonly IPeriodoRepository _faixaPrecoRepository = faixaPrecoRepository;

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

        var categoria = categoriaDto.ToModel();

        if (categoriaDto.FaixasPrecoIds.Count > 0) {
            foreach (var faixaId in categoriaDto.FaixasPrecoIds) {
                var faixa = await _faixaPrecoRepository.GetByIdAsync(faixaId);
                if (faixa != null) {
                    categoria.Periodos.Add(faixa);
                }
            }
        }

        await _repository.AddAsync(categoria);
        return categoria.Id;
    }
}
