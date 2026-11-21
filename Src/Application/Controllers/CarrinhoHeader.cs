namespace ProximoTurnoApi.Application.Controllers;

/// <summary>
/// O front gera um id para o carrinho (um GUID guardado no navegador) e o manda neste header.
/// É ele que identifica de quem é a reserva, logado ou não.
/// </summary>
public static class CarrinhoHeader {
    public const string Nome = "X-Carrinho-Id";

    /// <summary>O id do carrinho da requisição, ou null se o header falta ou não é um GUID.</summary>
    public static Guid? Ler(HttpRequest request) =>
        Guid.TryParse(request.Headers[Nome].ToString(), out var id) && id != Guid.Empty ? id : null;
}
