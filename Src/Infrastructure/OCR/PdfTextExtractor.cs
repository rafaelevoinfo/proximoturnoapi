using System.ClientModel;
using System.Text;
using Microsoft.Extensions.AI;
using OpenAI;
using ProximoTurnoApi.Application.UseCases.OCR;
using ProximoTurnoApi.Domain;

namespace ProximoTurnoApi.Infrastructure.OCR;

public class PdfTextExtractor(ILogger<PdfTextExtractor> _logger) : IPdfTextExtractor {
    public async Task<string> ExtractTextAsync(string pdfFilePath, CancellationToken cancellationToken) {
        var openRouterApiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY") ?? "";
        var openAiClientOptions = new OpenAIClientOptions() {
            Endpoint = new Uri("https://openrouter.ai/api/v1"),
        };

        var chatOptions = new ChatOptions() {
            Instructions = @"Você é um assistente de IA especializado em extrair texto de PDFs de manuais de jogos de tabuleiro. 
            Sua tarefa é ler o conteúdo do arquivo PDF fornecido e retornar o texto extraído em formato markdown. 
            Certifique-se de manter a formatação básica, como titulo, sub-titulos, parágrafos e listas, sempre que possível. 
            Ignore textos de capa, indices, creditos ou qualquer informação irrelevante para as regras do jogos.
            Se houver imagens ou gráficos, descreva-os brevemente no texto extraído.
            Apos realizar a extração, valide se alguma parte do texto ficou sem sentido ou incompleta, caso sim, remova-a.",
            Temperature = (float)0.3,
        };

        var agent = new OpenAIClient(new ApiKeyCredential(openRouterApiKey), openAiClientOptions)
            //.GetChatClient("openai/gpt-4o")
            .GetChatClient(IAModel.OCR_MODEL)
            .AsIChatClient();
        //.AsAIAgent();
        try {
            _logger.LogDebug("Extracting text from PDF: {PdfFilePath}", pdfFilePath);
            var message = new ChatMessage(ChatRole.User, "Extraia o texto deste PDF em formato markdown");
            message.Contents.Add(await DataContent.LoadFromAsync(pdfFilePath, "application/pdf", cancellationToken));

            var response = await agent.GetResponseAsync(
                message,
                chatOptions,
                cancellationToken
            );
            // response.Usage.

            var extractedText = response.Text;
            // var streaming = agent.GetStreamingResponseAsync(
            //     message,
            //     chatOptions,
            //     cancellationToken
            // );

            // var sb = new StringBuilder();
            // await foreach (var chunk in streaming) {
            //     _logger.LogDebug("Received chunk: {Chunk}", chunk.Text);
            //     sb.Append(chunk.Text);
            // }

            // var extractedText = sb.ToString();
            await File.WriteAllTextAsync(Path.ChangeExtension(pdfFilePath, ".md"), extractedText, cancellationToken);
            // // message.Contents.Add(new Microsoft.Extensions.AI.ChatContent("application/pdf", File.ReadAllBytes(pdfFilePath)));

            // await File.WriteAllTextAsync(Path.ChangeExtension(pdfFilePath, ".md"), response.Text, cancellationToken);
            _logger.LogInformation("Extração finalizada com sucesso");
        } catch (Exception ex) {
            _logger.LogError(ex, "Error while extracting text from PDF: {Message}", ex.Message);
            throw;
        }



        return "";
        // .AsIChatClient();
        // .AsAIAgent(new ChatClientAgentOptions() {
        //     ChatOptions = chatOptions,
        // });



        // var response = await agent.RunAsync<string>(null, null, null, cancellationToken);
        // return response.Result;
    }
}