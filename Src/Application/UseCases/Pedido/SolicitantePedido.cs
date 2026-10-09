namespace ProximoTurnoApi.Application.UseCases;

/// <summary>
/// Quem pede a alteração de um pedido. Montado pelo controller a partir do usuário logado:
/// admin altera qualquer pedido; os demais só os do cliente vinculado ao próprio e-mail.
/// </summary>
public sealed record SolicitantePedido(bool Admin, int? IdCliente) {
    public static readonly SolicitantePedido Administrador = new(true, null);

    public static SolicitantePedido DoCliente(int? idCliente) => new(false, idCliente);
}
