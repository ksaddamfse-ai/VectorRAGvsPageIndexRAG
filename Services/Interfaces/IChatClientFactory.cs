using Microsoft.Extensions.AI;

namespace RAGBench.Services.Interfaces;

public interface IChatClientFactory
{
    IChatClient? GetClient(string key);
}
