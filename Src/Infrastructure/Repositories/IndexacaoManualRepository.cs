using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Application.UseCases.RAG;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Infrastructure.Repositories;

public interface IIndexacaoManualRepository : IBaseRepository {
    Task<List<ManualJob>> GetElegiveisAsync(int maxTentativas);
    Task<EstadoLinkManual?> GetEstadoAsync(int idJogoLink);
    Task<bool> ExisteIndexadoComHashAsync(int idJogo, string hash, int idJogoLinkExceto);
    Task<List<int>> GetIdsDuplicadosAsync(int idJogo, int idJogoLinkExceto);
    Task<JogoLinkIndexacao?> GetMetadadosPorHashAsync(string hash);
    Task<HashSet<int>> GetIdsComVetoresEsperadosAsync();
    Task<List<int>> GetIdsLinksAsync(int idJogo);
    Task SalvarAsync(JogoLinkIndexacao indexacao);
}

public class IndexacaoManualRepository(DatabaseContext context) : BaseRepository(context), IIndexacaoManualRepository {

    /// <summary>
    /// Links que ainda têm algo a fazer: sem linha, com a URL trocada, no meio de um
    /// processamento interrompido ou com falha que ainda não esgotou as tentativas.
    /// Jogo desativado fica de fora: indexar gastaria embedding e poluiria a busca.
    /// </summary>
    public async Task<List<ManualJob>> GetElegiveisAsync(int maxTentativas) {
        return await _dbContext.JogoLinks
            .Where(jl => jl.Tipo == TipoLink.Regra && jl.Url != "")
            .Where(jl => _dbContext.JogoCopias.Any(jc => jc.IdJogo == jl.IdJogo && jc.Status != StatusJogo.Desativado))
            .Where(jl => !_dbContext.JogoLinkIndexacoes.Any(i =>
                i.IdJogoLink == jl.Id &&
                i.Url == jl.Url &&
                (i.Status == StatusIndexacao.Indexado ||
                 (i.Status == StatusIndexacao.Falhou && i.Tentativas >= maxTentativas))))
            .Select(jl => new ManualJob(jl.Id, jl.IdJogo))
            .ToListAsync();
    }

    public async Task<EstadoLinkManual?> GetEstadoAsync(int idJogoLink) {
        return await _dbContext.JogoLinks
            .Where(jl => jl.Id == idJogoLink)
            .Select(jl => new EstadoLinkManual(
                jl.Id,
                jl.IdJogo,
                jl.Tipo,
                jl.Url,
                jl.Titulo,
                _dbContext.Jogos.Where(j => j.Id == jl.IdJogo).Select(j => j.Nome).FirstOrDefault() ?? "",
                _dbContext.JogoCopias.Any(jc => jc.IdJogo == jl.IdJogo && jc.Status != StatusJogo.Desativado),
                _dbContext.JogoLinkIndexacoes.FirstOrDefault(i => i.IdJogoLink == jl.Id)))
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Diz se outro link do mesmo jogo já indexou este conteúdo. Dois links com o mesmo
    /// PDF encheriam o topo da busca do jogo com o mesmo trecho repetido.
    /// </summary>
    public async Task<bool> ExisteIndexadoComHashAsync(int idJogo, string hash, int idJogoLinkExceto) {
        return await _dbContext.JogoLinkIndexacoes
            .AnyAsync(i => i.Status == StatusIndexacao.Indexado &&
                           i.HashPdf == hash &&
                           i.IdJogoLink != idJogoLinkExceto &&
                           _dbContext.JogoLinks.Any(jl => jl.Id == i.IdJogoLink && jl.IdJogo == idJogo));
    }

    public async Task<List<int>> GetIdsDuplicadosAsync(int idJogo, int idJogoLinkExceto) {
        return await _dbContext.JogoLinkIndexacoes
            .Where(i => i.Status == StatusIndexacao.Duplicado &&
                        i.IdJogoLink != idJogoLinkExceto &&
                        _dbContext.JogoLinks.Any(jl => jl.Id == i.IdJogoLink && jl.IdJogo == idJogo))
            .Select(i => i.IdJogoLink)
            .ToListAsync();
    }

    /// <summary>
    /// Metadados são do conteúdo, não do link: quem reaproveita o cache de um hash herda
    /// o modelo e a nota de quem pagou a extração.
    /// </summary>
    public async Task<JogoLinkIndexacao?> GetMetadadosPorHashAsync(string hash) {
        return await _dbContext.JogoLinkIndexacoes
            .Where(i => i.HashPdf == hash && i.ModeloExtracao != null)
            .OrderByDescending(i => i.DataAtualizacao)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Links que legitimamente têm vetores no Qdrant. O que estiver lá fora desta lista
    /// é órfão: link apagado, jogo desativado ou link que virou vídeo.
    /// </summary>
    public async Task<HashSet<int>> GetIdsComVetoresEsperadosAsync() {
        var ids = await _dbContext.JogoLinkIndexacoes
            .Where(i => i.Status == StatusIndexacao.Indexado)
            .Where(i => _dbContext.JogoLinks.Any(jl =>
                jl.Id == i.IdJogoLink &&
                jl.Tipo == TipoLink.Regra &&
                jl.Url != "" &&
                _dbContext.JogoCopias.Any(jc => jc.IdJogo == jl.IdJogo && jc.Status != StatusJogo.Desativado)))
            .Select(i => i.IdJogoLink)
            .ToListAsync();

        return [.. ids];
    }

    public async Task<List<int>> GetIdsLinksAsync(int idJogo) {
        return await _dbContext.JogoLinks
            .Where(jl => jl.IdJogo == idJogo)
            .Select(jl => jl.Id)
            .ToListAsync();
    }

    public async Task SalvarAsync(JogoLinkIndexacao indexacao) {
        indexacao.DataAtualizacao = DateTime.Now;
        await SaveChangesAsync(_dbContext.JogoLinkIndexacoes, indexacao);
    }
}
