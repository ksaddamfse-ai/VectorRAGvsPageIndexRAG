using Microsoft.Extensions.AI;

namespace VectorRAGvsPageIndexRAG.Services;

public interface IChatClientFactory
{
    IChatClient? GetClient(string key);
}
