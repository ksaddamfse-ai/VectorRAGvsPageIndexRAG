using RAGBench.DTOs;

namespace RAGBench.Services.Interfaces;

public interface IRagQueryService
{
    Task<RagQueryResponse> QueryAsync(RagQueryRequest request);
}
