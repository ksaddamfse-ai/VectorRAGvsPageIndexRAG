using Anthropic;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using OpenAI;
using Qdrant.Client;
using System.ClientModel;
using VectorRAGvsPageIndexRAG.Services;
using VectorRAGvsPageIndexRAG.Services.Interfaces;
using VectorRAGvsPageIndexRAG;
using VectorRAGvsPageIndexRAG.Settings;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.ParameterFilter<ProviderDropdownFilter>();
    c.SchemaFilter<ProviderModelSchemaFilter>();
});

// ── Chat providers ──
foreach (var providerSection in builder.Configuration.GetSection("ProviderRegistry").GetChildren())
{
    var providerKey = providerSection.Key;
    var providerType = providerSection["Type"] ?? "OpenAI";
    var providerEnabled = providerSection["Enabled"] != "false";
    var chatModels = providerSection.GetSection("Models").Get<List<string>>() ?? [];

    if (!providerEnabled || chatModels.Count == 0) continue;

    foreach (var chatModel in chatModels)
    {
        if (providerType != "AzureOpenAI"
            && string.IsNullOrEmpty(providerSection["ApiKey"])
            && providerSection["BaseUrl"]?.Contains("localhost") != true)
            continue;

        var serviceKey = $"{providerKey}__{chatModel}";

        builder.Services.AddKeyedChatClient(serviceKey, _ => providerType switch
        {
            "AzureOpenAI" => new AzureOpenAIClient(
                new Uri(providerSection["Endpoint"]!),
                new ApiKeyCredential(providerSection["ApiKey"]!))
                .GetChatClient(chatModel).AsIChatClient(),

            "Anthropic" => new AnthropicClient { ApiKey = providerSection["ApiKey"]! }
                .AsIChatClient(chatModel),

            _ => new OpenAIClient(
                new ApiKeyCredential(providerSection["ApiKey"] ?? "not-needed"),
                new OpenAIClientOptions { Endpoint = new Uri(providerSection["BaseUrl"]!) })
                .GetChatClient(chatModel).AsIChatClient()
        });
    }
}

// ── Active embedding generator ──
var embeddingRegistrySection = builder.Configuration.GetSection("EmbeddingRegistry");
var activeEmbeddingProvider = embeddingRegistrySection["ActiveEmbeddingProvider"]
    ?? throw new InvalidOperationException("'EmbeddingRegistry:ActiveEmbeddingProvider' is not set.");
var activeEmbeddingProviderSection = embeddingRegistrySection.GetSection(activeEmbeddingProvider);
var activeEmbeddingModel = activeEmbeddingProviderSection["Model"]
    ?? throw new InvalidOperationException($"'EmbeddingRegistry:{activeEmbeddingProvider}:Model' is not set.");

builder.Services.AddEmbeddingGenerator(_ => activeEmbeddingProviderSection["Type"] switch
{
    "AzureOpenAI" => new AzureOpenAIClient(
        new Uri(activeEmbeddingProviderSection["Endpoint"]!),
        new ApiKeyCredential(activeEmbeddingProviderSection["ApiKey"]!))
        .GetEmbeddingClient(activeEmbeddingModel).AsIEmbeddingGenerator(),

    _ => new OpenAIClient(
        new ApiKeyCredential(activeEmbeddingProviderSection["ApiKey"] ?? ""),
        new OpenAIClientOptions { Endpoint = new Uri(activeEmbeddingProviderSection["BaseUrl"]!) })
        .GetEmbeddingClient(activeEmbeddingModel).AsIEmbeddingGenerator()
});

// ── Active vector store ──
var vectorStoreRegistrySection = builder.Configuration.GetSection("VectorStoreRegistry");
var activeVectorStoreProvider = vectorStoreRegistrySection["ActiveProvider"];
var activeVectorStoreSection = vectorStoreRegistrySection.GetSection(activeVectorStoreProvider!);

builder.Services.AddSingleton(_ => new QdrantClient(
    host: activeVectorStoreSection["Host"] ?? "localhost",
    port: activeVectorStoreSection.GetValue<int>("Port")));

builder.Services.Configure<VectorStoreRegistryEntry>(options => activeVectorStoreSection.Bind(options));

// ── Application services ──
builder.Services.Configure<Dictionary<string, ProviderRegistryEntry>>(
    builder.Configuration.GetSection("ProviderRegistry"));
builder.Services.AddSingleton<IChatClientFactory, ChatClientFactory>();
builder.Services.AddSingleton<IDocumentProcessor, DocumentProcessor>();
builder.Services.AddSingleton<IRagIngestionService, RagIngestionService>();
builder.Services.AddSingleton<IRagQueryService, RagQueryService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();
