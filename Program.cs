using Anthropic;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using OpenAI;
using Qdrant.Client;
using System.ClientModel;
using VectorRAGvsPageIndexRAG.Services;
using VectorRAGvsPageIndexRAG;
using VectorRAGvsPageIndexRAG.Settings;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.ParameterFilter<ProviderDropdownFilter>();
    c.ParameterFilter<EmbeddingDropdownFilter>();
    c.SchemaFilter<ProviderModelSchemaFilter>();
});

// ── Chat providers ──
foreach (var section in builder.Configuration.GetSection("ProviderRegistry").GetChildren())
{
    var providerKey = section.Key;
    var providerType = section["Type"] ?? "OpenAI";
    var models = section.GetSection("Models").Get<List<string>>() ?? [];

    if (models.Count == 0)
        continue;

    foreach (var model in models)
    {
        if (providerType != "AzureOpenAI"
            && string.IsNullOrEmpty(section["ApiKey"])
            && section["BaseUrl"]?.Contains("localhost") != true)
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
                new ApiKeyCredential(section["ApiKey"] ?? "not-needed"),
                new OpenAIClientOptions { Endpoint = new Uri(section["BaseUrl"]!) })
                .GetChatClient(model)
                .AsIChatClient()
        });
    }
}

// ── Active embedding generator ──
var embeddingRegistry = builder.Configuration.GetSection("EmbeddingRegistry");
var activeEmbeddingKey = embeddingRegistry["ActiveEmbeddingProvider"];
var activeEmbeddingCfg = embeddingRegistry.GetSection(activeEmbeddingKey!);
var activeEmbeddingModel = activeEmbeddingCfg.GetSection("Models").Get<List<string>>()?.FirstOrDefault();
var vectorSize = activeEmbeddingCfg.GetValue<int>("VectorSize");

if (!string.IsNullOrEmpty(activeEmbeddingModel))
{
    builder.Services.AddEmbeddingGenerator(sp => activeEmbeddingCfg["Type"] switch
    {
        "AzureOpenAI" => new AzureOpenAIClient(
            new Uri(activeEmbeddingCfg["Endpoint"]!),
            new ApiKeyCredential(activeEmbeddingCfg["ApiKey"]!))
            .GetEmbeddingClient(activeEmbeddingModel)
            .AsIEmbeddingGenerator(),

        _ => new OpenAIClient(
            new ApiKeyCredential(activeEmbeddingCfg["ApiKey"] ?? ""),
            new OpenAIClientOptions { Endpoint = new Uri(activeEmbeddingCfg["BaseUrl"]!) })
            .GetEmbeddingClient(activeEmbeddingModel)
            .AsIEmbeddingGenerator()
    });

    builder.Services.Configure<ActiveEmbeddingOptions>(o =>
    {
        o.ProviderKey = activeEmbeddingKey!;
        o.Model = activeEmbeddingModel;
        o.VectorSize = vectorSize;
    });
}

// ── Active vector store ──
var vsRegistry = builder.Configuration.GetSection("VectorStoreRegistry");
var activeVs = vsRegistry["ActiveProvider"];
var activeVsCfg = vsRegistry.GetSection(activeVs!);

switch (activeVs)
{
    case "Qdrant":
        builder.Services.AddSingleton(_ => new QdrantClient(
            host: activeVsCfg["Host"] ?? "localhost",
            port: activeVsCfg.GetValue<int>("Port")));
        break;
}

builder.Services.Configure<VectorStoreRegistryEntry>(options =>
{
    activeVsCfg.Bind(options);
    options.VectorSize = vectorSize;
});

// ── Common services ──
builder.Services.Configure<Dictionary<string, ProviderRegistryEntry>>(
    builder.Configuration.GetSection("ProviderRegistry"));
builder.Services.AddSingleton<IChatClientFactory, ChatClientFactory>();
builder.Services.AddSingleton<RagIngestionService>();
builder.Services.AddSingleton<RagQueryService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();
