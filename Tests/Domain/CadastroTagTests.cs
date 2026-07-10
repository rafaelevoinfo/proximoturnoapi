using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.DTOs.Filtros;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Tests.Domain;

public class CadastroTagTests {
    private const string MensagemDuplicado = "Já existe uma tag com o mesmo nome.";

    private static CadastroTag Montar(FakeTagRepository repo) =>
        new(repo, NullLogger<CadastroTag>.Instance);

    [Fact]
    public async Task NaoBloqueia_QuandoNomeEhSubstringDeOutraTag() {
        var repo = new FakeTagRepository {
            Existentes = { new Tag { Nome = "Cooperativo Avançado" } }
        };
        var useCase = Montar(repo);

        var id = await useCase.ExecuteAsync(new TagDTO { Nome = "Cooperativo" });

        Assert.True(useCase.IsValid);
        Assert.DoesNotContain(useCase.Notifications, n => n.Message == MensagemDuplicado);
        Assert.NotNull(repo.Adicionada);
    }

    [Fact]
    public async Task Bloqueia_QuandoNomeExatoJaExiste() {
        var repo = new FakeTagRepository {
            Existentes = { new Tag { Nome = "Cooperativo" } }
        };
        var useCase = Montar(repo);

        var id = await useCase.ExecuteAsync(new TagDTO { Nome = "Cooperativo" });

        Assert.False(useCase.IsValid);
        Assert.Contains(useCase.Notifications, n => n.Message == MensagemDuplicado);
        Assert.Equal(0, id);
        Assert.Null(repo.Adicionada);
    }

    private class FakeTagRepository : ITagRepository {
        public List<Tag> Existentes { get; set; } = [];
        public Tag? Adicionada { get; private set; }

        // Simula o filtro LIKE '%nome%' do repositório real (match por substring).
        public Task<List<Tag>> GetAllAsync(FiltroTagDTO? filtro, bool track = false) {
            var resultado = string.IsNullOrEmpty(filtro?.Nome)
                ? Existentes
                : Existentes.Where(t => t.Nome != null &&
                    t.Nome.Contains(filtro.Nome, StringComparison.OrdinalIgnoreCase)).ToList();
            return Task.FromResult(resultado.ToList());
        }

        public Task AddAsync(Tag tag) {
            tag.Id = 99;
            Adicionada = tag;
            return Task.CompletedTask;
        }

        public Task<List<Tag>> GetAllAsync(bool track = false) => Task.FromResult(Existentes);
        public Task<Tag?> GetByIdAsync(int id) => throw new NotImplementedException();
        public Task<Tag?> GetByNomeAsync(string nome) => throw new NotImplementedException();
        public Task UpdateAsync(Tag tag) => throw new NotImplementedException();
        public Task<bool> DeleteAsync(int id) => throw new NotImplementedException();
    }
}
