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
        if (string.IsNullOrWhiteSpace(request.GroupName))
            return BadRequest("groupName is required.");

        var ragReq = new RagQueryRequest(request.Question, request.Provider, request.Model, request.TopK, request.CollectionName);
        var piReq = new PageIndexQueryRequest(request.Question, request.Provider, request.Model, request.GroupName);

        var ragTask = SafeRun(() => ragQueryService.QueryAsync(ragReq));
        var piTask = SafeRun(() => pageIndexService.QueryAsync(piReq));

        await Task.WhenAll(ragTask, piTask);

        var (ragData, ragError) = await ragTask;
        var (piData, piError) = await piTask;

        var collName = string.IsNullOrWhiteSpace(request.CollectionName) ? "documents" : request.CollectionName;
        return Ok(new CompareQueryResponse(
            new RagResult(ragError ?? ragData?.Answer ?? "No result.", collName, ragData?.Chunks ?? [], ragError),
            new PageIndexResult(piError ?? piData?.Answer ?? "Document not found.", piData?.Citations ?? [], piError)));
    }

    private static async Task<(T? Result, string? Error)> SafeRun<T>(Func<Task<T>> action)
    {
        try { return (await action(), null); }
        catch (Exception ex) { return (default, $"Failed: {ex.Message}"); }
    }
}
