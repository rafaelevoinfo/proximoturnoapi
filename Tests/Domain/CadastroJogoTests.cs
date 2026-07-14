using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.DTOs.Filtros;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Tests.Domain;

public class CadastroJogoTests {
    private const string MensagemDuplicado = "Já existe um jogo com o mesmo nome.";

    private static CadastroJogo Montar(FakeJogoRepository jogoRepo) {
        var tagRepo = new FakeTagRepository();
        return new CadastroJogo(jogoRepo, tagRepo, NullLogger<CadastroJogo>.Instance);
    }

    private static JogoDTO NovoJogo(string nome) => new() {
        Nome = nome,
        Descricao = "Descrição",
        QuantidadeCopias = 1
    };

    [Fact]
    public async Task NaoBloqueia_QuandoNomeEhSubstringDeOutroJogo() {
        // "UNO Stacko" já existe e o filtro LIKE '%uno%' o retorna, mas não é o mesmo jogo.
        var jogoRepo = new FakeJogoRepository {
            Existentes = { new Jogo { Id = 1, Nome = "UNO Stacko", Descricao = "x" } }
        };
        var useCase = Montar(jogoRepo);

        var id = await useCase.ExecuteAsync(NovoJogo("UNO"));

        Assert.True(useCase.IsValid);
        Assert.DoesNotContain(useCase.Notifications, n => n.Message == MensagemDuplicado);
        Assert.NotEqual(0, id);
        Assert.NotNull(jogoRepo.Salvo);
    }

    [Fact]
    public async Task Bloqueia_QuandoNomeExatoJaExiste() {
        var jogoRepo = new FakeJogoRepository {
            Existentes = { new Jogo { Id = 1, Nome = "UNO", Descricao = "x" } }
        };
        var useCase = Montar(jogoRepo);

        var id = await useCase.ExecuteAsync(NovoJogo("UNO"));

        Assert.False(useCase.IsValid);
        Assert.Contains(useCase.Notifications, n => n.Message == MensagemDuplicado);
        Assert.Equal(0, id);
        Assert.Null(jogoRepo.Salvo);
    }

    [Fact]
    public async Task Bloqueia_IgnorandoCaixaEEspacos() {
        var jogoRepo = new FakeJogoRepository {
            Existentes = { new Jogo { Id = 1, Nome = "UNO", Descricao = "x" } }
        };
        var useCase = Montar(jogoRepo);

        var id = await useCase.ExecuteAsync(NovoJogo("  uno  "));

        Assert.False(useCase.IsValid);
        Assert.Contains(useCase.Notifications, n => n.Message == MensagemDuplicado);
        Assert.Equal(0, id);
    }

    private class FakeJogoRepository : IJogoRepository {
        public List<Jogo> Existentes { get; set; } = [];
        public Jogo? Salvo { get; private set; }

        // Simula o filtro LIKE '%nome%' do repositório real (match por substring).
        public Task<List<Jogo>> GetAllAsync(FiltroJogoDTO filtro) {
            var resultado = string.IsNullOrEmpty(filtro.Nome)
                ? Existentes
                : Existentes.Where(j => j.Nome != null &&
                    j.Nome.Contains(filtro.Nome, StringComparison.OrdinalIgnoreCase)).ToList();
            return Task.FromResult(resultado.ToList());
        }

        public Task SaveAsync(Jogo jogo, bool commit = true) {
            jogo.Id = 99;
            Salvo = jogo;
            return Task.CompletedTask;
        }

        public Task<List<Jogo>> GetMaisAlugadosAsync() => throw new NotImplementedException();
        public Task<List<Jogo>> GetAllByIdsAsync(List<int> ids) => throw new NotImplementedException();
        public Task<List<JogoCopia>> GetAllCopiasByIdsAsync(List<int> ids) => throw new NotImplementedException();
        public Task<List<JogoCopia>> GetAllCopiasByIdJogoAsync(int idJogo) => throw new NotImplementedException();
        public Task<Jogo?> GetByIdAsync(int id) => throw new NotImplementedException();
        public Task<Jogo?> GetResumoByIdAsync(int id) => throw new NotImplementedException();
        public Task<List<JogoCopia>> GetCopiasAsync(int id) => throw new NotImplementedException();
        public Task<JogoCopia?> GetCopiaByIdAsync(int id) => throw new NotImplementedException();
        public Task SaveAsync(JogoCopia jogo, bool commit = true) => throw new NotImplementedException();
        public Task<bool> DeleteAsync(int id) => throw new NotImplementedException();
        public Task<bool> ExisteAsync(int id) => throw new NotImplementedException();
        public Task<bool> CopiaExisteAndDisponivel(int id) => throw new NotImplementedException();
        public Task SaveChangesAsync() => Task.CompletedTask;
        public Task StartTransactionAsync() => Task.CompletedTask;
        public Task CommitTransactionAsync() => Task.CompletedTask;
        public Task RollbackTransactionAsync() => Task.CompletedTask;
    }

    private class FakeTagRepository : ITagRepository {
        public Task<List<Tag>> GetAllAsync(FiltroTagDTO? filtro, bool track = false) => Task.FromResult(new List<Tag>());
        public Task<List<Tag>> GetAllAsync(bool track = false) => Task.FromResult(new List<Tag>());
        public Task<Tag?> GetByIdAsync(int id) => throw new NotImplementedException();
        public Task<Tag?> GetByNomeAsync(string nome) => throw new NotImplementedException();
        public Task AddAsync(Tag tag) => throw new NotImplementedException();
        public Task UpdateAsync(Tag tag) => throw new NotImplementedException();
        public Task<bool> DeleteAsync(int id) => throw new NotImplementedException();
    }
}
