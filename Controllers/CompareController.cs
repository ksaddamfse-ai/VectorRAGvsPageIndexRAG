using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RAGBench.DTOs;
using RAGBench.Services.Interfaces;

namespace RAGBench;

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

        var totalSw = Stopwatch.StartNew();
        var ragTask = SafeRun(() => ragQueryService.QueryAsync(ragReq));
        var piTask = SafeRun(() => pageIndexService.QueryAsync(piReq));

        await Task.WhenAll(ragTask, piTask);
        totalSw.Stop();

        var (ragData, ragError, ragMs) = await ragTask;
        var (piData, piError, piMs) = await piTask;

        var collName = string.IsNullOrWhiteSpace(request.CollectionName) ? "documents" : request.CollectionName;
        return Ok(new CompareQueryResponse(
            new RagResult(ragError ?? ragData?.Answer ?? "No result.", collName, ragData?.Chunks ?? [], ragError, ragMs),
            new PageIndexResult(piError ?? piData?.Answer ?? "Document not found.", piData?.Citations ?? [], piData?.PageCitations ?? [], piError, piMs),
            totalSw.ElapsedMilliseconds));
    }

    private static async Task<(T? Result, string? Error, long TimeMs)> SafeRun<T>(Func<Task<T>> action)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await action();
            sw.Stop();
            return (result, null, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return (default, $"Failed: {ex.Message}", sw.ElapsedMilliseconds);
        }
    }
}
