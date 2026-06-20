using Microsoft.AspNetCore.Mvc;
using VectorRAGvsPageIndexRAG.DTOs;
using VectorRAGvsPageIndexRAG.Services;
using VectorRAGvsPageIndexRAG.Services.Interfaces;

namespace VectorRAGvsPageIndexRAG;

[ApiController]
[Route("api/compare")]
public class CompareController(
    IRagQueryService ragQueryService,
    IPageIndexService pageIndexService) : ControllerBase
{
    [HttpGet("query")]
    [ProducesResponseType<CompareQueryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<string>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Query([FromQuery] CompareQueryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest("Question is required.");
        if (string.IsNullOrWhiteSpace(request.DocId))
            return BadRequest("docId is required.");

        var ragReq = new RagQueryRequest(request.Question, request.Provider, request.Model, request.TopK);
        var piReq = new PageIndexQueryRequest(request.DocId, request.Question, request.Provider, request.Model);

        var ragTask = ragQueryService.QueryAsync(ragReq);
        var piTask = pageIndexService.QueryAsync(piReq);

        await Task.WhenAll(ragTask, piTask);

        var ragResult = ragTask.Result;
        var piResult = piTask.Result;

        return Ok(new CompareQueryResponse(
            new RagResult(ragResult.Answer, ragResult.Chunks),
            new PageIndexResult(
                piResult?.Answer ?? "Document not found.",
                piResult?.Citations ?? [])));
    }
}
