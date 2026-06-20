using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using VectorRAGvsPageIndexRAG.Services;
using VectorRAGvsPageIndexRAG.Settings;

namespace VectorRAGvsPageIndexRAG;

public class ProviderModelSchemaFilter(
    IOptions<Dictionary<string, ProviderRegistryEntry>> registry) : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type != typeof(RagQueryRequest)) return;

        var providers = registry.Value.Where(e => e.Value.Enabled).Select(e => e.Key).ToList();
        var models = registry.Value.SelectMany(e => e.Value.Models).Distinct().ToList();

        schema.Properties["provider"].Enum =
            providers.Select(p => new OpenApiString(p)).Cast<IOpenApiAny>().ToList();
        schema.Properties["provider"].Default = new OpenApiString("OpenRouter");

        schema.Properties["model"].Enum =
            models.Select(m => new OpenApiString(m)).Cast<IOpenApiAny>().ToList();
        schema.Properties["model"].Default = new OpenApiString("openrouter/free");
    }
}
