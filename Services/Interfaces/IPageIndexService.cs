using RAGBench.DTOs;

namespace RAGBench.Services.Interfaces;

public interface IPageIndexService
{
    Task<PageIndexIngestionResponse> IngestAsync(IFormFile file,
        string provider = "NvidiaNim", string model = "meta/llama-3.3-70b-instruct",
        string groupName = "PDFs");

    Task<PageIndexQueryResponse?> QueryAsync(PageIndexQueryRequest request);
}
