using Microsoft.AspNetCore.Mvc;
using ProximoTurnoApi.Domain;

namespace ProximoTurnoApi.Application.DTOs;

public class FiltroPedidoDTO {
    // Para não-admin, é sobrescrito pelo use case com o cliente do usuário logado.
    // Admin pode filtrar por um cliente específico via ?id_cliente=.
    [FromQuery(Name = "id_cliente")]
    public int? IdCliente { get; set; }
    [FromQuery(Name = "data_inicial")]
    public DateOnly? DataInicial { get; set; }
    [FromQuery(Name = "data_final")]
    public DateOnly? DataFinal { get; set; }
    /// <summary>
    /// Situacoes desejadas, combinadas por OU. <see cref="StatusPedido.Entregue"/> aqui
    /// significa "entregue e dentro do prazo"; os vencidos entram por <see cref="Atrasados"/>.
    /// </summary>
    [FromQuery(Name = "status")]
    public List<StatusPedido>? Status { get; set; }
    /// <summary>Inclui (por OU) os pedidos com item entregue e prazo vencido.</summary>
    [FromQuery(Name = "atrasados")]
    public bool Atrasados { get; set; }
}