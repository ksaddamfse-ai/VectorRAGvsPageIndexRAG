using Microsoft.Extensions.AI;

namespace VectorRAGvsPageIndexRAG.Services.Interfaces;

public interface IChatClientFactory
{
    IChatClient? GetClient(string key);
}
