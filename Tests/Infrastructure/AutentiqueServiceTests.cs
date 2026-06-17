using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using ProximoTurnoApi.Infrastructure.Services;
using Xunit;

namespace ProximoTurnoApi.Tests.Infrastructure;

public class AutentiqueServiceTests
{
    [Fact]
    public async Task CriarDocumentoAsync_WhenSandboxIsTrue_ShouldSendSandboxTrueInMutation()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var factory = new FakeHttpClientFactory(httpClient);
        var configuration = new FakeConfiguration("test-token");
        var service = new AutentiqueService(configuration, factory, NullLogger<AutentiqueService>.Instance);

        // Act
        await service.CriarDocumentoAsync(new byte[] { 1, 2, 3 }, "Contrato Teste", "Signatario Teste", sandbox: true);

        // Assert
        Assert.NotNull(handler.LastRequestContent);
        Assert.Contains("sandbox: true", handler.LastRequestContent);
    }

    [Fact]
    public async Task CriarDocumentoAsync_WhenSandboxIsFalse_ShouldSendSandboxFalseInMutation()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var factory = new FakeHttpClientFactory(httpClient);
        var configuration = new FakeConfiguration("test-token");
        var service = new AutentiqueService(configuration, factory, NullLogger<AutentiqueService>.Instance);

        // Act
        await service.CriarDocumentoAsync(new byte[] { 1, 2, 3 }, "Contrato Teste", "Signatario Teste", sandbox: false);

        // Assert
        Assert.NotNull(handler.LastRequestContent);
        Assert.Contains("sandbox: false", handler.LastRequestContent);
    }
}

public class FakeHttpMessageHandler : HttpMessageHandler
{
    public string? LastRequestContent { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
    {
        if (request.Content != null)
        {
            LastRequestContent = await request.Content.ReadAsStringAsync();
        }

        // Return a valid fake response so AutentiqueService doesn't throw during parsing
        var jsonResponse = """
        {
          "data": {
            "createDocument": {
              "id": "doc-id-123",
              "name": "Doc Teste",
              "signatures": [
                {
                  "public_id": "signer-public-id-456",
                  "name": "Signatario Teste",
                  "email": null,
                  "link": {
                    "short_link": "https://autentique.com.br/link-teste"
                  }
                }
              ]
            }
          }
        }
        """;

        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
        };
    }
}

public class FakeHttpClientFactory(HttpClient httpClient) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => httpClient;
}

public class FakeConfiguration(string apiToken) : IConfiguration
{
    public string? this[string key]
    {
        get => key == "AUTENTIQUE_API_TOKEN" ? apiToken : null;
        set => throw new NotImplementedException();
    }

    public IEnumerable<IConfigurationSection> GetChildren() => throw new NotImplementedException();
    public IConfigurationSection GetSection(string key) => throw new NotImplementedException();
    public IChangeToken GetReloadToken() => throw new NotImplementedException();
}
