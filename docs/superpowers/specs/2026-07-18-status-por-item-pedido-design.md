# Status por item de pedido — devolução e renovação parciais

**Data:** 2026-07-18
**Projetos afetados:** `ProximoTurnoApi` (domínio, persistência, DTO) e `ProximoTurno` (frontend)

## Problema

Ao renovar um pedido, se apenas alguns itens forem solicitados, deve-se renovar **apenas eles**; os itens restantes devem **continuar no pedido existente** com status `Entregue`, e o novo pedido deve conter **apenas os renovados**.

Hoje isso é impossível porque o status de locação existe apenas no **pedido** (`Pedido.Status`), não no item. Consequências no código atual:

- `Pedido.Devolver(ids)` marca as cópias informadas como disponíveis, mas seta o **pedido inteiro** para `Devolvido`, mesmo numa devolução parcial.
- `RenovarPedido` "devolve" os itens **não** renovados (invertido em relação ao requisito) e força o pedido a `Devolvido` antes de renovar.

Um pedido não consegue estar **parcialmente devolvido e parcialmente renovado/entregue**.

## Decisão de arquitetura

Abordagem **híbrida**: adicionar status por item e manter `Pedido.Status` como um **agregado derivado** dos itens.

Descartamos mover o status por completo para o item (removendo `Pedido.Status`): `Pendente` e `Cancelado` são genuinamente do pedido, e ~9 consultas/telas (incluindo SQL cru em `JogoRepository` e o badge do frontend) assumem um único status por pedido. O híbrido entrega a granularidade necessária no trecho `Entregue → Devolvido` com raio de impacto muito menor.

### Enum compartilhado

`ItemPedido.Status` reusa o mesmo enum `StatusPedido { Pendente, Entregue, Devolvido, Cancelado }`. Item e pedido têm, literalmente, os mesmos valores — o que dá sentido à derivação (um item de pedido recém-criado é `Pendente`, igual ao pedido).

### `Pedido.Status` como agregado mantido

`Pedido.Status` continua sendo **coluna gravada** (as consultas filtram `p.Status` no banco — não pode virar propriedade calculada só em memória), mas passa a ser **recalculado a cada mudança de status de item**, pela regra de precedência:

```
todos os itens Cancelado   -> Cancelado
algum item Pendente        -> Pendente
algum item Entregue        -> Entregue      (pedido parcial: sobrou item fora)
senão (todos Devolvido)    -> Devolvido
```

Isso preserva o comportamento atual nos casos totais (renovar/devolver todos → `Devolvido`) e habilita o caso parcial (sobrou item `Entregue` → pedido continua `Entregue`).

### Remoção do campo `RENOVADO`

`ItemPedido.Renovado` é removido (coluna + propriedade). Ele é redundante: seu único consumidor funcional é o badge "renovado" no dialog de detalhes, e ele só é setado nos itens do pedido **novo**. Como um pedido de renovação, por construção, contém **apenas** itens renovados, a informação equivale a `pedido.PedidoOriginal != null`.

`Status` **não** substitui `Renovado` (um item renovado fica `Entregue` no pedido novo e `Devolvido` no antigo — indistinguível de aluguel/devolução normal). Quem substitui é o vínculo de nível pedido `PedidoOriginal`.

**Trade-off aceito:** no pedido antigo não se distingue, item a item, "devolvido normal" de "saiu por renovação" (ambos ficam `Devolvido` sem marcador). Isso já é assim hoje e nenhum comportamento depende dessa distinção (ver decisão de comentários abaixo).

**Risco menor registrado:** se um dia um pedido de renovação passar a aceitar itens novos misturados, `PedidoOriginal != null` marcaria itens frescos como renovados. Hoje é impossível (o pedido vira `Entregue` e trava novas inclusões).

## Mudanças no domínio (`Pedido.cs` / `ItemPedido.cs`)

### `ItemPedido`
- Adicionar `public StatusPedido Status { get; set; }`.
- Remover `public bool Renovado`.

### `Pedido`
- Adicionar `public int? IdPedidoOriginal { get; init; }` mapeado na FK `ID_PEDIDO_ORIGINAL` (que já existe), para derivar renovação sem `Include` da navegação.
- Novo método privado `RecalcularStatus()` que aplica a regra de precedência acima sobre `_items`.

Transições:

| Método | Efeito nos itens | Efeito no pedido |
|---|---|---|
| `AdicionarItem` | item nasce `Pendente` | permanece `Pendente` |
| `Entregar` | todos → `Entregue` | `Entregue` (explícito) |
| `Cancelar` | todos → `Cancelado` | `Cancelado` (explícito) |
| `Devolver(ids)` | itens de `ids` (ou todos, se null/vazio) que estão `Entregue` → `Devolvido`; cópia → `Disponivel` | `RecalcularStatus()` |
| `Renovar(itens)` | itens renovados → `Devolvido` (cópia migra p/ novo pedido, permanece alugada); não-renovados intocados (`Entregue`) | `RecalcularStatus()` |

Regras/guardas:
- `Devolver`: exige que o pedido tenha ao menos um item `Entregue`; ignora itens já `Devolvido` (idempotente); notifica se nenhum item elegível foi devolvido.
- `Renovar`: exige pedido `Entregue`; valida que cada item renovado pertence ao pedido e está `Entregue`. Deixa de exigir `Status == Devolvido`. A criação do novo pedido (novo `Pedido` → `AdicionarItem` × N → `Entregar`) permanece encapsulada aqui; o "fechamento" da perna antiga (itens renovados → `Devolvido`) passa a ser feito neste mesmo método, mantendo toda a invariante dentro de `Pedido`.

### Fluxo de cópia (`JogoCopia.Status`) na renovação
Para cada item renovado: a cópia continua com o cliente (migra para o novo pedido). Mantém-se o mecanismo atual — a cópia é liberada momentaneamente (`Disponivel`) para `AdicionarItem` do novo pedido revalidar e reservar, terminando `Alugado` após `Entregar`. Itens não-renovados mantêm cópia `Alugado`.

## Reescrita do use case `RenovarPedido`

- Carrega o pedido (deve estar `Entregue`).
- Resolve período de cada item solicitado (como hoje).
- Remove o passo atual de `Devolver(idsItensNaoRenovados)`.
- Chama `pedidoExistente.Renovar(itensRenovar, cache)`, que fecha a perna antiga dos itens renovados e retorna o novo pedido.
- Persiste ambos (`SaveAsync(pedidoExistente, false)` + `SaveAsync(novoPedido)`) e enfileira contrato do novo pedido (como hoje).

`DevolverItensPedido` não muda de estrutura — passa a produzir devolução parcial automaticamente, pois `Pedido.Devolver` agora é parcial.

## Persistência / migration

Nova migration EF Core:
- `PEDIDO_ITEM`: adicionar coluna `STATUS` (`short`, not null, default `0` = `Pendente`), com `HasConversion<short>()` no `DatabaseContext` (mapeamento de `ItemPedido`).
- Backfill dos dados existentes: `UPDATE PEDIDO_ITEM pi JOIN PEDIDO p ON pi.ID_PEDIDO = p.ID SET pi.STATUS = p.STATUS;` (todo item herda o status atual do pedido pai).
- `PEDIDO_ITEM`: remover coluna `RENOVADO`.
- Mapear `Pedido.IdPedidoOriginal` na coluna `ID_PEDIDO_ORIGINAL` já existente (sem alteração de schema; ajuste no `HasOne(...).HasForeignKey(...)` para usar a propriedade CLR).

## DTOs

`ItemPedidoDTO`:
- Adicionar `public StatusPedido Status { get; set; }`.
- `Renovado` passa a ser derivado no `PedidoDTO.FromModel`: `Renovado = pedido.IdPedidoOriginal != null` (aplicado a todos os itens).

`PedidoDTO.FromModel`:
- `Atrasado` passa a considerar só itens `Entregue` vencidos:
  `Atrasado = pedido.Items.Any(i => i.Status == StatusPedido.Entregue && i.DataDevolucao.Date < DateTime.Today)`.

## Consultas afetadas

Quase todas continuam iguais (perguntam pelo `Pedido.Status`, que segue gravado e mantido). Mudam:

- **`PedidoRepository:46`** (filtro "Atrasados"): passa a `p.Items.Any(i => i.Status == StatusPedido.Entregue && i.DataDevolucao.Date < DateTime.Today)`.
- **`SalvarComentario:47`** e **`PodeComentarJogo:30`** (elegibilidade de comentário): de "pedido `Devolvido`" para "existe `ItemPedido` daquele jogo com `Status == Devolvido`", independente do status do pedido. (Decisão (b): qualquer item devolvido libera, inclusive renovado.)

Sem mudança: `ObterRelatorioFaturamento` (`Entregue || Devolvido`), `JogoRepository:175` (SQL, `Entregue`), `CupomRepository` (`!= Cancelado`), `GerarContratoPedido` (`Pendente || Entregue`), filtro por status em `PedidoRepository:41`.

## Frontend (`ProximoTurno`)

- Tipos TS (`lib/api-service.ts`): `ItemPedido` ganha `status: number`; `renovado` continua (agora derivado no backend).
- `components/pedidos/pedido-detalhes-dialog.tsx`: além do badge "renovado", adicionar badge de status por item (Entregue / Devolvido) para visualizar o pedido parcial.
- `app/pedidos/page.tsx`: badge de status do pedido (nível lista) inalterado; ações de renovar/devolver seguem habilitadas enquanto o pedido está `Entregue` (`status === 1`), o que se mantém correto em pedidos parciais.

## Testes

- **Domínio (`Pedido`)**:
  - `Devolver` parcial: devolve subconjunto → itens corretos `Devolvido`, restantes `Entregue`, pedido `Entregue`; devolve todos → pedido `Devolvido`.
  - `RecalcularStatus`: cobre as 4 regras de precedência.
  - `Renovar` parcial: itens renovados `Devolvido` no antigo, não-renovados `Entregue`, pedido antigo `Entregue`; novo pedido só com renovados, `Entregue`.
  - `Renovar` total: pedido antigo `Devolvido` (paridade com hoje).
- **Use case (`RenovarPedidoTests`)**: atualizar assert que hoje espera pedido original `Devolvido` no caso total; adicionar caso parcial (original permanece `Entregue`).
- **Comentário**: elegibilidade por item devolvido (pedido ainda `Entregue`).

## Fora de escopo

- Distinguir, no pedido antigo, "devolvido normal" vs "renovado-out" por item.
- Seleção parcial na tela de devolução (o backend já suporta; a UI de devolução não é alterada nesta etapa além dos badges).
