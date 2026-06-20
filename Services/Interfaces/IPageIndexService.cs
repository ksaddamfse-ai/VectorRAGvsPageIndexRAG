using VectorRAGvsPageIndexRAG.DTOs;

namespace VectorRAGvsPageIndexRAG.Services.Interfaces;

public interface IPageIndexService
{
    Task<PageIndexIngestionResponse> IngestAsync(IFormFile file,
        string provider = "NvidiaNim", string model = "meta/llama-3.3-70b-instruct");

    Task<PageIndexQueryResponse?> QueryAsync(PageIndexQueryRequest request);
}
