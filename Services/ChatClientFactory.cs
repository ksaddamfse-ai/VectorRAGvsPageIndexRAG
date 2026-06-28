using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using RAGBench.Services.Interfaces;

namespace RAGBench.Services;

public class ChatClientFactory(IServiceProvider serviceProvider) : IChatClientFactory
{
    public IChatClient? GetClient(string key) =>
        serviceProvider.GetKeyedService<IChatClient>(key);
}
