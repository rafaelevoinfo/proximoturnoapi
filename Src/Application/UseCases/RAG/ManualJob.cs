namespace ProximoTurnoApi.Application.UseCases.RAG;

/// <summary>
/// Pedido de sincronização de um manual. Só ids: a URL e o estado vêm do banco na hora de
/// executar, senão um job parado na fila carregaria uma foto velha do link.
/// O IdJogo serve para achar os duplicados quando o link já nem existe mais; vale 0 quando
/// quem enfileirou não sabe o jogo (reconciliação).
/// Forcar refaz tudo, inclusive a extração do PDF, mesmo com o link já indexado ou com as
/// tentativas esgotadas: é o pedido do admin de reindexar o manual.
/// </summary>
public record ManualJob(int IdJogoLink, int IdJogo, bool Forcar = false);
