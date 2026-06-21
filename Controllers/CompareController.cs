using Microsoft.AspNetCore.Mvc;
using VectorRAGvsPageIndexRAG.DTOs;
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

        var ragReq = new RagQueryRequest(request.Question, request.Provider, request.Model, request.TopK, request.CollectionName);
        var piReq = new PageIndexQueryRequest(request.DocId, request.Question, request.Provider, request.Model);

        var ragTask = ragQueryService.QueryAsync(ragReq);
        var piTask = pageIndexService.QueryAsync(piReq);

        await Task.WhenAll(ragTask, piTask);

        var ragResult = ragTask.Result;
        var piResult = piTask.Result;

        var collName = string.IsNullOrWhiteSpace(request.CollectionName) ? "documents" : request.CollectionName;
        return Ok(new CompareQueryResponse(
            new RagResult(ragResult.Answer, collName, ragResult.Chunks),
            new PageIndexResult(
                piResult?.Answer ?? "Document not found.",
                piResult?.Citations ?? [])));
    }
}
