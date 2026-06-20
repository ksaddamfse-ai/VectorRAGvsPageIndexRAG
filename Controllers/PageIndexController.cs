using Microsoft.AspNetCore.Mvc;
using VectorRAGvsPageIndexRAG.DTOs;
using VectorRAGvsPageIndexRAG.Services;
using VectorRAGvsPageIndexRAG.Services.Interfaces;

namespace VectorRAGvsPageIndexRAG;

[ApiController]
[Route("api/pageindex")]
public class PageIndexController(IPageIndexService pageIndexService) : ControllerBase
{
    [HttpPost("documents")]
    [ProducesResponseType<PageIndexIngestionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<string>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Ingest(
        IFormFile file,
        [FromQuery] string provider = "NvidiaNim",
        [FromQuery] string model = "meta/llama-3.3-70b-instruct")
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file provided.");

        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Only PDF files are supported.");

        var result = await pageIndexService.IngestAsync(file, provider, model);
        return CreatedAtAction(nameof(Ingest), result);
    }

    [HttpGet("query")]
    [ProducesResponseType<PageIndexQueryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<string>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<string>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Query([FromQuery] PageIndexQueryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DocId))
            return BadRequest("docId is required.");

        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest("Question is required.");

        var result = await pageIndexService.QueryAsync(request);

        if (result is null)
            return NotFound("Document not found.");

        return Ok(result);
    }
}
