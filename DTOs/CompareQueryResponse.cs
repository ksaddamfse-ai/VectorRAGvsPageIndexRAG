using VectorRAGvsPageIndexRAG.Services;

namespace VectorRAGvsPageIndexRAG.DTOs;

public record CompareQueryResponse(
    RagResult Rag,
    PageIndexResult PageIndex,
    long TotalTimeMs);

public record RagResult(
    string Answer,
    string CollectionName,
    List<RagChunkResult> Chunks,
    string? Error = null,
    long TimeMs = 0);

public record PageIndexResult(
    string Answer,
    List<PageIndexCitation> Citations,
    List<PageCitation> PageCitations,
    string? Error = null,
    long TimeMs = 0);
