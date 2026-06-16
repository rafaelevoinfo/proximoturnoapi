using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Infrastructure.Services;

public interface IContratoPdfService {
    Task<byte[]> GerarPdfAsync(Domain.Pedido pedido);
}

public class ContratoPdfService(ILogger<ContratoPdfService> logger, IWebHostEnvironment environment) : IContratoPdfService {

    private static readonly CultureInfo PtBr = new("pt-BR");

    public async Task<byte[]> GerarPdfAsync(Domain.Pedido pedido) {
        var templatePath = Path.Combine(environment.ContentRootPath, "Templates", "contrato-aluguel.html");
        var templateHtml = await File.ReadAllTextAsync(templatePath);

        var html = SubstituirPlaceholders(templateHtml, pedido);

        return await ConverterHtmlParaPdfAsync(html);
    }

    private static string SubstituirPlaceholders(string html, Domain.Pedido pedido) {
        var cliente = pedido.Cliente;
        var agora = DateTime.Now;

        var tabelaItens = new StringBuilder();
        var index = 1;
        foreach (var item in pedido.Items) {
            var nomeJogo = item.JogoCopia?.Jogo?.Nome ?? "N/A";
            tabelaItens.AppendLine($"""
                <tr>
                    <td>{index}</td>
                    <td>{nomeJogo}</td>
                    <td>R$ {item.Valor.ToString("N2", PtBr)}</td>
                    <td>{item.DataDevolucao.ToString("dd/MM/yyyy", PtBr)}</td>
                </tr>
            """);
            index++;
        }

        return html
            .Replace("{{NUMERO_PEDIDO}}", pedido.Id.ToString())
            .Replace("{{DATA_PEDIDO}}", pedido.DataHora.ToString("dd/MM/yyyy", PtBr))
            .Replace("{{NOME_CLIENTE}}", CultureInfo.CurrentCulture.TextInfo.ToTitleCase(cliente.Nome))
            .Replace("{{TELEFONE_CLIENTE}}", cliente.Telefone)
            .Replace("{{EMAIL_CLIENTE}}", cliente.Email)
            .Replace("{{ENDERECO_CLIENTE}}", cliente.Endereco)
            .Replace("{{TABELA_ITENS}}", tabelaItens.ToString())
            .Replace("{{VALOR_TOTAL}}", pedido.ValorTotal.ToString("N2", PtBr))
            .Replace("{{VALOR_DESCONTO}}", pedido.ValorDesconto.ToString("N2", PtBr))
            .Replace("{{METODO_PAGAMENTO}}", pedido.MetodoPagamento ?? "Não informado")
            .Replace("{{METODO_ENTREGA}}", pedido.MetodoEntrega ?? "Não informado")
            .Replace("{{CIDADE}}", "Sua Cidade") // TODO: configurar via appsettings
            .Replace("{{DATA_ATUAL}}", agora.ToString("dd 'de' MMMM 'de' yyyy", PtBr));
    }

    private async Task<byte[]> ConverterHtmlParaPdfAsync(string html) {
        logger.LogInformation("Iniciando conversão HTML para PDF via PuppeteerSharp");

        var executablePath = Environment.GetEnvironmentVariable("PUPPETEER_EXECUTABLE_PATH");

        LaunchOptions launchOptions;
        if (!string.IsNullOrEmpty(executablePath)) {
            logger.LogInformation("Utilizando executável do Chromium configurado em PUPPETEER_EXECUTABLE_PATH: {Path}", executablePath);
            launchOptions = new LaunchOptions {
                Headless = true,
                ExecutablePath = executablePath,
                Args = ["--no-sandbox", "--disable-setuid-sandbox", "--disable-dev-shm-usage"]
            };
        } else {
            logger.LogInformation("PUPPETEER_EXECUTABLE_PATH não configurado. Baixando Chromium via BrowserFetcher");
            var browserFetcher = new BrowserFetcher();
            await browserFetcher.DownloadAsync();
            launchOptions = new LaunchOptions {
                Headless = true,
                Args = ["--no-sandbox", "--disable-setuid-sandbox", "--disable-dev-shm-usage"]
            };
        }

        await using var browser = await Puppeteer.LaunchAsync(launchOptions);

        await using var page = await browser.NewPageAsync();
        await page.SetContentAsync(html, new SetContentOptions {
            WaitUntil = [WaitUntilNavigation.DOMContentLoaded]
        });

        var pdfBytes = await page.PdfDataAsync(new PdfOptions {
            Format = PaperFormat.A4,
            PrintBackground = true,
            MarginOptions = new MarginOptions {
                Top = "10mm",
                Bottom = "10mm",
                Left = "10mm",
                Right = "10mm"
            }
        });

        logger.LogInformation("PDF gerado com sucesso ({Bytes} bytes)", pdfBytes.Length);
        return pdfBytes;
    }
}
