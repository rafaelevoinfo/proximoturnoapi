# Renovação pela tela de pedido — Design

**Data:** 2026-10-09
**Projetos afetados:** `ProximoTurnoApi` (use case, DTO, controller) e `ProximoTurno` (tela de pedido e lista de pedidos)

## Problema

Hoje a renovação é um modal na lista de pedidos (`app/pedidos/page.tsx`), com duas variações:

- **Renovar:** renova todos os itens ainda entregues, com uma data de devolução opcional para todos.
- **Renovar Parcial:** permite marcar itens, trocar o período e informar uma data por item ou uma global.

Ao confirmar, `RenovarPedido` / `Pedido.Renovar` fecha os itens renovados no pedido original (`Devolvido`) e cria um pedido novo, já entregue e ligado ao original por `PedidoOriginal`, com contrato próprio.

Problemas do fluxo atual:

1. **Não dá para revisar a renovação como um pedido:** não há resumo de valores e não é possível aplicar cupom nem escolher a forma de pagamento.
2. **A data de devolução manual fica aberta a qualquer um:** `ItemPedidoRenovarDTO.DataDevolucao` é aceita de qualquer usuário. O modal só aparece com essa opção no front, mas um cliente consegue mandar qualquer data chamando a API.
3. **O período não é validado contra a categoria do jogo:** `RenovarPedido` resolve o `IdPeriodo` no cache, mas não confere se ele pertence à categoria do jogo, ao contrário de `ValidarAdicionarItem` no cadastro.
4. **Itens inválidos são ignorados em silêncio:** um id que não pertence ao pedido só gera log, e a renovação segue com os demais.

## Objetivo

"Renovar" passa a abrir a **tela de pedido** (`/pedidos/novo`) pré-preenchida com os itens do pedido, num modo de renovação. Nela é possível revisar períodos, remover itens, aplicar cupom e escolher a forma de pagamento antes de confirmar.

## Decisões

1. **Renovar não inclui jogos novos.** Nem admin nem cliente podem incluir jogos na renovação; só podem **remover** itens. O pedido de renovação continua contendo apenas itens renovados, o que mantém válida a premissa do spec de status por item (`PedidoOriginal != null` ⇒ todos os itens são renovados). A garantia é estrutural: o DTO só referencia ids de itens do pedido original, então não existe como mandar um jogo novo.
2. **Cliente escolhe o período como num pedido novo.** Para cada jogo, ele escolhe um dos períodos da categoria. A data de devolução é **data atual + dias do período**, calculada pelo backend (`CalcularDataDevolucao`, com a entrega do pedido novo feita no momento da renovação). O cliente **não** informa data.
3. **Admin pode, além disso, informar a data de devolução:** uma **por jogo** ou **uma para todos**. A data por jogo tem preferência sobre a global. Sem data, vale o cálculo do item 2. A data informada precisa ser posterior a hoje, mesma regra da entrega.
4. **O registro continua sendo um pedido novo vinculado ao original.** Não muda o modelo atual: `Pedido.Renovar` cria o pedido novo `Entregue`, com `PedidoOriginal`, e fecha as pernas antigas dos itens renovados. Os itens removidos na tela continuam `Entregue` no pedido original, aguardando devolução.
5. **Cupom e pagamento.** A renovação aceita **um cupom**, validado por `ValidarCupom` com o cliente do pedido original e os itens/períodos escolhidos. O cliente escolhe a **forma de pagamento**, obrigatória como num pedido novo. A **forma de entrega não se aplica**, porque os jogos já estão com o cliente, então a tela não pede entrega nem cobra taxa de entrega.
6. **O antigo "Renovar Parcial" deixa de existir.** Remover itens na tela já cobre a renovação parcial, então o dropdown e os dois modais saem da lista de pedidos.

## Backend

### DTO

O corpo de `PUT /api/pedidos/{id}/renovar` deixa de ser uma lista e passa a ser um objeto:

```csharp
public class RenovarPedidoDTO {
    public List<ItemPedidoRenovarDTO> Itens { get; set; } = [];

    /// <summary>Só admin. Vale para os itens sem data própria.</summary>
    public DateTime? DataDevolucao { get; set; }

    public string? CupomCodigo { get; set; }
    public string? MetodoPagamento { get; set; }
}

public class ItemPedidoRenovarDTO {
    public int Id { get; set; }              // item do pedido original
    public int? IdPeriodo { get; set; }      // null = mantém o período atual
    public DateTime? DataDevolucao { get; set; } // só admin
}
```

Front e back mudam juntos, então a quebra de contrato do corpo é aceitável. Se preferir manter compatibilidade, a alternativa é um endpoint novo (`POST /api/pedidos/{id}/renovacao`) e remover o antigo depois.

### `RenovarPedido`

Assinatura: `ExecuteAsync(SolicitantePedido solicitante, int idPedido, RenovarPedidoDTO dto)`.

Validações, todas com notificação, na ordem:

1. Pedido existe; senão `BadRequest`.
2. `PodeAlterar(solicitante, pedido)`: admin ou dono; senão `Forbid` (já existe).
3. Pedido `Entregue` (regra atual de `Pedido.Renovar`).
4. Pelo menos um item informado.
5. Todo `Id` informado pertence ao pedido e está `Entregue`. Diferente de hoje, um id inválido **rejeita a renovação** em vez de ser ignorado.
6. Ids repetidos são rejeitados.
7. Período: `IdPeriodo` (ou o atual, quando null) existe no cache **e pertence à categoria do jogo**. Mesma regra de `ValidarAdicionarItem`.
8. Data de devolução, global ou por item, informada por quem **não é admin** gera `Forbid`. Informada por admin, precisa ser posterior a hoje.
9. `MetodoPagamento` obrigatório.
10. Cupom, quando informado: `ValidarCupom` com `IdCliente` do pedido original e os pares `(IdJogo, IdPeriodo)` dos itens renovados. Se inválido, `BadRequest` com a mensagem do cupom.

Montagem:

- `Pedido.Renovar` recebe os itens com o período resolvido e a data efetiva: a do item, senão a global, senão null para calcular pelo período. Ele cria o pedido novo, entrega e sobrescreve as datas informadas, como já faz hoje.
- Depois de `Renovar`: `novoPedido.DefinirMetodos(dto.MetodoPagamento, null)` e, havendo cupom, `novoPedido.AplicarCupom(idCupom, desconto)`.
- Persistência atômica, mantendo o padrão atual: `SaveAsync(original, commit: false)` só anexa o original ao contexto, e `SaveAsync(novo)` grava os dois num único `SaveChanges`. Como a validação do cupom e das datas acontece antes, nada é gravado se a renovação for rejeitada.
- Enfileirar o contrato do pedido novo, como hoje.

### Controller

`RenovarPedido` passa a receber `RenovarPedidoDTO` e mapeia `Forbid` para 403, como já faz após o PR de autorização.

## Frontend

### Lista de pedidos (`app/pedidos/page.tsx`)

- O botão **Renovar** (pedido `Entregue`, para admin e para o dono) passa a navegar para `/pedidos/novo?renovar={id}`.
- Saem o dropdown "Renovar Parcial", os dois modais de renovação e os estados e handlers associados (`itensRenovacao`, `dataDevolucaoRenovacaoGlobal`, `handleConfirmarRenovarTodos` etc.).

### Tela de pedido (`app/pedidos/novo/page.tsx`) em modo renovação

Ativado por `?renovar={id}`. Carrega o pedido com `getPedidoById` e usa só os itens `Entregue`. Se o pedido não estiver `Entregue`, ou o usuário não puder acessá-lo, a tela avisa e volta para `/pedidos`.

| Elemento | Pedido novo / edição | Renovação |
| --- | --- | --- |
| Título | "Novo Pedido" / "Editar Pedido #N" | "Renovar Pedido #N" |
| Cliente | escolhido (admin) ou o logado | fixo, o do pedido; sem busca |
| Catálogo / busca de jogos | visível | **oculto**; não há como incluir jogo |
| Itens | adicionar e remover | só **remover**; é preciso manter pelo menos um |
| Período por jogo | select da categoria | select da categoria, pré-selecionado com o período atual |
| Devolução prevista | — | por jogo: "hoje + dias do período", recalculada ao trocar o período |
| Data de devolução manual | — | **só admin**: campo por jogo e campo "aplicar a todos"; a do jogo tem preferência |
| Forma de entrega e taxa | obrigatória | **oculta**, sem taxa |
| Forma de pagamento | obrigatória | obrigatória |
| Cupom | validar e aplicar | validar e aplicar, com `idCliente` do pedido e sem `idPedido` |
| Botão | "Criar Pedido" / "Salvar Alterações" | "Confirmar Renovação" |

- **Data por jogo:** quando o admin informa uma data para o jogo, a "devolução prevista" desse item mostra essa data. Quando informa a data global, ela vale para os itens sem data própria.
- **Ao confirmar:** chama `apiService.renovarPedido(id, { itens, dataDevolucao, cupomCodigo, metodoPagamento })`, mostra o toast de sucesso e volta para `/pedidos`.
- **Erros da API:** são exibidos com `getErrorMessage`; um 403 vira a mensagem "Você não tem permissão para renovar este pedido".
- **Datas no cliente:** a devolução prevista mostrada no front é só informativa. A data que vale é a calculada pelo backend no momento da confirmação.

## Testes

**Backend** (`RenovarPedidoTests`):

- Renova com período trocado e calcula a data como hoje + dias do novo período.
- Renova só os itens informados; os não informados continuam `Entregue` no original.
- Id de item de outro pedido, repetido ou não `Entregue`: rejeita e não salva nada.
- Período de outra categoria: rejeita.
- Cliente enviando data de devolução (global ou por item): `Forbid`, sem salvar.
- Admin com data global e data por item: a do item prevalece, a global vale para os demais.
- Admin com data de hoje ou passada: rejeita.
- Sem forma de pagamento: rejeita.
- Cupom válido: desconto aplicado no pedido novo. Cupom inválido: rejeita e não salva.
- Contrato enfileirado só para o pedido novo.

**Frontend** (manual, sem suíte automatizada no projeto):

- Cliente: abre a renovação, troca o período, remove um item, aplica cupom e confirma. A data prevista acompanha o período, e não aparece campo de data nem catálogo.
- Admin: o mesmo, e também data por jogo e "aplicar a todos".
- O item removido continua no pedido original como entregue.

## Fora de escopo

- Incluir jogos novos na renovação (decisão 1).
- Renovação automática ou lembrete de vencimento.
- Mudar a cobrança. A renovação continua cobrando o valor cheio do período escolhido, sem proporcional.

## Pontos em aberto

- **Cupom de uso único** (`LimiteUsoCliente = 1`): deve valer também em renovação, ou renovação deveria ser excluída de certos cupons, como os de primeiro aluguel? Hoje `ValidarCupom` não distingue renovação; a proposta é não distinguir nesta etapa.
- **Prazo para o cliente renovar:** pode renovar a qualquer momento enquanto o pedido estiver `Entregue`, inclusive com a devolução vencida? A proposta é manter como hoje, sem restrição de prazo.
