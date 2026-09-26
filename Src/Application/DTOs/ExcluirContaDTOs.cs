namespace ProximoTurnoApi.Application.DTOs;

/// <summary>Corpo do DELETE /api/clientes/{id}/conta. Senha é obrigatória quando o próprio cliente solicita.</summary>
public class ExcluirContaRequestDTO {
    public string? Senha { get; set; }
}

/// <summary>Pedido que impede a exclusão, devolvido no 409 para o frontend montar a tela.</summary>
public record PedidoEmAbertoDTO(int Id, DateTime DataHora, List<string> Jogos);
