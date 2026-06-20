using Anthropic;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using VectorRAGvsPageIndexRAG.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

foreach (var section in builder.Configuration.GetSection("ProviderRegistry").GetChildren())
{
    var providerKey = section.Key;
    var providerType = section["Type"] ?? "OpenAI";
    var models = section.GetSection("Models").Get<List<string>>() ?? [];

    if (models.Count == 0)
        continue;

    foreach (var model in models)
    {
        if (providerType != "AzureOpenAI" && string.IsNullOrEmpty(section["ApiKey"]))
            continue;

        var key = $"{providerKey}__{model}";

        builder.Services.AddKeyedChatClient(key, _ => providerType switch
        {
            "AzureOpenAI" => new AzureOpenAIClient(
                new Uri(section["Endpoint"]!),
                new ApiKeyCredential(section["ApiKey"]!))
                .GetChatClient(model)
                .AsIChatClient(),

            "Anthropic" => new AnthropicClient { ApiKey = section["ApiKey"]! }
                .AsIChatClient(model),

            _ => new OpenAIClient(
                new ApiKeyCredential(section["ApiKey"]!),
                new OpenAIClientOptions { Endpoint = new Uri(section["BaseUrl"]!) })
                .GetChatClient(model)
                .AsIChatClient()
        });
    }
}

builder.Services.AddSingleton<IChatClientFactory, ChatClientFactory>();
builder.Services.AddTransient<MyAiService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();
