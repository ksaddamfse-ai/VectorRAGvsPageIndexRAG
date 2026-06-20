using Microsoft.AspNetCore.Mvc;
using VectorRAGvsPageIndexRAG.DTOs;
using VectorRAGvsPageIndexRAG.Services.Interfaces;

namespace VectorRAGvsPageIndexRAG;

[ApiController]
[Route("api/rag")]
public class RagController(
    IDocumentProcessor documentProcessor,
    IRagIngestionService ingestionService,
    IRagQueryService queryService) : ControllerBase
{
    [HttpPost("documents")]
    [ProducesResponseType<RagIngestionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<string>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Ingest(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file provided.");

        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Only PDF files are supported.");

        using var stream = file.OpenReadStream();
        var text = documentProcessor.ExtractText(stream);
        var result = await ingestionService.IngestAsync(text, file.FileName);

        return CreatedAtAction(nameof(Ingest), new RagIngestionResponse(
            result.FileName,
            result.ChunkCount,
            result.Chunks.Select(c => new RagChunkResponse(c.Id, c.Text, c.ChunkIndex)).ToList()));
    }

    [HttpPost("query")]
    [ProducesResponseType<RagQueryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<string>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Query([FromBody] RagQueryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest("Question is required.");

        var result = await queryService.QueryAsync(request);
        return Ok(result);
    }
}
