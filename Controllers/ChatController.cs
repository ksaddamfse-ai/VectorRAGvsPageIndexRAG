using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using VectorRAGvsPageIndexRAG.Services.Interfaces;
using VectorRAGvsPageIndexRAG.Settings;

namespace VectorRAGvsPageIndexRAG;

[ApiController]
[Route("api/chat")]
public class ChatController(
    ILogger<ChatController> logger,
    IOptions<Dictionary<string, ProviderRegistryEntry>> registry,
    IChatClientFactory clientFactory) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<string>(StatusCodes.Status200OK)]
    [ProducesResponseType<string>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Ask(
        [FromQuery] string question,
        [FromQuery] string provider = "OpenRouter",
        [FromQuery] string model = "openrouter/free",
        CancellationToken cancellationToken = default)
    {
        if (!registry.Value.TryGetValue(provider, out var entry))
            return BadRequest($"Provider '{provider}' not found in registry");

        if (!entry.Enabled)
            return BadRequest($"Provider '{provider}' is disabled");

        if (!entry.Models.Contains(model, StringComparer.OrdinalIgnoreCase))
            return BadRequest($"Provider '{provider}' does not have model '{model}'");

        var key = $"{provider}__{model}";
        var client = clientFactory.GetClient(key);
        if (client is null)
            return BadRequest($"Provider '{provider}' model '{model}' not registered");

        logger.LogInformation("Chat request ({Provider}/{Model}): {Question}", provider, model, question);
        var response = await client.GetResponseAsync(question, cancellationToken: cancellationToken);
        return Ok(response?.Text);
    }
}
