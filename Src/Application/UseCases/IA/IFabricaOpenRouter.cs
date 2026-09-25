using Microsoft.Extensions.AI;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Application.UseCases.IA;

/// <summary>
/// Único lugar que constrói cliente de LLM. Existe para que nenhum nasça sem o registro de
/// uso: escapar do ledger passa a exigir intenção, não esquecimento.
/// </summary>
public interface IFabricaOpenRouter {

    /// <param name="operacao">Para que serve este cliente. Vai em toda linha do ledger.</param>
    IChatClient CriarChat(string modelo, OperacaoLlm operacao, TimeSpan timeout, int tentativas);

    IEmbeddingGenerator<string, Embedding<float>> CriarEmbedding(string modelo);
}
