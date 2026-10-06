using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.DTOs.Filtros;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;
using ProximoTurnoApi.Tests.Fakes;

namespace ProximoTurnoApi.Tests.Domain;

public class AtualizarJogoTests {

    private static AtualizarJogo Montar(FakeJogoRepository jogoRepo, FakeManualQueue manualQueue) =>
        new(jogoRepo, new FakeTagRepository(), manualQueue, NullLogger<AtualizarJogo>.Instance);

    [Fact]
    public async Task RemoverUmLinkDeDois_EnfileiraOLinkRemovidoComOIdDoJogoCerto() {
        var jogo = new Jogo {
            Id = 7,
            Nome = "Azul",
            Descricao = "Descrição",
            Links = [
                new JogoLink { Id = 1, Titulo = "Manual", Url = "https://site/uploads/manual.pdf", Tipo = TipoLink.Regra },
                new JogoLink { Id = 2, Titulo = "Como jogar", Url = "https://youtube.com/watch?v=abc", Tipo = TipoLink.Video }
            ]
        };
        var jogoRepo = new FakeJogoRepository { Existentes = { jogo } };
        var manualQueue = new FakeManualQueue();
        var jogoDto = new JogoDTO {
            Id = 7,
            Nome = "Azul",
            Descricao = "Descrição",
            QuantidadeCopias = 1,
            Links = [new JogoLinkDTO { Id = 1, Titulo = "Manual", Url = "https://site/uploads/manual.pdf", Tipo = TipoLink.Regra }]
        };

        await Montar(jogoRepo, manualQueue).ExecuteAsync(jogoDto);

        // O link 2 saiu do DTO: sem ele na fila com o IdJogo certo, o SincronizarManual nao
        // acha os duplicados do jogo na hora de apagar os vetores do link removido.
        Assert.Contains(manualQueue.Enfileirados, j => j.IdJogoLink == 2 && j.IdJogo == 7);
    }

    private static JogoDTO DtoDe(Jogo jogo, List<CopiaJogoDTO> copias) => new() {
        Id = jogo.Id, Nome = jogo.Nome, Descricao = "Descrição", Copias = copias,
    };

    [Fact]
    public async Task CopiasDoFormulario_SaoAplicadasAoJogo() {
        var jogo = new Jogo {
            Id = 7, Nome = "Azul", Descricao = "Descrição",
            Copias = [new JogoCopia { Id = 1, IdJogo = 7, Status = StatusJogo.Disponivel },
                      new JogoCopia { Id = 2, IdJogo = 7, Status = StatusJogo.Disponivel }],
        };
        var useCase = Montar(new FakeJogoRepository { Existentes = { jogo } }, new FakeManualQueue());

        var ok = await useCase.ExecuteAsync(DtoDe(jogo, [
            new CopiaJogoDTO { Id = 1, Status = StatusJogo.Manutencao },
            new CopiaJogoDTO { Id = 0, Status = StatusJogo.ApenasEmEventos },
        ]));

        Assert.True(ok);
        Assert.Equal([StatusJogo.Manutencao, StatusJogo.Desativado, StatusJogo.ApenasEmEventos], jogo.Copias.Select(c => c.Status));
    }

    [Fact]
    public async Task CopiaAlugadaRemovidaDoFormulario_NaoSalva() {
        var jogo = new Jogo {
            Id = 7, Nome = "Azul", Descricao = "Descrição",
            Copias = [new JogoCopia { Id = 1, IdJogo = 7, Status = StatusJogo.Alugado },
                      new JogoCopia { Id = 2, IdJogo = 7, Status = StatusJogo.Disponivel }],
        };
        var useCase = Montar(new FakeJogoRepository { Existentes = { jogo } }, new FakeManualQueue());

        var ok = await useCase.ExecuteAsync(DtoDe(jogo, [new CopiaJogoDTO { Id = 2, Status = StatusJogo.Disponivel }]));

        Assert.False(ok);
        Assert.Contains("alugada", useCase.AggregateErrors());
        Assert.Equal(StatusJogo.Alugado, jogo.Copias[0].Status);
    }

    [Fact]
    public async Task SemListaDeCopias_NaoMexeNasCopias() {
        var jogo = new Jogo { Id = 7, Nome = "Azul", Descricao = "Descrição",
                              Copias = [new JogoCopia { Id = 1, IdJogo = 7, Status = StatusJogo.Manutencao }] };
        var useCase = Montar(new FakeJogoRepository { Existentes = { jogo } }, new FakeManualQueue());

        Assert.True(await useCase.ExecuteAsync(new JogoDTO { Id = 7, Nome = "Azul", Descricao = "Descrição", QuantidadeCopias = 3 }));
        Assert.Equal([StatusJogo.Manutencao], jogo.Copias.Select(c => c.Status));
    }

    private class FakeJogoRepository : IJogoRepository {
        public List<Jogo> Existentes { get; set; } = [];

        public Task<Jogo?> GetByIdAsync(int id) => Task.FromResult(Existentes.FirstOrDefault(j => j.Id == id));
        public Task<List<Jogo>> GetAllAsync(FiltroJogoDTO filtro) => Task.FromResult(new List<Jogo>());
        public Task SaveAsync(Jogo jogo, bool commit = true) => Task.CompletedTask;

        public Task<List<Jogo>> GetMaisAlugadosAsync() => throw new NotImplementedException();
        public Task<List<Jogo>> GetNovidadesAsync(int quantidade = 3) => throw new NotImplementedException();
        public Task<List<Jogo>> GetAllByIdsAsync(List<int> ids) => throw new NotImplementedException();
        public Task<List<JogoCopia>> GetAllCopiasByIdsAsync(List<int> ids) => throw new NotImplementedException();
        public Task<List<JogoCopia>> GetAllCopiasByIdJogoAsync(int idJogo) => throw new NotImplementedException();
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
