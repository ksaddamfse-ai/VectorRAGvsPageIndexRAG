using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace VectorRAGvsPageIndexRAG.Services;

public class ChatClientFactory(IServiceProvider serviceProvider) : IChatClientFactory
{
    public IChatClient? GetClient(string key) =>
        serviceProvider.GetKeyedService<IChatClient>(key);
}
