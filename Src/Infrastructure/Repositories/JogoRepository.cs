using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Infrastructure.Repositories;

public interface IJogoRepository : IBaseRepository {
    Task<List<Jogo>> GetAllAsync(FiltroJogoDTO filtro);
    Task<List<JogoMaisAlugado>> GetMaisAlugadosAsync();
    Task<List<Jogo>> GetAllByIdsAsync(List<int> ids);
    Task<List<JogoCopia>> GetAllCopiasByIdsAsync(List<int> ids);
    Task<List<JogoCopia>> GetAllCopiasByIdJogoAsync(int idJogo);
    Task<Jogo?> GetByIdAsync(int id);
    Task<List<JogoCopia>> GetCopiasAsync(int id);
    Task<JogoCopia?> GetCopiaByIdAsync(int id);
    Task SaveAsync(Jogo jogo, bool commit = true);
    Task SaveAsync(JogoCopia jogo, bool commit = true);
    Task<bool> DeleteAsync(int id);
    Task<bool> ExisteAsync(int id);
    Task<bool> CopiaExisteAndDisponivel(int id);
}

public class JogoRepository : BaseRepository, IJogoRepository {

    public JogoRepository(DatabaseContext context) : base(context) {

    }

    public async Task<List<Jogo>> GetAllAsync(FiltroJogoDTO filtro) {
        // Listamos as colunas explicitamente para evitar conflitos de nomes (ex: ID e id)
        var sql = @"SELECT j.* 
                    FROM JOGO j";
        var where = new List<string>();
        var parameters = new List<MySqlParameter>();

        if (!string.IsNullOrEmpty(filtro.Nome)) {
            where.Add("j.NOME LIKE @NOME");
            parameters.Add(new MySqlParameter("@NOME", $"%{filtro.Nome.ToLowerInvariant()}%"));
        }

        if (filtro.IdCategoria.HasValue) {
            where.Add("j.ID_CATEGORIA = @ID_CATEGORIA");
            parameters.Add(new MySqlParameter("@ID_CATEGORIA", filtro.IdCategoria.Value));
        }

        if (filtro.Tags != null && filtro.Tags.Count > 0) {
            // Para tags, usamos uma subquery ou join. Subquery é mais segura com FromSqlRaw.
            where.Add($"EXISTS (SELECT 1 FROM JOGO_TAG jt WHERE jt.ID_JOGO = j.ID AND jt.ID_TAG IN ({string.Join(",", filtro.Tags)}))");
        }

        if (filtro.Status.HasValue) {
            where.Add("EXISTS (SELECT 1 FROM JOGO_COPIA jc WHERE jc.ID_JOGO = j.ID AND jc.STATUS = @STATUS)");
            parameters.Add(new MySqlParameter("@STATUS", (short)filtro.Status.Value));
        }

        if (filtro.IdadeMinima.HasValue) {
            where.Add("j.IDADE_MINIMA <= @IDADE_MINIMA");
            parameters.Add(new MySqlParameter("@IDADE_MINIMA", filtro.IdadeMinima.Value));
        }

        if (filtro.QtdeJogadores.HasValue) {
            where.Add("j.MINIMO_JOGADORES <= @QTDE_JOGADORES AND j.MAXIMO_JOGADORES >= @QTDE_JOGADORES");
            parameters.Add(new MySqlParameter("@QTDE_JOGADORES", filtro.QtdeJogadores.Value));
        }

        // Filtro fixo para não trazer jogos desativados
        where.Add("EXISTS (SELECT 1 FROM JOGO_COPIA jc WHERE jc.ID_JOGO = j.ID AND jc.STATUS != @STATUS_DESATIVADO)");
        parameters.Add(new MySqlParameter("@STATUS_DESATIVADO", (short)StatusJogo.Desativado));

        if (where.Count > 0) {
            sql += " WHERE " + string.Join(" AND ", where);
        }

        // O uso de .AsSplitQuery() melhora muito a performance ao carregar múltiplas coleções.
        return await _dbContext.Jogos
            .FromSqlRaw(sql, [.. parameters])
            .Include(j => j.Categoria)
            .Include(j => j.Tags)
            .Include(j => j.Links)
            .Include(j => j.Copias)
            .AsSplitQuery()
            .AsNoTracking()
            .OrderBy(j => j.Nome)
            .ToListAsync();
    }

    public async Task<Jogo?> GetByIdAsync(int id) {
        return await _dbContext.Jogos
            .Include(j => j.Tags)
            .Include(j => j.Links)
            .Include(j => j.Fotos)
            .Include(j => j.Copias)
            .AsTracking()
            .FirstOrDefaultAsync(j => j.Id == id);
    }


    public async Task<JogoCopia?> GetCopiaByIdAsync(int id) {
        return await _dbContext.JogoCopias
           .Include(jc => jc.Jogo)
           .AsTracking()
           .FirstOrDefaultAsync(j => j.Id == id);
    }

    public async Task SaveAsync(Jogo jogo, bool commit = true) {
        await SaveChangesAsync(_dbContext.Jogos, jogo, commit);
    }

    public async Task SaveAsync(JogoCopia jogoCopia, bool commit = true) {
        await SaveChangesAsync(_dbContext.JogoCopias, jogoCopia, commit);
    }

    public async Task<bool> DeleteAsync(int id) {
        return await _dbContext.Jogos
            .Where(j => j.Id == id)
            .ExecuteDeleteAsync() > 0;
    }

    public Task<bool> ExisteAsync(int id) {
        return _dbContext.Jogos.AnyAsync(j => j.Id == id);
    }

    public Task<List<Jogo>> GetAllByIdsAsync(List<int> ids) {
        return _dbContext.Jogos
            .Where(j => ids.Contains(j.Id))
            //Nao quero carregar todos os dados do jogo
            .Select(j => new Jogo {
                Id = j.Id,
                Nome = j.Nome
            })
            .AsTracking()
            .ToListAsync();
    }

    public async Task<List<JogoCopia>> GetAllCopiasByIdsAsync(List<int> ids) {
        return await _dbContext.JogoCopias
            .Where(c => ids.Contains(c.Id))
            .AsTracking()
            .ToListAsync();
    }

    public async Task<List<JogoMaisAlugado>> GetMaisAlugadosAsync() {
        return await _dbContext.Database
            .SqlQuery<JogoMaisAlugado>(@$"
            select count(*) as qtde,
                   j.ID,
                   j.NOME,
                   (select URL from JOGO_FOTO jf where jf.ID_JOGO = j.ID order by jf.ORDEM limit 1) as FOTO
              from PEDIDO p
            inner join PEDIDO_ITEM pi ON (p.ID = pi.ID_PEDIDO)
            inner join JOGO_COPIA jc on (jc.ID = pi.ID_JOGO_COPIA)
            inner join JOGO j on (j.ID = jc.ID_JOGO)
            where p.STATUS = {(short)StatusPedido.Entregue}
            group by j.ID, j.NOME
            order by count(*) desc 
            limit 3")
            .ToListAsync();
    }

    public Task<bool> CopiaExisteAndDisponivel(int idCopia) {
        return _dbContext.Jogos
            .Include(j => j.Copias)
            .AnyAsync(j => j.Copias != null && j.Copias.Any(c => c.Id == idCopia && c.Status == StatusJogo.Disponivel));
    }

    public async Task<List<JogoCopia>> GetAllCopiasByIdJogoAsync(int idJogo) {
        return await _dbContext.JogoCopias
            .Where(c => c.IdJogo == idJogo)
            .AsTracking()
            .ToListAsync();
    }

    public async Task<List<JogoCopia>> GetCopiasAsync(int idJogo) {
        return await _dbContext.JogoCopias
            .Where(jc => jc.IdJogo == idJogo)
            .ToListAsync();
    }
}
