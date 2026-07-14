# Data de devolução na entrega do pedido — Design

**Data:** 2026-07-13
**Escopo:** Backend (.NET 10) + Frontend (Next.js)

## Problema

Ao entregar um pedido, o sistema deve permitir informar **opcionalmente** uma data de devolução:

- **Em branco (padrão):** o backend calcula a data automaticamente com base na **data da entrega** + a duração do período de locação de cada item.
- **Informada:** o backend valida que é uma data válida (**superior à data atual**) e a utiliza para todos os itens do pedido.

### Bug existente que este trabalho corrige

Hoje `Pedido.Entregar()` apenas faz `DataHoraEntrega = DateTime.Now` e **não recalcula** o `DataDevolucao` dos itens. Esse valor foi calculado no `CadastroPedido`, quando `DataHoraEntrega` ainda era `null` — portanto `CalcularDataDevolucao` usou a **data de cadastro** como base. Consequência: se a entrega ocorre dias após o cadastro, a data de devolução fica incorreta (presa à data de cadastro). O cálculo correto deve acontecer **na entrega**.

## Decisões de design (confirmadas)

1. Granularidade: **uma única data** por pedido. Quando informada, sobrescreve o `DataDevolucao` de todos os itens; quando em branco, cada item é recalculado pelo seu próprio período.
2. Origem da `QuantidadeDias` na entrega: um **cache em memória** de categorias/períodos, entregue ao método `Entregar()` **por parâmetro** (double dispatch), não por construtor da entidade.
3. Invalidação do cache: na **camada de infraestrutura** (`CategoriaRepository`), à prova de esquecimento.
4. Fallback quando o `IdPeriodo` do item não existir no cache: usar `QuantidadeDias = 1` e registrar `LogWarning` (não trava a entrega).
5. Regra de validação da data informada: a data (componente de data) deve ser **superior à data atual** (`dataDevolucao.Date > DateTime.Now.Date`); datas de hoje ou passadas são rejeitadas.

### Por que não injetar o cache no construtor de `Pedido`

O fluxo de entrega carrega o pedido por `PedidoRepository.GetByIdAsync`, e o EF Core materializa a entidade pelo **construtor privado sem parâmetros**, não pelo construtor de domínio. O EF não injeta singletons do DI da aplicação em construtores de entidade, então o campo do service ficaria `null` justamente na instância sobre a qual `Entregar()` roda. Além disso, entidade de domínio dependendo de service de infraestrutura fere a pureza/testabilidade. Por isso o cache é passado **como parâmetro do método**.

---

## Componentes

### 1. Cache de períodos (`ICategoriaPeriodoCache`) — Infrastructure/Services

Cache singleton, em memória, com snapshot imutável trocado atomicamente.

```csharp
public record CategoriaPeriodoInfo(
    int IdPeriodo,
    int QuantidadeDias,
    decimal Valor,
    int IdCategoria,
    string DescricaoCategoria);

public interface ICategoriaPeriodoCache {
    bool TryGetPeriodo(int idPeriodo, out CategoriaPeriodoInfo info);
    int GetQuantidadeDias(int idPeriodo, int defaultDias = 1); // fallback + LogWarning
    Task RefreshAsync();
}
```

- **Armazenamento:** `private volatile IReadOnlyDictionary<int, CategoriaPeriodoInfo> _porPeriodo` indexado por `IdPeriodo`. `RefreshAsync` monta um dicionário novo e troca a referência (atribuição de referência é atômica) — sem lock no caminho de leitura.
- **Carga:** carrega **todos** os períodos, inclusive de categorias inativas (`FiltroCategoriaDTO { ApenasAtivos = false }`), pois pedidos pendentes podem referenciar períodos de categorias já inativadas. Usa `IServiceScopeFactory` para abrir escopo e resolver `ICategoriaRepository` dentro do `RefreshAsync`.
- **`GetQuantidadeDias`:** se o `idPeriodo` não estiver no snapshot, retorna `defaultDias` (1) e emite `LogWarning`.
- **Registro:** `builder.Services.AddSingleton<ICategoriaPeriodoCache, CategoriaPeriodoCache>();`

### 2. Warm-up no startup (`IHostedService`)

Espelha o padrão já existente (`ContratoQueueBackgroundService`).

```csharp
public class CategoriaPeriodoCacheWarmup(ICategoriaPeriodoCache cache) : IHostedService {
    public Task StartAsync(CancellationToken ct) => cache.RefreshAsync();
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
```
Registro: `builder.Services.AddHostedService<CategoriaPeriodoCacheWarmup>();`

### 3. Invalidação na infraestrutura (`CategoriaRepository`)

`CategoriaRepository` recebe `ICategoriaPeriodoCache` por construtor e dispara `RefreshAsync()` após qualquer mutação persistida:

- `SaveAsync` (cobre `CadastroCategoria` e `AtualizarCategoria`, inclusive alterações de períodos) — refresh **após** o commit (quando `commit == true`).
- `DeleteAsync` (soft-delete que seta `Ativo=false`) — refresh após a operação.

Como o cache carrega inclusive inativos, o soft-delete não remove o período do cache — o refresh apenas mantém `Valor`/`QuantidadeDias`/descrição atualizados. Isso é intencional e coerente com o fallback.

### 4. Domínio — `Pedido.Entregar`

Assinatura nova (mantém compatibilidade com o default):

```csharp
public bool Entregar(ICategoriaPeriodoCache cache, DateTime? dataDevolucao = null)
```

Lógica:
1. `Clear()` e validação de status `Pendente` (como hoje).
2. Se `dataDevolucao` informada: validar `dataDevolucao.Value.Date > DateTime.Now.Date`; se inválida → `AddNotification` e `return false`.
3. `DataHoraEntrega = DateTime.Now`.
4. Para cada item:
   - **Informada:** `item.DataDevolucao = dataDevolucao.Value.Date.AddHours(23).AddMinutes(59).AddSeconds(59)` (mesma data para todos).
   - **Em branco:** `qtdeDias = cache.GetQuantidadeDias(item.IdPeriodo)`; `item.DataDevolucao = CalcularDataDevolucao(qtdeDias)` — que agora usa `DataHoraEntrega` como base.
5. Marca cópias como `Alugado`; `RegistrarAlteracao()`; `return true`.

`CalcularDataDevolucao` permanece igual (já usa `DataHoraEntrega ?? DataHora`).

### 5. Uso em `RenovarPedido` (bônus de consistência)

Hoje `RenovarPedido` carrega **todas** as categorias do banco (`_categoriaRepository.GetAllAsync`) só para achar um período por id, e `Pedido.Renovar` chama `Entregar()`. Ajustes:
- `RenovarPedido` passa a resolver o período pelo cache (elimina o hit no banco).
- A chamada interna `novoPedido.Entregar()` em `Pedido.Renovar` passa a receber o cache. Como `Renovar` é método de domínio, o cache também entra em `Renovar(...)` por parâmetro e é repassado ao `Entregar`.

### 5b. `ValidarAdicionarItem` passa a usar o cache

Hoje `PedidoUseCaseBasico.ValidarAdicionarItem` recebe `ICategoriaRepository` e faz `GetByIdAsync` apenas para localizar o período e validar que ele pertence à categoria do jogo. Como o `CategoriaPeriodoInfo` já carrega `IdCategoria`, o cache cobre isso sem tocar o banco. Ajustes:

- **Assinatura:** trocar o parâmetro `ICategoriaRepository categoriaRepository` por `ICategoriaPeriodoCache cache`. `IJogoRepository` permanece (ainda precisamos da `IdCategoria` do jogo, via `copia.Jogo` com o fallback existente para `jogoRepository.GetByIdAsync`).
- **Retorno:** passa a `(JogoCopia copia, CategoriaPeriodoInfo periodo)?` (antes era `CategoriaPeriodo`).
- **Validação equivalente:** obtém a `IdCategoria` do jogo; `cache.TryGetPeriodo(item.IdPeriodo)` → se não existir **ou** `info.IdCategoria != jogo.IdCategoria`, adiciona a notificação "A categoria deste jogo não permite o período informado." (mesma mensagem/semântica de hoje). Mantém também a notificação de categoria não encontrada quando não se consegue determinar a categoria do jogo.
- **Paridade:** o cache carrega ativos e inativos, e o `GetByIdAsync` atual também não filtra `Ativo` — comportamento preservado.
- **Chamadores:** `CadastroPedido` e `AtualizarPedido` deixam de injetar `ICategoriaRepository` (usado só aqui) e passam o cache. Trocar `resultValidacao.Value.periodo.Id` por `.IdPeriodo`; `.Valor` e `.QuantidadeDias` permanecem.

### 6. Use case — `AtualizarStatusPedido`

- Injeta `ICategoriaPeriodoCache` no construtor.
- Assinatura: `ExecuteAsync(int idPedido, StatusPedido novoStatus, DateTime? dataDevolucao = null)`.
- No ramo `Entregue`: `pedidoExistente.Entregar(cache, dataDevolucao)`. Demais ramos inalterados.

### 7. API — DTO e Controller

- `StatusPedidoDTO`: adicionar `public DateTime? DataDevolucao { get; set; }`.
- `PedidosController.AtualizarStatusPedido`: repassar `novoStatus.DataDevolucao` ao use case. O endpoint `DELETE` (cancelamento) segue chamando sem data.

### 8. Frontend (Next.js)

- **`api-service.ts`:** interface `StatusPedido` ganha `dataDevolucao?: string` (ISO `YYYY-MM-DD`).
- **`app/pedidos/page.tsx` — diálogo "Confirmar Entrega":**
  - Adicionar input de data **opcional** (default vazio), reusando o padrão de data já usado nessa página.
  - Texto auxiliar: "Se não informado, será calculado automaticamente a partir da data de entrega."
  - Validação client-side: se preenchido, precisa ser data futura (`> hoje`); caso contrário, exibe erro e não envia.
  - `handleConfirmarEntrega` envia `{ status: 1, dataDevolucao }` (omitindo `dataDevolucao` quando vazio).
- **Proxy `app/api/pedidos/[id]/status/route.ts`:** repassa o corpo como está — sem alteração.

### Serialização da data

`DataDevolucao` é `DateTime?`. O `System.Text.Json` desserializa string ISO (`"2026-07-20"`) para `DateTime` sem conflito com os conversores `DateOnly/TimeOnly` já registrados (que atuam sobre `DateOnly`/`TimeOnly`, não `DateTime`).

---

## Fluxo de dados (entrega)

```
Frontend (diálogo Entrega)
  └─ PUT /api/pedidos/{id}/status  { status: 1, dataDevolucao? }
       └─ proxy Next → API .NET
            └─ PedidosController.AtualizarStatusPedido
                 └─ AtualizarStatusPedido.ExecuteAsync(id, Entregue, dataDevolucao)
                      └─ Pedido.Entregar(cache, dataDevolucao)
                           ├─ informada  → todos os itens recebem a data (fim do dia)
                           └─ em branco  → cache.GetQuantidadeDias(item.IdPeriodo)
                                            → CalcularDataDevolucao (base = entrega)
```

## Tratamento de erros

- Status ≠ `Pendente` na entrega: notificação (como hoje).
- Data informada ≤ hoje: notificação `BadRequest`, entrega não ocorre.
- `IdPeriodo` ausente no cache: `QuantidadeDias = 1` + `LogWarning`; entrega prossegue.
- Falha ao salvar: exceção logada e propagada (como hoje).

## Testes

Testes de unidade no domínio (`Tests/`):
1. `Entregar` sem data, entrega N dias após o cadastro → `DataDevolucao` recalculada a partir da **data de entrega** + período (usa cache mockado).
2. `Entregar` com data válida → todos os itens recebem a mesma data (fim do dia).
3. `Entregar` com data no passado/hoje → retorna `false` e adiciona notificação; nada é alterado.
4. `Entregar` com `IdPeriodo` ausente no cache → usa `QuantidadeDias = 1` (cache mockado retornando default).
5. (Se viável) Teste do `CategoriaPeriodoCache`: `RefreshAsync` popula e `GetQuantidadeDias` retorna default+warning para id inexistente.
6. `ValidarAdicionarItem` com período de outra categoria / inexistente → notificação "A categoria deste jogo não permite o período informado" (cache mockado).

## Fora de escopo

- Alteração da data de devolução após a entrega (edição pós-entrega).
- Data de devolução por item na UI.
- Migração de schema (a abordagem por cache evita coluna nova em `PEDIDO_ITEM`).
