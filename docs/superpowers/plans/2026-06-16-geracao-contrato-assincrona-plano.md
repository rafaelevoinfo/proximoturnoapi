# Plano de Implementação: Geração de Contrato Assíncrona

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Integrar a geração automática e assíncrona de contratos digitais no fluxo de criação de novos pedidos, garantindo retentativas progressivas sem bloquear a API principal.

**Architecture:** Fila em memória thread-safe baseada em `System.Threading.Channels` consumida por um `BackgroundService` em segundo plano. Escopo de DI isolado por execução para invocar os Use Cases Scoped.

**Tech Stack:** .NET 10.0, System.Threading.Channels, Microsoft.Extensions.Hosting

---

### Task 1: Criar a Fila de Contratos (`IContratoQueue` e `ContratoQueue`)

**Files:**
- Create: `Src/Application/UseCases/Contrato/ContratoJob.cs`
- Create: `Src/Application/UseCases/Contrato/IContratoQueue.cs`
- Create: `Src/Application/UseCases/Contrato/ContratoQueue.cs`
- Create: `Tests/Domain/ContratoQueueTests.cs`

- [ ] **Step 1: Escrever o teste que falha para a fila**
  Criar o arquivo `Tests/Domain/ContratoQueueTests.cs` usando namespace file-scoped:
  ```csharp
  using System.Threading;
  using System.Threading.Tasks;
  using ProximoTurnoApi.Application.UseCases;
  using Xunit;

  namespace ProximoTurnoApi.Tests.Domain;

  public class ContratoQueueTests
  {
      [Fact]
      public async Task Enfileirar_DeveAdicionarItemNaFila_EDesenfileirarDeveRetornarNaOrdem()
      {
          // Arrange
          var queue = new ContratoQueue();

          // Act
          queue.Enfileirar(1);
          queue.Enfileirar(2);

          var job1 = await queue.DesenfileirarAsync(CancellationToken.None);
          var job2 = await queue.DesenfileirarAsync(CancellationToken.None);

          // Assert
          Assert.Equal(1, job1.IdPedido);
          Assert.Equal(0, job1.Tentativas);
          Assert.Equal(2, job2.IdPedido);
          Assert.Equal(0, job2.Tentativas);
      }
  }
  ```

- [ ] **Step 2: Executar o teste e certificar-se de que falha**
  Executar: `dotnet test --filter "FullyQualifiedName~ContratoQueueTests"`
  Esperado: Erro de compilação (classes `ContratoQueue` e `ContratoJob` não encontradas).

- [ ] **Step 3: Criar a mensagem do Job (`ContratoJob`)**
  Criar o arquivo `Src/Application/UseCases/Contrato/ContratoJob.cs` com namespace file-scoped:
  ```csharp
  namespace ProximoTurnoApi.Application.UseCases;

  public class ContratoJob
  {
      public int IdPedido { get; }
      public int Tentativas { get; set; }

      public ContratoJob(int idPedido, int tentativas = 0)
      {
          IdPedido = idPedido;
          Tentativas = tentativas;
      }
  }
  ```

- [ ] **Step 4: Criar a interface da Fila (`IContratoQueue`)**
  Criar o arquivo `Src/Application/UseCases/Contrato/IContratoQueue.cs` com namespace file-scoped:
  ```csharp
  using System.Threading;
  using System.Threading.Tasks;

  namespace ProximoTurnoApi.Application.UseCases;

  public interface IContratoQueue
  {
      void Enfileirar(int idPedido, int tentativa = 0);
      ValueTask<ContratoJob> DesenfileirarAsync(CancellationToken cancellationToken);
  }
  ```

- [ ] **Step 5: Criar a implementação da Fila (`ContratoQueue`)**
  Criar o arquivo `Src/Application/UseCases/Contrato/ContratoQueue.cs` com namespace file-scoped:
  ```csharp
  using System.Threading;
  using System.Threading.Channels;
  using System.Threading.Tasks;

  namespace ProximoTurnoApi.Application.UseCases;

  public class ContratoQueue : IContratoQueue
  {
      private readonly Channel<ContratoJob> _channel;

      public ContratoQueue()
      {
          _channel = Channel.CreateUnbounded<ContratoJob>(new UnboundedChannelOptions
          {
              SingleReader = true,
              SingleWriter = false
          });
      }

      public void Enfileirar(int idPedido, int tentativa = 0)
      {
          _channel.Writer.TryWrite(new ContratoJob(idPedido, tentativa));
      }

      public ValueTask<ContratoJob> DesenfileirarAsync(CancellationToken cancellationToken)
      {
          return _channel.Reader.ReadAsync(cancellationToken);
      }
  }
  ```

- [ ] **Step 6: Executar o teste e certificar-se de que passa**
  Executar: `dotnet test --filter "FullyQualifiedName~ContratoQueueTests"`
  Esperado: PASS.

- [ ] **Step 7: Realizar Commit**
  Executar:
  ```bash
  git add Src/Application/UseCases/Contrato/ContratoJob.cs Src/Application/UseCases/Contrato/IContratoQueue.cs Src/Application/UseCases/Contrato/ContratoQueue.cs Tests/Domain/ContratoQueueTests.cs
  git commit -m "feat: add ContratoQueue using System.Threading.Channels"
  ```

---

### Task 2: Integrar Enfileiramento no Use Case `CadastroPedido`

**Files:**
- Modify: `Src/Application/UseCases/Pedido/CadastroPedido.cs`

- [ ] **Step 1: Modificar `CadastroPedido.cs` para injetar e chamar `IContratoQueue`**
  Alterar a declaração da classe `CadastroPedido` (linhas 11-17) e chamar `_contratoQueue.Enfileirar` logo após a gravação com sucesso (linha 97+):
  
  Código antigo (linhas 11-17):
  ```csharp
  public class CadastroPedido(IPedidoRepository pedidoRepository,
      IJogoRepository _jogoRepository,
      IClienteRepository _clienteRepository,
      ICategoriaRepository _categoriaRepository,
      UserManager<Usuario> _userManager,
      ValidarCupom _validarCupom,
      ILogger<CadastroPedido> logger) : PedidoUseCaseBasico(pedidoRepository) {
  ```

  Código novo (linhas 11-17):
  ```csharp
  public class CadastroPedido(IPedidoRepository pedidoRepository,
      IJogoRepository _jogoRepository,
      IClienteRepository _clienteRepository,
      ICategoriaRepository _categoriaRepository,
      UserManager<Usuario> _userManager,
      ValidarCupom _validarCupom,
      IContratoQueue _contratoQueue,
      ILogger<CadastroPedido> logger) : PedidoUseCaseBasico(pedidoRepository) {
  ```

  Código antigo (linhas 95-99):
  ```csharp
          try {
              await _pedidoRepository.SaveAsync(pedido);
              logger.LogInformation("Pedido {PedidoId} cadastrado com sucesso para o cliente {ClienteId}.", pedido.Id, cliente.Id);
              return pedido.Id;
  ```

  Código novo (linhas 95-102):
  ```csharp
          try {
              await _pedidoRepository.SaveAsync(pedido);
              logger.LogInformation("Pedido {PedidoId} cadastrado com sucesso para o cliente {ClienteId}.", pedido.Id, cliente.Id);
              
              // Enfileira a geração de contrato de forma assíncrona
              _contratoQueue.Enfileirar(pedido.Id);
              
              return pedido.Id;
  ```

- [ ] **Step 2: Executar testes de regressão**
  Executar: `dotnet test`
  Esperado: Compilação com aviso (ou erro) que `CadastroPedido` não está recebendo a nova dependência no registro de DI do `Program.cs`.

- [ ] **Step 3: Realizar Commit**
  Executar:
  ```bash
  git add Src/Application/UseCases/Pedido/CadastroPedido.cs
  git commit -m "feat: enqueue contract generation on order success in CadastroPedido"
  ```

---

### Task 3: Criar o Serviço de Segundo Plano (`ContratoQueueBackgroundService`) e Testes

**Files:**
- Create: `Src/Application/UseCases/Contrato/ContratoQueueBackgroundService.cs`
- Create: `Tests/Domain/ContratoQueueBackgroundServiceTests.cs`

- [ ] **Step 1: Escrever teste unitário para o worker em segundo plano**
  Criar o arquivo `Tests/Domain/ContratoQueueBackgroundServiceTests.cs` testando a lógica de reprocessamento em caso de erro. Usaremos stubs para o `IServiceScopeFactory`, `IServiceProvider` e simulador do worker.
  
  Código do teste:
  ```csharp
  using System;
  using System.Collections.Generic;
  using System.Threading;
  using System.Threading.Tasks;
  using Microsoft.Extensions.DependencyInjection;
  using Microsoft.Extensions.Logging.Abstractions;
  using ProximoTurnoApi.Application.UseCases;
  using Xunit;

  namespace ProximoTurnoApi.Tests.Domain;

  public class ContratoQueueBackgroundServiceTests
  {
      private class FakeContratoQueue : IContratoQueue
      {
          public readonly List<ContratoJob> Jobs = new();
          private readonly SemaphoreSlim _sem = new(0);

          public void Enfileirar(int idPedido, int tentativa = 0)
          {
              Jobs.Add(new ContratoJob(idPedido, tentativa));
              _sem.Release();
          }

          public async ValueTask<ContratoJob> DesenfileirarAsync(CancellationToken cancellationToken)
          {
              await _sem.WaitAsync(cancellationToken);
              var job = Jobs[0];
              Jobs.RemoveAt(0);
              return job;
          }
      }

      // Este teste valida que o background service enfileira novamente o job caso ocorra uma falha
      [Fact]
      public async Task ExecuteAsync_QuandoOcorreErro_DeveReenfileirarJobComDelay()
      {
          // Arrange
          var queue = new FakeContratoQueue();
          queue.Enfileirar(42, 0); // Pedido 42, tentativa 0

          // Simulador do escopo para retornar erro na resolução/execução
          var serviceProvider = new FakeServiceProvider(_ => throw new Exception("Falha de rede Simulada"));
          var scopeFactory = new FakeServiceScopeFactory(serviceProvider);
          
          var service = new ContratoQueueBackgroundService(
              queue, 
              scopeFactory, 
              NullLogger<ContratoQueueBackgroundService>.Instance,
              new[] { TimeSpan.FromMilliseconds(1) } // Delay instantâneo para testes
          );

          using var cts = new CancellationTokenSource();

          // Act
          var runTask = service.StartAsync(cts.Token);
          
          // Aguarda um pequeno momento para processamento
          await Task.Delay(100);
          await service.StopAsync(CancellationToken.None);
          cts.Cancel();

          // Assert
          // Deve ter re-enfileirado com tentativa 1
          Assert.Single(queue.Jobs);
          Assert.Equal(42, queue.Jobs[0].IdPedido);
          Assert.Equal(1, queue.Jobs[0].Tentativas);
      }
  }

  public class FakeServiceScopeFactory(IServiceProvider serviceProvider) : IServiceScopeFactory
  {
      public IServiceScope CreateScope() => new FakeServiceScope(serviceProvider);
  }

  public class FakeServiceScope(IServiceProvider serviceProvider) : IServiceScope
  {
      public IServiceProvider ServiceProvider => serviceProvider;
      public void Dispose() { }
  }

  public class FakeServiceProvider(Func<Type, object?> getService) : IServiceProvider
  {
      public object? GetService(Type serviceType) => getService(serviceType);
  }
  ```

- [ ] **Step 2: Executar o teste e certificar-se de que falha**
  Executar: `dotnet test --filter "FullyQualifiedName~ContratoQueueBackgroundServiceTests"`
  Esperado: Falha de compilação (classe `ContratoQueueBackgroundService` não existe).

- [ ] **Step 3: Implementar o `ContratoQueueBackgroundService`**
  Criar o arquivo `Src/Application/UseCases/Contrato/ContratoQueueBackgroundService.cs` com namespace file-scoped:
  ```csharp
  using System;
  using System.Linq;
  using System.Threading;
  using System.Threading.Tasks;
  using Microsoft.Extensions.DependencyInjection;
  using Microsoft.Extensions.Hosting;
  using Microsoft.Extensions.Logging;

  namespace ProximoTurnoApi.Application.UseCases;

  public class ContratoQueueBackgroundService : BackgroundService
  {
      private readonly IContratoQueue _queue;
      private readonly IServiceScopeFactory _scopeFactory;
      private readonly ILogger<ContratoQueueBackgroundService> _logger;
      private readonly TimeSpan[] _delaysRetentativa;

      public ContratoQueueBackgroundService(
          IContratoQueue queue,
          IServiceScopeFactory scopeFactory,
          ILogger<ContratoQueueBackgroundService> logger,
          TimeSpan[]? delaysRetentativa = null)
      {
          _queue = queue;
          _scopeFactory = scopeFactory;
          _logger = logger;
          _delaysRetentativa = delaysRetentativa ?? new[]
          {
              TimeSpan.FromMinutes(1),
              TimeSpan.FromMinutes(5),
              TimeSpan.FromMinutes(15)
          };
      }

      protected override async Task ExecuteAsync(CancellationToken stoppingToken)
      {
          _logger.LogInformation("ContratoQueueBackgroundService iniciado.");

          while (!stoppingToken.IsCancellationRequested)
          {
              try
              {
                  var job = await _queue.DesenfileirarAsync(stoppingToken);
                  _logger.LogInformation("Job de geração de contrato para o pedido {IdPedido} retirado da fila.", job.IdPedido);

                  // Executa em segundo plano sem bloquear a leitura dos próximos itens da fila
                  _ = ProcessarJobAsync(job, stoppingToken);
              }
              catch (OperationCanceledException)
              {
                  break;
              }
              catch (Exception ex)
              {
                  _logger.LogError(ex, "Erro no loop principal do serviço de fila de contratos.");
              }
          }

          _logger.LogInformation("ContratoQueueBackgroundService finalizado.");
      }

      private async Task ProcessarJobAsync(ContratoJob job, CancellationToken stoppingToken)
      {
          try
          {
              using var scope = _scopeFactory.CreateScope();
              var gerarContratoUseCase = scope.ServiceProvider.GetRequiredService<GerarContratoPedido>();

              var contrato = await gerarContratoUseCase.ExecuteAsync(job.IdPedido);

              if (!gerarContratoUseCase.IsValid)
              {
                  var erroNotificacao = gerarContratoUseCase.Notifications.FirstOrDefault();
                  
                  // Se o erro for de validação (ex: BadRequest/NotFound), não re-enfileiramos
                  if (erroNotificacao?.Type == UseCaseNotificationType.BadRequest || 
                      erroNotificacao?.Type == UseCaseNotificationType.NotFound)
                  {
                      _logger.LogWarning("Falha de validação ao gerar contrato do pedido {IdPedido}: {Erro}. Nenhuma retentativa será agendada.", 
                          job.IdPedido, gerarContratoUseCase.AggregateErrors());
                      return;
                  }

                  throw new Exception($"Falha de integração ao gerar contrato: {gerarContratoUseCase.AggregateErrors()}");
              }

              _logger.LogInformation("Contrato gerado e enviado com sucesso para o pedido {IdPedido}.", job.IdPedido);
          }
          catch (Exception ex)
          {
              _logger.LogError(ex, "Erro ao processar job de geração de contrato do pedido {IdPedido}. Tentativa atual: {Tentativas}", job.IdPedido, job.Tentativas);
              
              if (job.Tentativas < _delaysRetentativa.Length)
              {
                  var delay = _delaysRetentativa[job.Tentativas];
                  job.Tentativas++;

                  _logger.LogInformation("Re-enfileirando geração de contrato do pedido {IdPedido} com delay de {Delay}.", job.IdPedido, delay);
                  
                  // Agenda o re-enfileiramento assíncrono
                  _ = Task.Run(async () =>
                  {
                      try
                      {
                          await Task.Delay(delay, stoppingToken);
                          _queue.Enfileirar(job.IdPedido, job.Tentativas);
                      }
                      catch (OperationCanceledException)
                      {
                          // Ignorar cancelamento da aplicação
                      }
                  }, stoppingToken);
              }
              else
              {
                  _logger.LogError("Número máximo de tentativas de envio de contrato excedido para o pedido {IdPedido}.", job.IdPedido);
              }
          }
      }
  }
  ```

- [ ] **Step 4: Executar o teste e verificar aprovação**
  Executar: `dotnet test --filter "FullyQualifiedName~ContratoQueueBackgroundServiceTests"`
  Esperado: PASS.

- [ ] **Step 5: Realizar Commit**
  Executar:
  ```bash
  git add Src/Application/UseCases/Contrato/ContratoQueueBackgroundService.cs Tests/Domain/ContratoQueueBackgroundServiceTests.cs
  git commit -m "feat: implement ContratoQueueBackgroundService and tests"
  ```

---

### Task 4: Registrar os Serviços no `Program.cs`

**Files:**
- Modify: `Src/Program.cs`

- [ ] **Step 1: Registrar `IContratoQueue` e `ContratoQueueBackgroundService` no `Program.cs`**
  Adicionar os registros singleton e do hosted service no arquivo `Src/Program.cs`.
  
  Localizar:
  ```csharp
  builder.Services.AddScoped<IContratoRepository, ContratoRepository>();
  ```
  Adicionar logo abaixo:
  ```csharp
  builder.Services.AddSingleton<IContratoQueue, ContratoQueue>();
  builder.Services.AddHostedService<ContratoQueueBackgroundService>();
  ```

- [ ] **Step 2: Rodar o projeto de testes completo**
  Executar: `dotnet test`
  Esperado: Todos os 42 testes passando com sucesso.

- [ ] **Step 3: Realizar Commit**
  Executar:
  ```bash
  git add Src/Program.cs
  git commit -m "feat: register ContratoQueue and ContratoQueueBackgroundService in Program"
  ```
