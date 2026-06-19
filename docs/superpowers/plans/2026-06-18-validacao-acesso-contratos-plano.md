# Validação de Acesso a Contratos (Proprietário do Pedido) - Plano de Implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Garantir que apenas usuários admin ou proprietários do pedido possam consultar e gerar seus contratos, tratando o erro graciosamente no frontend sem forçar o logout do usuário.

**Architecture:** Mapear a autenticação de propriedade nos Use Cases do backend .NET Core (retornando `Forbid` em caso de falha), converter o status `403` da resposta do backend para `400` na API proxy do Next.js (evitando o logout automático), e tratar o erro na página `/contrato/[idPedido]` no React.

**Tech Stack:** .NET 10.0, Next.js 16 (App Router), React 19

---

### Task 1: Backend Use Case `ConsultarContratoPedido`

**Files:**
- Modify: `Src/Application/UseCases/Contrato/ConsultarContratoPedido.cs`

- [ ] **Step 1: Injetar dependências no Use Case**
  Adicionar os parâmetros `UserManager<Usuario> userManager`, `IClienteRepository clienteRepository` e `IPedidoRepository pedidoRepository` ao construtor primário de `ConsultarContratoPedido`.
  *Código a ser inserido:*
  ```csharp
  public class ConsultarContratoPedido(
      IContratoRepository contratoRepository,
      IAutentiqueService autentiqueService,
      UserManager<Usuario> userManager,
      IClienteRepository clienteRepository,
      IPedidoRepository pedidoRepository,
      ILogger<ConsultarContratoPedido> logger) : UseCaseBasico
  ```
  Adicionar os namespaces necessários no topo do arquivo se não existirem:
  ```csharp
  using System.Security.Claims;
  using Microsoft.AspNetCore.Identity;
  ```

- [ ] **Step 2: Adicionar validação de propriedade ao `ExecuteAsync`**
  Alterar a assinatura de `ExecuteAsync` para aceitar `ClaimsPrincipal userClaim`. Adicionar a validação de propriedade do pedido caso o usuário não seja Admin.
  *Código de modificação:*
  ```csharp
  public async Task<ContratoAutentique?> ExecuteAsync(ClaimsPrincipal userClaim, int idPedido) {
      var contrato = await contratoRepository.GetByPedidoIdAsync(idPedido);
      if (contrato is null) {
          AddNotification(UseCaseNotification.Create(
              UseCaseNotificationType.NotFound,
              "Nenhum contrato encontrado para este pedido."));
          return null;
      }

      if (!userClaim.IsInRole(Roles.Admin)) {
          var user = await userManager.GetUserAsync(userClaim);
          var idCliente = await clienteRepository.GetIdByEmailAsync(user?.Email ?? "");
          
          var pedido = contrato.Pedido ?? await pedidoRepository.GetByIdAsync(idPedido);
          if (pedido == null || idCliente is null || idCliente.Value != pedido.Cliente?.Id) {
              logger.LogWarning("Acesso negado: Usuário {UserEmail} tentou acessar o contrato do pedido {PedidoId}.", user?.Email, idPedido);
              AddNotification(UseCaseNotification.Create(
                  UseCaseNotificationType.Forbid,
                  "Acesso negado: você não tem permissão para visualizar o contrato deste pedido."));
              return null;
          }
      }

      // Se já está em estado final, retorna direto
      if (contrato.Status != StatusContrato.Pendente) {
          return contrato;
      }
      // ... restante do método existente ...
  ```

- [ ] **Step 3: Compilar o backend**
  Verificar se a compilação do backend funciona sem erros.
  Run: `dotnet build Src/ProximoTurnoApi.csproj`
  Expected: Build sucedida.

---

### Task 2: Backend Use Case `GerarContratoPedido`

**Files:**
- Modify: `Src/Application/UseCases/Contrato/GerarContratoPedido.cs`

- [ ] **Step 1: Injetar dependências no Use Case**
  Adicionar `UserManager<Usuario> userManager` e `IClienteRepository clienteRepository` ao construtor primário de `GerarContratoPedido`.
  *Código a ser inserido:*
  ```csharp
  public class GerarContratoPedido(
      IPedidoRepository pedidoRepository,
      IContratoRepository contratoRepository,
      IContratoPdfService contratoPdfService,
      IAutentiqueService autentiqueService,
      IConfiguration configuration,
      UserManager<Usuario> userManager,
      IClienteRepository clienteRepository,
      ILogger<GerarContratoPedido> logger) : UseCaseBasico
  ```
  Adicionar namespaces:
  ```csharp
  using System.Security.Claims;
  using Microsoft.AspNetCore.Identity;
  ```

- [ ] **Step 2: Adicionar validação de propriedade ao `ExecuteAsync`**
  Alterar a assinatura de `ExecuteAsync` para aceitar `ClaimsPrincipal userClaim`. Adicionar a validação de propriedade do pedido caso o usuário não seja Admin.
  *Código de modificação:*
  ```csharp
  public async Task<ContratoAutentique?> ExecuteAsync(ClaimsPrincipal userClaim, int idPedido) {
      // 1. Verificar se já existe contrato para este pedido
      var contratoExistente = await contratoRepository.GetByPedidoIdAsync(idPedido);
      if (contratoExistente is not null) {
          // Validação de proprietário mesmo que já exista contrato
          if (!userClaim.IsInRole(Roles.Admin)) {
              var user = await userManager.GetUserAsync(userClaim);
              var idCliente = await clienteRepository.GetIdByEmailAsync(user?.Email ?? "");
              
              var pedidoExistente = contratoExistente.Pedido ?? await pedidoRepository.GetByIdAsync(idPedido);
              if (pedidoExistente == null || idCliente is null || idCliente.Value != pedidoExistente.Cliente?.Id) {
                  logger.LogWarning("Acesso negado: Usuário {UserEmail} tentou gerar contrato para o pedido {PedidoId}.", user?.Email, idPedido);
                  AddNotification(UseCaseNotification.Create(
                      UseCaseNotificationType.Forbid,
                      "Acesso negado: você não tem permissão para visualizar o contrato deste pedido."));
                  return null;
              }
          }
          logger.LogWarning("Tentativa de gerar contrato para o pedido {IdPedido}, mas já existe um contrato com ID {IdContrato}", idPedido, contratoExistente.Id);
          AddNotification(UseCaseNotification.Create(
              UseCaseNotificationType.BadRequest,
              "Já existe um contrato gerado para este pedido."));
          return contratoExistente;
      }

      // 2. Buscar o pedido completo
      var pedido = await pedidoRepository.GetByIdAsync(idPedido);
      if (pedido is null) {
          logger.LogWarning("Pedido com ID {IdPedido} não encontrado para geração de contrato.", idPedido);
          AddNotification(UseCaseNotification.Create(
              UseCaseNotificationType.NotFound,
              "Pedido não encontrado."));
          return null;
      }

      // Validação de proprietário do pedido
      if (!userClaim.IsInRole(Roles.Admin)) {
          var user = await userManager.GetUserAsync(userClaim);
          var idCliente = await clienteRepository.GetIdByEmailAsync(user?.Email ?? "");
          if (idCliente is null || idCliente.Value != pedido.Cliente?.Id) {
              logger.LogWarning("Acesso negado: Usuário {UserEmail} tentou gerar contrato para o pedido {PedidoId}.", user?.Email, idPedido);
              AddNotification(UseCaseNotification.Create(
                  UseCaseNotificationType.Forbid,
                  "Acesso negado: você não tem permissão para visualizar o contrato deste pedido."));
              return null;
          }
      }
      // ... restante do método existente ...
  ```

- [ ] **Step 3: Compilar o backend**
  Verificar se a compilação do backend funciona.
  Run: `dotnet build Src/ProximoTurnoApi.csproj`
  Expected: Build sucedida.

---

### Task 3: Background Worker do Contrato

**Files:**
- Modify: `Src/Application/UseCases/Contrato/ContratoQueueBackgroundService.cs`

- [ ] **Step 1: Atualizar chamada de `ExecuteAsync` no Worker**
  Como o Worker gera contratos de forma assíncrona em segundo plano fora de requisições web, ele não possui um `ClaimsPrincipal` do usuário logado. Devemos passar uma representação de Admin do sistema (ou criar um ClaimsPrincipal com papel Admin) para a chamada de `ExecuteAsync`.
  *Código de modificação em `ContratoQueueBackgroundService.cs`:*
  Buscar por `await gerarContratoUseCase.ExecuteAsync(job.IdPedido)` e substituir por:
  ```csharp
  var adminClaims = new System.Security.Claims.ClaimsPrincipal(
      new System.Security.Claims.ClaimsIdentity(
          new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, Roles.Admin) },
          "SystemAuth"
      )
  );
  var contrato = await gerarContratoUseCase.ExecuteAsync(adminClaims, job.IdPedido);
  ```

- [ ] **Step 2: Compilar o backend**
  Run: `dotnet build Src/ProximoTurnoApi.csproj`
  Expected: Build sucedida.

---

### Task 4: Atualização do `ContratosController.cs`

**Files:**
- Modify: `Src/Application/Controllers/ContratosController.cs`

- [ ] **Step 1: Passar `User` nos métodos de Controller e tratar `UseCaseNotificationType.Forbid`**
  Atualizar os endpoints `GerarContrato` e `ConsultarContrato` para passar `User` para os Use Cases e adicionar o tratamento de status de erro `UseCaseNotificationType.Forbid`.
  *Modificação em `GerarContrato`:*
  ```csharp
      [HttpPost("pedido/{idPedido:int}")]
      public async Task<IActionResult> GerarContrato([FromRoute] int idPedido) {
          return await EncapsulateRequestAsync(async () => {
              var contrato = await _gerarContratoUseCase.ExecuteAsync(User, idPedido);

              if (!_gerarContratoUseCase.IsValid) {
                  var notification = _gerarContratoUseCase.Notifications.FirstOrDefault();
                  return notification?.Type switch {
                      UseCaseNotificationType.Forbid =>
                          Forbid(),
                      UseCaseNotificationType.NotFound =>
                          NotFound(ApiResultDTO<ContratoDTO>.CreateFailureResult(_gerarContratoUseCase.AggregateErrors())),
                      UseCaseNotificationType.Error =>
                          StatusCode(500, ApiResultDTO<ContratoDTO>.CreateFailureResult(_gerarContratoUseCase.AggregateErrors())),
                      _ =>
                          BadRequest(ApiResultDTO<ContratoDTO>.CreateFailureResult(_gerarContratoUseCase.AggregateErrors()))
                  };
              }
              // ...
  ```
  *Modificação em `ConsultarContrato`:*
  ```csharp
      [HttpGet("pedido/{idPedido:int}")]
      public async Task<IActionResult> ConsultarContrato([FromRoute] int idPedido) {
          return await EncapsulateRequestAsync(async () => {
              var contrato = await _consultarContratoUseCase.ExecuteAsync(User, idPedido);

              if (!_consultarContratoUseCase.IsValid) {
                  var notification = _consultarContratoUseCase.Notifications.FirstOrDefault();
                  if (notification?.Type == UseCaseNotificationType.Forbid) {
                      return Forbid();
                  }
                  return NotFound(ApiResultDTO<ContratoDTO>.CreateFailureResult(_consultarContratoUseCase.AggregateErrors()));
              }

              var dto = ContratoDTO.FromModel(contrato!);
              return Ok(ApiResultDTO<ContratoDTO>.CreateSuccessResult(dto, "Contrato encontrado"));
          });
      }
  ```

- [ ] **Step 2: Compilar e rodar testes do backend**
  Verificar se o projeto constrói e os testes passam.
  Run: `dotnet test Tests/ProximoTurnoApi.Tests.csproj`
  Expected: Sucesso total.

---

### Task 5: Rota Proxy Frontend e `api-service.ts`

**Files:**
- Modify: `ProximoTurno/app/api/contratos/pedido/[idPedido]/route.ts`
- Modify: `ProximoTurno/lib/api-service.ts`

- [ ] **Step 1: Interceptador na rota Next.js**
  Substituir o tratamento de erro em `app/api/contratos/pedido/[idPedido]/route.ts` para retornar status `400` (evitando logout automático) quando a resposta for `403`.
  *Código de modificação:*
  ```typescript
          if (!response.ok) {
              if (response.status === 403) {
                  return NextResponse.json(
                      { error: "Acesso negado: você não tem permissão para visualizar o contrato deste pedido." },
                      { status: 400 }
                  )
              }
              return await handleApiError(response, "Erro ao consultar contrato")
          }
  ```

- [ ] **Step 2: Tratar erro de acesso negado no `api-service.ts`**
  No método `consultarContratoPedido` em `lib/api-service.ts`, repassar o erro de acesso negado.
  *Código de modificação:*
  ```typescript
      async consultarContratoPedido(idPedido: number): Promise<ContratoAutentique | null> {
          try {
              const apiResponse: ApiResponse<ContratoAutentique> = await this.request(`/contratos/pedido/${idPedido}`)
              return apiResponse.data
          } catch (error: any) {
              if (error?.message?.includes("Acesso negado") || error?.message?.includes("Forbidden")) {
                  throw error
              }
              return null
          }
      }
  ```

---

### Task 6: Página de Contrato (`app/contrato/[idPedido]/page.tsx`)

**Files:**
- Modify: `ProximoTurno/app/contrato/[idPedido]/page.tsx`

- [ ] **Step 1: Adicionar estado local `forbidden`**
  Adicionar a declaração do estado `forbidden` no início do componente `ContratoPage`.
  *Código a ser inserido:*
  ```typescript
      const [contrato, setContrato] = useState<ContratoAutentique | null>(null)
      const [loading, setLoading] = useState(true)
      const [error, setError] = useState(false)
      const [forbidden, setForbidden] = useState(false)
  ```

- [ ] **Step 2: Parar o *polling* e setar `forbidden` no erro de Acesso Negado**
  Modificar a rotina de `pollContrato` para capturar a exceção e tratar o caso do Acesso Negado.
  *Código de modificação:*
  ```typescript
      const pollContrato = async () => {
          const elapsed = (Date.now() - startTimeRef.current) / 1000
  
          if (elapsed >= MAX_WAIT_SECONDS) {
              if (isMountedRef.current) {
                  updateLoading(false)
                  setError(true)
              }
              return
          }
  
          try {
              const result = await apiService.consultarContratoPedido(idPedido)
              if (isMountedRef.current && result?.linkAssinatura) {
                  setContrato(result)
                  updateLoading(false)
                  return
              }
          } catch (err: any) {
              console.warn(`Tentativa ${retryCountRef.current + 1} de consulta do contrato falhou`, err)
              if (err?.message?.includes("Acesso negado")) {
                  if (isMountedRef.current) {
                      updateLoading(false)
                      setForbidden(true)
                  }
                  return
              }
          }
  
          retryCountRef.current += 1
  ```

- [ ] **Step 3: Renderizar Tela de Acesso Negado**
  Exibir a tela de Acesso Negado se `forbidden` for `true`.
  *Código a ser inserido acima de `{/* Loading state */}`:*
  ```typescript
                  {/* Forbidden state */}
                  {!loading && forbidden && (
                      <Card className="max-w-2xl mx-auto">
                          <CardContent className="py-12 text-center">
                              <div className="flex flex-col items-center gap-6">
                                  <div className="rounded-full bg-destructive/10 p-4">
                                      <AlertCircle className="h-10 w-10 text-destructive" />
                                  </div>
                                  <div className="space-y-2">
                                      <p className="text-xl font-semibold">
                                          Acesso Negado
                                      </p>
                                      <p className="text-sm text-muted-foreground max-w-md mx-auto">
                                          Você não tem permissão para visualizar o contrato deste pedido.
                                      </p>
                                  </div>
                                  <Button
                                      className="gap-2"
                                      onClick={() => router.push("/pedidos")}
                                  >
                                      Voltar para Pedidos
                                  </Button>
                              </div>
                          </CardContent>
                      </Card>
                  )}
  ```

- [ ] **Step 4: Executar build do frontend**
  Executar o build de testes para certificar que o frontend não tem erros de sintaxe ou tipos.
  Run: `npm run build`
  Cwd: `ProximoTurno`
  Expected: Build sucedida.
