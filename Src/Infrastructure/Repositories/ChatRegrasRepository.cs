using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Infrastructure.Repositories;

/// <summary>Jogo que pode ser assunto do chat, e se já tem manual indexado para a busca.</summary>
public sealed record JogoChat(int Id, string Nome, bool TemManual);

public interface IChatRegrasRepository {

    /// <summary>
    /// Soma do valor base (o <c>VALOR</c> do item, sem cupom e sem taxa de entrega) dos itens
    /// alugados pelo cliente deste e-mail. Conta item entregue ou devolvido; pendente ainda
    /// pode ser cancelado e cancelado nunca conta.
    /// </summary>
    Task<decimal> SomarValorBaseAlugadoAsync(string email);

    /// <summary>Soma do que o usuário já gastou no chat, em reais.</summary>
    Task<decimal> SomarGastoAsync(string idUsuario);

    /// <summary>Jogos com pelo menos um manual indexado: os únicos que o chat sabe responder.</summary>
    Task<List<JogoChat>> ListarJogosComManualAsync();

    Task<JogoChat?> ObterJogoAsync(int idJogo);
}

public class ChatRegrasRepository(DatabaseContext _dbContext) : IChatRegrasRepository {

    public async Task<decimal> SomarValorBaseAlugadoAsync(string email) {
        var emailNormalizado = email.ToLowerInvariant();

        return await _dbContext.Pedidos
            .Where(p => p.Cliente.Email == emailNormalizado)
            .SelectMany(p => p.Items)
            .Where(i => i.Status == StatusPedido.Entregue || i.Status == StatusPedido.Devolvido)
            .SumAsync(i => (decimal?)i.Valor) ?? 0m;
    }

    public async Task<decimal> SomarGastoAsync(string idUsuario) =>
        await _dbContext.UsosLlm
            .Where(u => u.IdUsuario == idUsuario)
            .SumAsync(u => u.CustoBrl) ?? 0m;

    public async Task<List<JogoChat>> ListarJogosComManualAsync() {
        var idsComManual = IdsJogosComManual();

        return await _dbContext.Jogos
            .Where(j => idsComManual.Contains(j.Id))
            .OrderBy(j => j.Nome)
            .Select(j => new JogoChat(j.Id, j.Nome, true))
            .ToListAsync();
    }

    public async Task<JogoChat?> ObterJogoAsync(int idJogo) {
        var idsComManual = IdsJogosComManual();

        return await _dbContext.Jogos
            .Where(j => j.Id == idJogo)
            .Select(j => new JogoChat(j.Id, j.Nome, idsComManual.Contains(j.Id)))
            .FirstOrDefaultAsync();
    }

    // Indexado e o unico status com vetores: Removido cobre o jogo desativado e o link que
    // virou video, e Duplicado aponta para outro link que ja tem os mesmos vetores.
    private IQueryable<int> IdsJogosComManual() =>
        from indexacao in _dbContext.JogoLinkIndexacoes
        join link in _dbContext.JogoLinks on indexacao.IdJogoLink equals link.Id
        where indexacao.Status == StatusIndexacao.Indexado
        select link.IdJogo;
}
