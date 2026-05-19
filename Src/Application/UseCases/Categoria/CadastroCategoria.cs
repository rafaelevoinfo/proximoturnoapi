
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class CadastroCategoria(ICategoriaRepository _repository, ILogger<CadastroCategoria> logger) : UseCaseBasico {
    public async Task<int> ExecuteAsync(CategoriaDTO categoriaDto) {
        logger.LogInformation("Iniciando cadastro de nova categoria: {Descricao}", categoriaDto.Descricao);
        var filtro = new FiltroCategoriaDTO {
            Descricao = categoriaDto.Descricao,
            ApenasAtivos = true
        };

        var categoriasExistentes = await _repository.GetAllAsync(filtro);
        if (categoriasExistentes.Any(c => c.Descricao == categoriaDto.Descricao.ToLowerInvariant())) {
            logger.LogWarning("Falha ao cadastrar categoria: Já existe uma categoria ativa com a descrição {Descricao}.", categoriaDto.Descricao);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, "Já existe uma categoria ativa com a mesma descrição."));
        }

        if (!IsValid)
            return 0;

        var categoria = new Categoria() {
            Descricao = categoriaDto.Descricao,
            Ativo = categoriaDto.Ativo,
            Periodos = categoriaDto.Periodos.Select(cp => new CategoriaPeriodo() {
                QuantidadeDias = cp.QtdeDias,
                Valor = cp.Valor
            }).ToList()
        };

        try {
            await _repository.SaveAsync(categoria);
            logger.LogInformation("Categoria {CategoriaId} ({Descricao}) cadastrada com sucesso.", categoria.Id, categoria.Descricao);
            return categoria.Id;
        } catch (Exception ex) {
            logger.LogError(ex, "Erro fatal ao salvar a categoria {Descricao} no banco de dados.", categoriaDto.Descricao);
            throw;
        }
    }
}
