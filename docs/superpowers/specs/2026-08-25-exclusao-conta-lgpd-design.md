# Exclusão de conta do cliente (LGPD) — Design

**Data:** 2026-08-25
**Status:** aprovado para planejamento

## Objetivo

Permitir que o titular exerça o direito de eliminação previsto no Art. 18, VI da LGPD:
uma opção de "excluir minha conta" no perfil que inativa o cliente, anonimiza seus dados
pessoais, apaga os registros pessoais vinculados e remove o login — preservando o que a
lei permite (e obriga) reter.

Não é possível excluir a linha do `CLIENTE`: `PEDIDO` aponta para ela por chave
estrangeira e o histórico de pedidos precisa sobreviver por obrigação fiscal e contábil
(Art. 16, I). A solução é anonimizar o registro no lugar, tornando-o não reidentificável.

## Decisões tomadas

Registradas com o motivo, porque várias divergem da abordagem óbvia.

### 1. Cliente com pedido em aberto não consegue excluir a conta

Se houver qualquer pedido em `Pendente` ou `Entregue`, a exclusão é **recusada**, com uma
mensagem que lista os pedidos e os jogos em aberto.

O motivo é que `Pedido` referencia `Cliente` por FK e **não guarda cópia dos dados**.
Anonimizar o cliente mantendo o contrato do pedido pendente produziria um contrato sem
locatário identificável — inútil justamente na situação que motivaria guardá-lo (ação
judicial por não devolução).

Base legal para a recusa: Art. 16, II (conservação para cumprimento de obrigação legal) e
Art. 7º, VI (exercício regular de direitos em processo). A recusa deve ser informada ao
titular com a razão e o caminho para resolver — devolver os jogos.

Consequência que simplifica todo o resto: **quando a exclusão é permitida, todos os
contratos do cliente podem ser apagados**, sem caso especial.

### 2. `DATA_ANONIMIZACAO` separada de `ATIVO`

Coluna nova `CLIENTE.DATA_ANONIMIZACAO` (`DateTime?`, null = nunca excluído).

Hoje `Ativo = false` significa uma coisa só: *o admin bloqueou este cliente* (inadimplência
etc.). Reusar a mesma flag para *esta pessoa não existe mais* juntaria dois casos que pedem
tratamento diferente — o inadimplente bloqueado continua com dados reais visíveis ao admin
e não há motivo para a avaliação dele sair do catálogo.

A data (e não um booleano) porque o Art. 37 espera registro das operações de tratamento:
é preciso poder demonstrar **quando** o direito foi atendido.

**Invariante:** `DataAnonimizacao != null ⇒ Ativo == false`. A recíproca não vale.

### 3. Comentários são apagados de vez

Hard delete dos comentários do cliente, não anonimização do autor.

Anonimizar o `Cliente` já faria o comentário aparecer assinado como "cliente removido" de
graça, mas isso **não limpa o campo `Texto`**, que é livre (1000 caracteres) e pode conter
dado pessoal escrito pela própria pessoa. Apagar é a única opção que remove de verdade.

Custo aceito: a nota dele sai da média do jogo.

### 4. O `IdentityUser` é deletado e o e-mail volta a ficar livre

`UserManager.DeleteAsync` remove a linha de `AspNetUsers` e, em cascata, as roles em
`AspNetUserRoles`. Senha, tokens e logins externos vão junto.

O e-mail real deixa de existir no sistema e pode ser reutilizado num cadastro novo. Quem
voltar entra como cliente novo, com histórico zerado — os pedidos antigos permanecem no
`CLIENTE` anonimizado e não são reassociados.

A alternativa de manter o e-mail com lockout eterno foi descartada: guardaria exatamente o
dado pessoal que se prometeu excluir, e impediria a pessoa de voltar a ser cliente.

### 5. O documento no Autentique NÃO é excluído — risco residual assumido

Apenas a linha local de `CONTRATO_AUTENTIQUE` é apagada. O `AutentiqueService` não ganha a
mutation `deleteDocument`.

**Risco registrado, decisão consciente do responsável pelo produto:** o PDF assinado
permanece no Autentique contendo nome, CPF, endereço e assinatura, e o painel deles é
pesquisável por e-mail do signatário. "Desvinculado do meu sistema" não equivale a
"excluído" — como controlador, a responsabilidade sobre esse dado permanece com o Próximo
Turno, sendo o Autentique operador.

O design não impede a evolução: fechar essa lacuna depois é acrescentar a mutation ao
`AutentiqueService` e um job de expurgo, sem alterar nada do que está aqui.

### 6. Contas com role Admin não podem ser excluídas

Reusa e endurece a regra que já existe em `AtualizarStatusCliente.cs:21`.

No caso do lockout o estrago é reversível. Aqui não: deletar o `IdentityUser` leva junto o
vínculo de role, e se era a última conta Admin não resta ninguém capaz de conceder Admin a
alguém — a recuperação vira inserir usuário, hash de senha e role na mão no MySQL. Com a
anonimização por cima, nem o e-mail original sobra para orientar.

Isto é uma **guarda técnica, não uma isenção de LGPD**. Admin no Próximo Turno é staff, e
o tratamento se apoia na relação de trabalho, não em consentimento. O caminho para um admin
que queira sair existe e tem duas etapas, e a mensagem de recusa precisa dizê-lo:

> "Contas com perfil de administrador não podem ser excluídas por aqui. Peça a outro
> administrador para remover seu perfil de administrador e repita a exclusão."

Variante descartada: bloquear apenas o *último* admin. A contagem é fácil, mas dois admins
excluindo em paralelo passam ambos pela verificação e zeram o sistema; blindar isso exigiria
lock de tabela por um caso que quase nunca ocorre.

### 7. As rotas públicas filtram por anonimizado, não por `Ativo`

Filtrar por `Ativo` faria o cliente bloqueado por inadimplência sumir do catálogo junto com
o anonimizado, e não há razão para a avaliação dele sair do ar por ele estar devendo.

## Arquitetura

### Schema

Uma migration, uma coluna:

```
CLIENTE.DATA_ANONIMIZACAO  datetime NULL
```

### Use case novo

`Application/UseCases/Cliente/ExcluirContaCliente.cs`, herdando de `UseCaseBasico`, no
padrão Flunt/notification do resto do projeto.

Recebe `DatabaseContext`? **Não.** Fala apenas com repositórios e `UserManager`, para poder
ser testado com os fakes que já existem em `Tests/Fakes/PedidoUseCaseFakes.cs`.

Dependências: `IClienteRepository`, `IPedidoRepository`, `IContratoRepository`,
`UserManager<Usuario>`, `IEmailService`, `ILogger`.

Registro como `Scoped` em `Program.cs`, como os demais.

### Métodos novos de repositório

Cada um na sua fronteira natural:

| Repositório | Método | Uso |
|---|---|---|
| `IPedidoRepository` | `ObterPedidosEmAbertoAsync(int idCliente)` | Pré-condição 4 e mensagem de recusa. Um método só: lista vazia responde "pode excluir", e quando não está vazia já traz o que a mensagem precisa |
| `IClienteRepository` | `ExcluirDadosVinculadosAsync(int idCliente)` | Comentários + lista de desejos |
| `IContratoRepository` | `ExcluirPorClienteAsync(int idCliente)` | Contratos dos pedidos do cliente |

`IEmailService.SendEmailAsync(toEmail, subject, body, isHtml)` já existe e atende o e-mail de
confirmação sem mudança.

### Transação

`DatabaseContext` herda de `IdentityDbContext<Usuario>` e o Identity está configurado com
`AddEntityFrameworkStores<DatabaseContext>()`. Logo **a operação inteira, incluindo o delete
do `IdentityUser`, cabe numa única transação** — não existe estado de conta meio-excluída.

Sutileza que merece comentário no código: `BaseRepository` guarda `_currentTransaction` por
instância, mas todos os repositórios compartilham o mesmo `DatabaseContext` *scoped*. Um
`StartTransactionAsync()` em qualquer repositório vale para os outros, e as chamadas
`ExecuteDeleteAsync` dos demais entram nessa mesma transação.

## Fluxo da exclusão

### Pré-condições, nesta ordem

1. Cliente existe. Se não → `404`.
2. Já anonimizado → **sucesso idempotente**, não erro.
3. Senha confere (só no caminho do cliente; ver Autorização). Se não → `400`.
4. Nenhum pedido em `Pendente` ou `Entregue` → senão `409` com a lista.
5. O `IdentityUser` do cliente não tem role Admin → senão `400` com a mensagem de duas etapas.

### Dentro da transação

| Alvo | Ação |
|---|---|
| `CLIENTE.NOME` | `"cliente removido"` |
| `CLIENTE.EMAIL` | `anon-{id}@removido.local` |
| `CLIENTE.TELEFONE` | `anon{id}` |
| `CLIENTE.ENDERECO` | `"removido"` |
| `CLIENTE.CPF` | `null` |
| `CLIENTE.DATA_NASCIMENTO` | `null` |
| `CLIENTE.COMO_NOS_CONHECEU` | `null` |
| `CLIENTE.ACEITA_RECEBER_OFERTAS` | `false` |
| `CLIENTE.ATIVO` | `false` |
| `CLIENTE.DATA_ANONIMIZACAO` | `DateTime.Now` |
| `COMENTARIO` do cliente | DELETE |
| `LISTA_DESEJOS` do cliente | DELETE |
| `CONTRATO_AUTENTIQUE` dos pedidos dele | DELETE (linha local; ver decisão 5) |
| `AspNetUsers` | `UserManager.DeleteAsync` |

**Tokens únicos são obrigatórios**, não estética: `EMAIL`, `TELEFONE` e `CPF` são índices
UNIQUE (`DatabaseContext.cs:24,25,28`). Escrever o mesmo valor em duas linhas anonimizadas
viola a constraint na segunda exclusão. `CPF` é nullable e o MySQL aceita NULL repetido em
índice único, então ele pode ir a null; `TELEFONE` e `EMAIL` são `required` e precisam do
`{id}` embutido.

Limites conferem: `TELEFONE` tem `MaxLength(15)` e `anon` + id cabe; `EMAIL` tem
`MaxLength(100)` contra 20 caracteres fixos + id.

Nota: o setter de `Cliente.Nome` e de `Cliente.Email` aplica `ToLowerInvariant()`, então os
valores acima devem ser escritos já em minúsculo para evitar surpresa em comparações.

### Preservado

`PEDIDO` e `ITEM_PEDIDO`, apontando para o `CLIENTE` já anonimizado. Depois da anonimização
carregam apenas valores, datas e jogos. Necessários para obrigação fiscal (Art. 16, I) e
para o relatório de faturamento não ficar com buraco.

### Depois do commit

E-mail de confirmação ("sua conta foi excluída") para o endereço real, capturado em memória
antes da anonimização.

Deliberadamente **fora da transação**: se o SMTP falhar, a exclusão já está feita, que é a
prioridade correta. A falha vira log, nunca rollback.

## Superfície de API

### Endpoint novo

```
DELETE /api/clientes/{id}/conta      [Authorize]
```

Autorização dentro do controller, no mesmo padrão de `GetPerfilCliente`: o chamador precisa
ser o dono da conta **ou** ter role Admin.

| Situação | Resposta |
|---|---|
| Sucesso | `200` |
| Pedidos em aberto | `409 Conflict`, payload com os pedidos e jogos |
| Alvo é Admin | `400`, mensagem de duas etapas |
| Senha incorreta | `400` |
| Nem dono nem admin | `403` |
| Cliente inexistente | `404` |

O `409` é um desvio pequeno do padrão do projeto (que só usa `BadRequest`/`NotFound`), e é
intencional: o frontend precisa distinguir "não pode" genérico de "devolva estes jogos" para
montar uma tela diferente.

### Confirmação por senha

O cliente envia a senha atual no corpo; o use case valida com
`UserManager.CheckPasswordAsync` antes de qualquer efeito. Sem isso, uma sessão sequestrada
apaga a conta de forma irreversível.

O caminho do admin **não** pede senha — a autenticação de admin já é o controle.

### Endpoint existente preservado

`DELETE /api/clientes/{id}` (admin, inativa) **fica como está**. É o bloqueio comercial,
conceito distinto — separar os dois estados (decisão 2) só faz sentido se ambos os caminhos
continuarem existindo.

## Varredura de rotas

| Ponto | Situação hoje | Ação |
|---|---|---|
| `EnviarEmailsClientes` → `GetAllByIdsAsync` | Sem filtro nenhum. Disparo para lista salva tentaria enviar para `anon-123@removido.local` | **Filtrar anonimizados.** Único caso com dano concreto |
| `ObterComentariosJogo` (público) | `Include(Cliente)` sem filtro | `&& c.Cliente.DataAnonimizacao == null` — cinto de segurança; as linhas já foram apagadas |
| `GetPerfilCliente` / `PutCliente` | Alcançáveis por admin após a exclusão | Recusar se anonimizado |
| `ClienteRepository.GetAllAsync` (grid admin) | — | Continua listando; `ClienteDTO.DataAnonimizacao` (`DateTime?`) novo, ao lado de `Ativo` e `LoginAtivo`, para a UI marcar "conta excluída em {data}". O admin precisa da linha para o histórico de pedidos fechar, e a data é o registro exigido pelo Art. 37 |
| `ObterComentariosFiltrados` / `ObterComentarioPorId` (admin) | — | Sem mudança. Admin ver tudo é correto |
| `ObterRelatorioFaturamento` (top clientes) | Passará a exibir "cliente removido" | Sem mudança — é o resultado desejado |
| `PodeComentarJogo`, `SalvarComentario`, `ResetPassword` | Inalcançáveis sem login, que deixou de existir | Sem mudança |
| `CadastroCliente` | — | Sem mudança. Com e-mail/telefone/CPF liberados, não há colisão no recadastro |

## Frontend

- **`app/perfil/page.tsx`**: seção "Zona de risco" ao final do card, com botão "Excluir
  minha conta".
- **Dialog de confirmação**, duas barreiras: texto explicando o que some (comentários, lista
  de desejos, login) e o que permanece (histórico de pedidos), mais campo de senha.
- **Resposta `409`**: o dialog dá lugar à lista de pedidos em aberto, com link para cada um.
- **Sucesso**: logout, redirect para a home, toast de confirmação.
- **Proxy novo**: `app/api/clientes/[id]/conta/route.ts` (DELETE), seguindo os proxies
  existentes.
- **`lib/api-service.ts`**: método `excluirConta`.
- **Admin**: mesma ação na tela de detalhe do cliente, sem campo de senha.

## Testes

xUnit com os fakes existentes (`FakeClienteRepository`, `FakePedidoRepository`,
`FakeContratoRepository`, `FakeUserManager`). Sem banco.

**`ExcluirContaClienteTests`:**

- recusa com pedido em `Pendente`
- recusa com pedido em `Entregue`
- permite quando só há `Devolvido` / `Cancelado`
- permite quando o cliente não tem pedido nenhum
- recusa quando o alvo tem role Admin
- idempotente quando já anonimizado
- recusa com senha incorreta
- não pede senha no caminho do admin
- anonimiza todos os campos listados
- gera tokens únicos de e-mail e telefone para dois clientes diferentes
- apaga comentários, lista de desejos e contratos
- deleta o `IdentityUser`
- rollback quando uma etapa falha
- falha no envio do e-mail não desfaz a exclusão

**Filtros:**

- `ObterComentariosJogo` ignora comentário de cliente anonimizado
- `EnviarEmailsClientes` pula cliente anonimizado — estende `EnviarEmailsClientesTests`, que
  já existe

**Frontend**: verificação manual, como o projeto já faz.

## Fora de escopo

- Expurgo do documento no Autentique (decisão 5, risco residual registrado)
- Política de retenção com prazo para os pedidos preservados
- Exportação de dados do titular (Art. 18, V) — direito diferente, feature diferente
- Aviso/consentimento na tela de cadastro e política de privacidade
