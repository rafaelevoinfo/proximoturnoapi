namespace ProximoTurnoApi.Domain;

public enum TipoDesconto : short
{
    Percentual = 1,
    Fixo = 2,
    // Não dá desconto: troca o período dos itens pelo menor da categoria e fixa a devolução na data do cupom
    ExtensaoPrazo = 3
}
