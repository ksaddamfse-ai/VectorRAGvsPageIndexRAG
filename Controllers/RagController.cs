using Microsoft.AspNetCore.Mvc;
using RAGBench.DTOs;
using RAGBench.Services.Interfaces;

namespace RAGBench;

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
    public async Task<IActionResult> Ingest(IFormFile file, [FromQuery] string collectionName = "PDFs")
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file provided.");

        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Only PDF files are supported.");

        using var stream = file.OpenReadStream();
        var text = documentProcessor.ExtractText(stream);
        var result = await ingestionService.IngestAsync(text, file.FileName, collectionName);

        return CreatedAtAction(nameof(Ingest), new RagIngestionResponse(
            result.FileName,
            result.ChunkCount,
            result.CollectionName,
            result.Chunks.Select(c => new RagChunkResponse(c.Id, c.Text, c.ChunkIndex)).ToList()));
    }

    [HttpGet("query")]
    [ProducesResponseType<RagQueryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<string>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Query([FromQuery] RagQueryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest("Question is required.");

        var result = await queryService.QueryAsync(request);
        return Ok(result);
    }
}
