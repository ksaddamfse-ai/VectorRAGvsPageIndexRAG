using VectorRAGvsPageIndexRAG.Services;

namespace VectorRAGvsPageIndexRAG.DTOs;

public record CompareQueryResponse(
    RagResult Rag,
    PageIndexResult PageIndex);

public record RagResult(
    string Answer,
    List<RagChunkResult> Chunks);

public record PageIndexResult(
    string Answer,
    List<PageIndexCitation> Citations);
