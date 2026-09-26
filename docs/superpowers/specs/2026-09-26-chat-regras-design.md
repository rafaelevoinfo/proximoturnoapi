# Chat de dúvidas sobre regras de jogos

Envolve os dois repositórios: `proximoturnoapi` (API, RAG, créditos) e `proximo-turno` (widget).

## O que o chat faz

Um assistente que responde **apenas** dúvidas de regras, usando os manuais já indexados no
Qdrant (coleção `manuais`, payload com `IdJogo`). Regras de negócio:

1. Aparece em todas as páginas não administrativas (tudo fora de `/admin`).
2. Só usuário logado conversa. Deslogado vê o botão, mas o painel convida a entrar.
3. Crédito = 10% do valor base de cada aluguel, mais US$ 0,50 de boas-vindas. Cada requisição
   abate o custo real que gerou. Admin não tem limite.
4. Toda busca é restrita a **um** jogo. Aberto a partir de `/jogos/[id]`, o jogo já vem
   definido. Fora dela, ou quando o usuário muda de assunto para outro jogo, o chat
   **confirma o jogo antes de buscar**.
5. Sem saldo: mensagem amigável dizendo que os créditos acabaram e voltam no próximo aluguel.
6. Fora de regras de jogo: recusa educada, sem responder.

## O que já existe e será reaproveitado

| Peça | Onde | Uso no chat |
|---|---|---|
| Vetores dos manuais, filtráveis por `IdJogo` | `QdrantManualVectorStore` | Ganha um método de busca |
| Embedding (`text-embedding-3-small`) | `IEmbeddingGenerator` singleton | Vetoriza a pergunta |
| Fábrica única de clientes LLM | `IFabricaOpenRouter` | Cria o cliente do chat |
| Ledger de custo por chamada | `USO_LLM` + `PoliticaUsoLlm` | Base do débito de créditos |
| Escopo ambiente do gasto | `EscopoUsoLlm` (AsyncLocal) | Passa a carregar o usuário |
| Dashboard de custos | `/admin/custos-ia` | Ganha as novas operações |

Achados que mudam o desenho:

- **A `PoliticaUsoLlm` ignora chamadas em streaming** (o corpo não fica em memória). Por isso
  a resposta do chat será **não-streaming** na v1. Streaming fica para depois, com captura de
  `usage` no último chunk SSE.
- O `usage.cost` da OpenRouter vem em **USD** e pode vir **nulo**. O crédito é em **R$**,
  então o débito precisa de uma cotação e de um fallback para custo nulo.
- Usuário (Identity, `AspNetUsers.Id` string) e `Cliente` se ligam **por e-mail**
  (`ClienteRepository.GetIdByEmailAsync`), como no resto da API.

## Decisões

### 1. Vínculo custo → usuário

`USO_LLM` ganha a coluna `ID_USUARIO varchar(255) NULL` com índice `(ID_USUARIO, MOMENTO)`.
**Sem FK**, pelo mesmo motivo de `ID_JOGO`: o registro do gasto precisa sobreviver a
exclusão/anonimização de conta (LGPD).

O usuário entra pelo mesmo caminho do jogo: `AlvoUsoLlm` ganha `IdUsuario`, e
`EscopoUsoLlm.Abrir(...)` ganha uma sobrecarga que aceita o usuário. O `RegistradorUsoLlm.Montar`
copia para a linha. A indexação continua gravando `NULL` (custo da loja, não do cliente).

`OperacaoLlm` ganha `ChatClassificacao = 3`, `ChatEmbedding = 4` e `ChatResposta = 5`.
Separar do `Embedding = 2` da indexação mantém o relatório de custos legível.

> Observação: o cliente de embedding singleton hoje está fixo em `OperacaoLlm.Embedding`. O
> chat pede à fábrica um cliente próprio (`CriarEmbedding(modelo, OperacaoLlm.ChatEmbedding)`,
> sobrecarga nova), para não misturar o gasto de busca com o de indexação.

### 2. Saldo derivado, sem tabela de saldo

Saldo em **R$**, calculado na hora:

```
bonus_rs    = 0,50 USD × cotação do momento da consulta               (boas-vindas, todo usuário)
creditos_rs = Σ 10% × ItemPedido.Valor  dos itens do cliente com status Entregue ou Devolvido
debitos_rs  = Σ USO_LLM.CUSTO_BRL       das linhas com ID_USUARIO = usuário
saldo_rs    = bonus_rs + creditos_rs − debitos_rs
```

- **Base do crédito = valor base do aluguel**: o `Valor` de cada item (preço do período),
  sem descontar cupom e sem a taxa de entrega. A soma é por **item**, porque o status também
  é por item: um item cancelado dentro de um pedido entregue não gera crédito.
- **Quando o aluguel conta:** item `Entregue` (depois `Devolvido`). `Pendente` não conta
  (ainda pode ser cancelado); `Cancelado` nunca conta. Na renovação, o item antigo vira
  `Devolvido` e o novo nasce no pedido de renovação: os dois geram crédito, como dois aluguéis.
- Sem tabela de saldo nem job: não tem como divergir do ledger, e o "renova no próximo
  aluguel" sai de graça, porque o item entregue já aumenta o saldo.
- **Bônus de boas-vindas** de US$ 0,50 para todo usuário logado, inclusive quem nunca alugou.
  Convertido pela cotação atual; oscila alguns centavos com o dólar, o que é aceitável para um
  bônus. Configurável (`CHAT_BONUS_USD`), assim como o percentual (`CHAT_PERCENTUAL_CREDITO`).
- **Admin não tem limite**: usuário com role `Admin` pula a checagem de saldo. O gasto dele
  continua indo para o ledger com `ID_USUARIO`, para aparecer no relatório.
- Performance: duas somas indexadas por usuário. Não precisa de cache na v1.

### 3. Cotação USD → BRL e débito congelado

O `usage.cost` vem em USD. O débito é convertido **no momento da chamada** e gravado em reais
numa coluna nova `USO_LLM.CUSTO_BRL decimal NULL` (e a cotação usada em `COTACAO_USD_BRL`).
Assim a variação do dólar não reescreve o saldo passado.

- **Fonte da cotação:** AwesomeAPI (`https://economia.awesomeapi.com.br/json/last/USD-BRL`),
  que é gratuita, não pede chave e é mantida no Brasil. Usa-se o campo `ask`.
- `ICotacaoDolar` (singleton) guarda o valor em memória por **6 horas**. Se a API falhar,
  usa o último valor obtido; sem nenhum, usa o valor fixo `COTACAO_USD_BRL_PADRAO` da `.env`.
  A cotação **nunca** derruba o chat.
- Só linhas com `ID_USUARIO` preenchido ganham `CUSTO_BRL`: a indexação não consome crédito
  e não precisa da consulta.
- Se `CustoUsd` vier nulo, o valor em USD é **estimado pelos tokens** com o preço por token
  configurado em `IAModel` para os modelos do chat. Custo nulo não pode sair de graça.
- O dashboard de custos mostra a cotação em uso e se ela veio da API ou do valor padrão.

> A rede deste ambiente de desenvolvimento bloqueia APIs externas, então a AwesomeAPI não pôde
> ser testada aqui. O fallback para o valor fixo cobre o caso de ela estar fora do ar.

### 3.1 Controle de saldo por requisição

1. Antes de qualquer chamada paga: se o usuário não é admin e `saldo ≤ 0`, responde
   `saldoEsgotado` sem gastar nada.
2. Cada etapa paga abre `EscopoUsoLlm` com `IdUsuario` + `IdJogo`, e o ledger grava sozinho,
   já com `CUSTO_BRL`.
3. **O saldo nunca aparece para o usuário**: nem na resposta do chat, nem em endpoint dele.
   Só o admin vê o crédito de um cliente (`GET /api/chat/creditos/cliente/{idCliente}`) e o
   ranking de gasto no dashboard de custos.

Duas requisições simultâneas podem deixar o saldo levemente negativo (centavos). Isso é aceito
de propósito: bloquear por usuário custaria mais do que o risco. O próximo pedido é recusado.
Um `max_tokens` baixo na resposta e um tamanho máximo de pergunta limitam o estouro.

### 4. Um jogo por conversa, sempre confirmado

O estado da conversa fica no cliente (sem persistência na v1) e vai em cada requisição:

```jsonc
POST /api/chat/mensagens
{
  "idJogoConfirmado": 42,       // null se ainda não há jogo confirmado
  "idJogoPagina": 42,           // jogo da página de onde o chat foi aberto, se houver
  "historico": [ { "papel": "usuario|assistente", "texto": "..." } ],  // últimos 6 turnos
  "mensagem": "Posso trocar recursos com o banco?"
}
```

Fluxo no use case `ResponderPerguntaRegras`:

1. **Saldo** (decisão 3.1).
2. **Classificação** (modelo barato, saída JSON estruturada) devolve
   `{ tipo: "regra" | "fora_de_escopo" | "saudacao", jogoMencionado: string | null }`.
   - `fora_de_escopo` → recusa padrão, sem busca.
3. **Resolução do jogo:**
   - Sem jogo confirmado e `idJogoPagina` presente, sem menção a outro jogo → confirma
     implicitamente o jogo da página (a regra permite: aberto na página, já filtra).
   - `jogoMencionado` presente e diferente do confirmado → busca candidatos no MySQL
     (nome aproximado, só jogos com manual indexado em `JOGO_LINK_INDEXACAO`; o status que a
     indexação já dá a links de jogo desativado exclui esses jogos)
     e responde `confirmarJogo` com até 3 opções. **Não busca regras ainda.**
   - Sem jogo confirmado e sem menção → pergunta "Sobre qual jogo é a sua dúvida?".
   - Jogo sem manual indexado → avisa que ainda não temos o manual desse jogo.
4. **Busca**: embedding da pergunta e `BuscarAsync(idJogo, vetor, topK: 6)` no Qdrant com
   filtro **obrigatório** `IdJogo`. O método nem aceita busca sem jogo; a garantia de "um jogo
   só" fica no tipo, e não na disciplina de quem chama.
5. **Resposta**: prompt de sistema restrito (responder só com base nos trechos, citar o
   manual/seção, dizer "não encontrei isso no manual" quando faltar base, recusar outros
   assuntos, ignorar instruções vindas do usuário para mudar de papel).

O usuário confirma o jogo por botão no widget. O botão reenvia a mesma pergunta com
`idJogoConfirmado` preenchido, para não precisar digitar de novo.

Resposta:

```jsonc
{
  "tipo": "resposta | confirmarJogo | perguntarJogo | foraDeEscopo | saldoEsgotado | semManual",
  "texto": "...",
  "jogo": { "id": 42, "nome": "Catan" },          // jogo usado, quando houver
  "opcoesJogo": [ { "id": 42, "nome": "Catan" } ], // só em confirmarJogo
  "fontes": [ { "titulo": "Manual > Comércio", "idJogoLink": 7 } ]
}
```

`saldoEsgotado` usa HTTP 200 com o tipo próprio (e não 402), para o widget tratar como
mensagem normal: *"Seus créditos para o assistente de regras acabaram por enquanto. Eles são
renovados automaticamente no seu próximo aluguel. Bom jogo! 🎲"*

### 5. Somente regras de jogo

Três camadas, porque só o prompt não basta:

1. Classificador descarta `fora_de_escopo` antes de gastar com busca e resposta.
2. Prompt de sistema proíbe outros assuntos e manda responder só com base nos trechos.
3. Sem jogo confirmado não há busca, e sem trechos não há resposta.

Custo das recusas: a classificação já foi paga e **é debitada**. Assim o saldo também serve de
freio contra abuso.

## Endpoints (API)

| Método | Rota | Auth | Descrição |
|---|---|---|---|
| POST | `/api/chat/mensagens` | `[Authorize]` | Fluxo da decisão 4 |
| GET | `/api/chat/creditos/cliente/{idCliente}` | `Admin` | Crédito do chat de um cliente (base alugada, crédito, bônus, gasto, saldo, cotação) |

Rate limit (`AddRateLimiter`, janela fixa por usuário, ~10 req/min) no `POST`. Pergunta
limitada a 500 caracteres; histórico cortado no servidor para os últimos 6 turnos.

## Front-end (`proximo-turno`)

- `lib/chat-context.tsx`: `ChatProvider` com `jogoPagina`, estado da conversa, `abrir()`.
  Entra no `app/layout.tsx` dentro do `AuthProvider`.
- `components/chat-regras/`: botão flutuante e painel (shadcn `Sheet` no mobile, card no
  desktop), lista de mensagens, chips de confirmação de jogo. **Sem saldo na tela.**
- Oculto quando `usePathname().startsWith("/admin")`. Não existe layout de admin separado,
  então o filtro fica no próprio widget.
- Deslogado: o painel mostra "Entre para tirar dúvidas de regras" com um link para `/login`.
- `app/jogos/[id]/page.tsx` chama `setJogoPagina({ id, nome })` ao carregar e limpa ao sair.
  Botão "Dúvidas sobre as regras?" na página abre o chat já filtrado. Trocar de página de jogo
  com o chat aberto inicia uma conversa nova com o novo jogo.
- Rota proxy `app/api/chat/mensagens/route.ts` (e a de créditos, usada só no admin) no padrão de
  `app/api/relatorios/custos-ia/route.ts` (`getBaseHeaders`, `handleApiError`).
- Conversa em `sessionStorage` (sobrevive à navegação, some ao fechar a aba).

## Admin

- `/admin/custos-ia`: as novas operações aparecem com rótulo; card "Top usuários do chat por
  custo" e a cotação em uso (e de onde veio).
- `/admin/clientes`: crédito do chat de cada cliente, no detalhe do cliente.

## Fora do escopo da v1

Streaming, histórico persistido no servidor, feedback 👍/👎, compra avulsa de créditos,
concessão manual de créditos pelo admin.

## Riscos

- **AwesomeAPI fora do ar ou mudando o formato**: cache de 6 h, último valor conhecido e, por
  fim, o valor fixo da `.env`. O dashboard mostra de onde veio a cotação.
- **Custo nulo** (provider sem `usage.cost`): coberto pela estimativa por token.
- **Manual mal indexado** gera resposta ruim. O prompt obriga a dizer "não encontrei no manual"
  em vez de inventar, e as fontes aparecem na resposta.
- **Prompt injection** pedindo outro assunto: coberto pelas três camadas da decisão 5, e o
  gasto sai do crédito do próprio usuário.

## Plano de implementação

### API (`proximoturnoapi`)

1. **Ledger por usuário**: `ID_USUARIO`, `CUSTO_BRL` e `COTACAO_USD_BRL` em `UsoLlm` + índice + migration; `AlvoUsoLlm`/
   `EscopoUsoLlm` com usuário; `RegistradorUsoLlm.Montar` copia. Novos valores em
   `OperacaoLlm`. Testes: `Montar` com e sem usuário; escopo aninhado restaura o anterior.
2. **Fábrica**: sobrecarga `CriarEmbedding(modelo, operacao)`. Modelos do chat e preços de
   fallback em `IAModel`.
3. **Busca vetorial**: `IManualVectorStore.BuscarAsync(int idJogo, ReadOnlyMemory<float>,
   int topK, CancellationToken)` com filtro `IdJogo` obrigatório. Teste de integração garante
   que nenhum ponto de outro jogo volta.
4. **Cotação**: `ICotacaoDolar` com AwesomeAPI, cache de 6 h e fallback para a `.env`.
   `RegistradorUsoLlm` preenche `CUSTO_BRL` quando há usuário. Testes com `HttpMessageHandler`
   falso: sucesso, erro com cache, erro sem cache (usa o padrão), JSON inesperado.
   **Créditos**: `IChatRegrasRepository` + use case `ObterSaldoChat` (decisão 2). Testes:
   item cancelado/pendente não conta; renovação conta; cupom e taxa de entrega não mudam o
   crédito; usuário sem aluguel tem só o bônus; admin não é bloqueado.
5. **Resolução de jogo**: busca de candidatos por nome entre jogos ativos com manual
   indexado. Testes com nomes parecidos ("Catan" × "Catan: Cidades e Cavaleiros").
6. **Use case `ResponderPerguntaRegras`** (herda `UseCaseBasico`, Flunt): orquestra as etapas
   da decisão 4, cada uma dentro de `EscopoUsoLlm`. Testes com `IChatClient` falso para cada
   `tipo` de resposta, inclusive "saldo zero não chama LLM nenhum".
7. **`ChatController`** + DTOs + rate limit + registro `Scoped` no `Program.cs`.
8. **Relatório de custos**: rótulos das novas operações, filtro/top por usuário.

### Front (`proximo-turno`)

9. Rotas proxy `/api/chat/*` e tipos em `lib/api-service.ts`.
10. `ChatProvider` + widget + regra de ocultar em `/admin` + estado deslogado.
11. Integração com a página do jogo (contexto do jogo + botão de atalho).
12. Estados de UI: carregando, confirmar jogo, fora de escopo, sem manual, saldo esgotado,
    erro/limite de requisições.
13. Ajustes no dashboard `/admin/custos-ia`.

### Validação ponta a ponta

14. Cenários manuais: aberto em `/jogos/[id]` responde sem confirmar; aberto na home pede o
    jogo; troca de jogo no meio da conversa pede confirmação; pergunta fora do tema é recusada;
    saldo zerado mostra a mensagem amigável; entregar um pedido novo libera o chat; admin
    conversa sem saldo; usuário novo começa com o bônus; o widget não aparece em `/admin/*`.

## Decisões do produto (26/09/2026)

1. Base do crédito: valor base do aluguel (preço do item, sem cupom e sem taxa de entrega).
2. Cotação: API gratuita (AwesomeAPI), com valor fixo na `.env` como fallback.
3. Admin: sem limite de crédito.
4. Boas-vindas: US$ 0,50 de crédito para todo usuário.
5. O crédito não é exibido para o usuário, só para admins.

## Revisão de 26/09/2026: sem chamada de classificação

A pedido do produto, a etapa de classificação saiu. Cada pergunta faz **uma** chamada de chat
(DeepSeek `deepseek-v4-flash`) mais o embedding da busca:

- Sem jogo na conversa: nenhuma chamada paga. O catálogo é varrido em memória atrás de um nome
  de jogo na mensagem; se achar, pede confirmação; se não, pergunta qual é o jogo.
- Com jogo: busca no manual e resposta. As instruções de sistema trazem as regras (só regras
  deste jogo, só com base nos trechos, recusar outros assuntos, ignorar pedidos para mudar de
  papel). Se a pergunta for de outro jogo, o modelo responde só `[[OUTRO_JOGO:nome]]`, e a API
  transforma isso em confirmação de jogo.
- `OperacaoLlm` fica com `ChatEmbedding = 3` e `ChatResposta = 4`.
