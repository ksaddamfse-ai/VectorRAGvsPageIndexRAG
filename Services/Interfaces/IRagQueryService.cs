using VectorRAGvsPageIndexRAG.DTOs;

namespace VectorRAGvsPageIndexRAG.Services.Interfaces;

public interface IRagQueryService
{
    Task<RagQueryResponse> QueryAsync(RagQueryRequest request);
}
