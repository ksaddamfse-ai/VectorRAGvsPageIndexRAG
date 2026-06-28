using Microsoft.Extensions.AI;
using RAGBench.Services.Interfaces;

namespace RAGBench.Services;

public class MyAiService(IChatClientFactory clientFactory, ILogger<MyAiService> logger)
{
    public async Task<string?> AskAsync(string provider, string model, string question, CancellationToken cancellationToken = default)
    {
        var key = $"{provider}__{model}";
        var client = clientFactory.GetClient(key);

        if (client is null)
        {
            logger.LogWarning("Client not found for {Key}", key);
            return null;
        }

        logger.LogInformation("Asking {Provider}/{Model}: {Question}", provider, model, question);
        var response = await client.GetResponseAsync(question, cancellationToken: cancellationToken);
        return response?.Text;
    }
}
