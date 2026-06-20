using Microsoft.AspNetCore.Mvc;
using VectorRAGvsPageIndexRAG.Services;

namespace VectorRAGvsPageIndexRAG;

[ApiController]
[Route("api/chat")]
public class ChatController(MyAiService aiService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Ask(
        [FromQuery] string question,
        [FromQuery] string provider = "OpenRouter",
        [FromQuery] string model = "openrouter/free",
        CancellationToken cancellationToken = default)
    {
        var result = await aiService.AskAsync(provider, model, question, cancellationToken);

        if (result is null)
            return BadRequest($"Provider '{provider}' model '{model}' not found or disabled");

        return Ok(result);
    }
}
