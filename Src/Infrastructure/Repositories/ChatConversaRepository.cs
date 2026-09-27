using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Infrastructure.Repositories;

public interface IChatConversaRepository {

    /// <summary>A conversa, só se for deste usuário: a chave sozinha não dá acesso.</summary>
    Task<ChatConversa?> ObterAsync(Guid chave, string idUsuario);

    /// <summary>Grava a conversa (criando se for nova) e acrescenta a mensagem do turno.</summary>
    Task SalvarAsync(ChatConversa conversa, ChatMensagem mensagem);

    /// <summary>Exclusão de conta (LGPD): as perguntas são dado do titular.</summary>
    Task ExcluirDoUsuarioAsync(string idUsuario);
}

public class ChatConversaRepository(DatabaseContext _dbContext) : IChatConversaRepository {

    public Task<ChatConversa?> ObterAsync(Guid chave, string idUsuario) =>
        _dbContext.ChatConversas
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Chave == chave && c.IdUsuario == idUsuario);

    public async Task SalvarAsync(ChatConversa conversa, ChatMensagem mensagem) {
        // O contexto nao rastreia por padrao: a conversa lida em ObterAsync volta como update.
        if (conversa.Id == 0) {
            _dbContext.ChatConversas.Add(conversa);
        } else {
            _dbContext.ChatConversas.Update(conversa);
        }

        await _dbContext.SaveChangesAsync();

        mensagem.IdConversa = conversa.Id;
        _dbContext.ChatMensagens.Add(mensagem);
        await _dbContext.SaveChangesAsync();
    }

    public Task ExcluirDoUsuarioAsync(string idUsuario) =>
        _dbContext.ChatConversas.Where(c => c.IdUsuario == idUsuario).ExecuteDeleteAsync();
}
