using VectorRAGvsPageIndexRAG.Services;

namespace VectorRAGvsPageIndexRAG.DTOs;

public record CompareQueryResponse(
    RagResult Rag,
    PageIndexResult PageIndex);

public record RagResult(
    string Answer,
    string CollectionName,
    List<RagChunkResult> Chunks,
    string? Error = null);

public record PageIndexResult(
    string Answer,
    List<PageIndexCitation> Citations,
    string? Error = null);
