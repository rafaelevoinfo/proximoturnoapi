# Especificação de Design: Validação de Acesso a Contratos (Proprietário do Pedido)

## Histórico de Revisões
- **Data:** 18/06/2026
- **Status:** Em Revisão
- **Autor:** Antigravity (AI Coding Assistant)

---

## 1. Visão Geral
Atualmente, qualquer usuário autenticado pode gerar ou consultar contratos de qualquer pedido informando o `idPedido` nas rotas do backend.
Este design especifica as alterações necessárias para garantir que:
1. Um usuário não-admin somente possa consultar ou gerar contratos dos seus próprios pedidos.
2. A página de visualização de contrato no frontend (`/contrato/[idPedido]`) trate a falta de permissão graciosamente, exibindo uma mensagem de "Acesso Negado" e permitindo o retorno à listagem de pedidos, sem deslogar o usuário.

---

## 2. Detalhes das Implementações no Backend

### 2.1. Alterações em `GerarContratoPedido.cs` e `ConsultarContratoPedido.cs`
*   Injetar `UserManager<Usuario>` e `IClienteRepository` para validação de e-mail e id do cliente.
*   Atualizar o método `ExecuteAsync` para aceitar `ClaimsPrincipal userClaim` como primeiro argumento.
*   Se o usuário não for do papel `Roles.Admin`:
    *   Buscar o usuário no `UserManager` e obter seu e-mail.
    *   Obter o `idCliente` vinculado a esse e-mail no `IClienteRepository`.
    *   Verificar se o `idCliente` do usuário coincide com o `Cliente.Id` do pedido associado ao contrato.
    *   Se não coincidir, adicionar uma notificação de `UseCaseNotificationType.Forbid` e retornar `null`.

#### Exemplo em `ConsultarContratoPedido.cs`:
```csharp
public async Task<ContratoAutentique?> ExecuteAsync(ClaimsPrincipal userClaim, int idPedido) {
    var contrato = await contratoRepository.GetByPedidoIdAsync(idPedido);
    if (contrato is null) {
        AddNotification(UseCaseNotification.Create(UseCaseNotificationType.NotFound, "Nenhum contrato encontrado para este pedido."));
        return null;
    }

    // Validação de proprietário do pedido
    if (!userClaim.IsInRole(Roles.Admin)) {
        var user = await userManager.GetUserAsync(userClaim);
        var idCliente = await clienteRepository.GetIdByEmailAsync(user?.Email ?? "");
        
        // Carrega o pedido se necessário para pegar o Cliente
        var pedido = contrato.Pedido ?? await pedidoRepository.GetByIdAsync(idPedido);
        if (pedido == null || idCliente is null || idCliente.Value != pedido.Cliente?.Id) {
            logger.LogWarning("Acesso negado: Usuário {UserEmail} tentou acessar o contrato do pedido {PedidoId}.", user?.Email, idPedido);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Forbid, "Acesso negado: você não tem permissão para visualizar o contrato deste pedido."));
            return null;
        }
    }
    
    // ... lógica existente ...
}
```

### 2.2. Alterações em `ContratosController.cs`
*   Atualizar as chamadas de `_gerarContratoUseCase.ExecuteAsync(idPedido)` e `_consultarContratoUseCase.ExecuteAsync(idPedido)` para passarem a propriedade `User` como primeiro parâmetro.
*   Tratar o status de notificação `UseCaseNotificationType.Forbid` retornando `Forbid()`.

---

## 3. Detalhes das Implementações no Frontend

### 3.1. Rota Proxy no Next.js (`app/api/contratos/pedido/[idPedido]/route.ts`)
*   Se o backend retornar status `403` (Forbidden), interceptar esse status e retornar um status `400 Bad Request` com o corpo `{ error: "Acesso negado: você não tem permissão para visualizar o contrato deste pedido." }`.
*   Isso impede que o middleware global do cliente (`api-service.ts`) intercepte o `403` e execute o logout automático do usuário, permitindo o tratamento direto na página.

### 3.2. Serviço de API (`lib/api-service.ts`)
*   No método `consultarContratoPedido(idPedido)`:
    *   Verificar se o erro lançado no `try-catch` possui a mensagem `"Acesso negado"`.
    *   Caso seja um erro de acesso negado, relançar (`throw error`), permitindo que a página capture a exceção e aja devidamente.

### 3.3. Página do Contrato (`app/contrato/[idPedido]/page.tsx`)
*   Adicionar estado local `forbidden` (iniciado como `false`).
*   No `catch` da função `pollContrato`, verificar se a mensagem do erro contém `"Acesso negado"`. Se sim, definir `forbidden = true`, interromper o *polling* e parar o *loading*.
*   Exibir uma tela amigável de **Acesso Negado** com um botão para voltar para os pedidos do cliente, sem deslogá-lo.

---

## 4. Plano de Testes
1. **Acesso por Admin**: Um usuário administrador deve conseguir acessar o contrato de qualquer pedido (seu ou de terceiros).
2. **Acesso por Proprietário**: Um usuário cliente comum deve conseguir acessar o contrato de seus próprios pedidos normalmente.
3. **Acesso por Não-Proprietário**: Um usuário cliente comum tentando acessar a página de contrato ou a API correspondente de um pedido que pertence a outro cliente deve receber a tela de "Acesso Negado", mantendo-se devidamente logado no sistema.
