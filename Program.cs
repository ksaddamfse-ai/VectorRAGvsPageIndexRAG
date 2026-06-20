using Microsoft.Extensions.AI;
using VectorRAGvsPageIndexRAG.Services;
using OpenAI;
using System.ClientModel;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

foreach (var section in builder.Configuration.GetSection("ProviderRegistry").GetChildren())
{
    var providerKey = section.Key;
    var models = section.GetSection("Models").Get<List<string>>() ?? [];

    if (models.Count == 0)
        continue;

    foreach (var model in models)
    {
        var key = $"{providerKey}__{model}";

        if (string.IsNullOrEmpty(section["ApiKey"]))
            continue;

        builder.Services.AddKeyedChatClient(key, _ =>
        {
            return new OpenAIClient(
                new ApiKeyCredential(section["ApiKey"]!),
                new OpenAIClientOptions { Endpoint = new Uri(section["BaseUrl"]!) })
                .GetChatClient(model)
                .AsIChatClient();
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
