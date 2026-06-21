using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using VectorRAGvsPageIndexRAG.DTOs;
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

        if (schema.Properties.TryGetValue("provider", out var providerProp))
        {
            providerProp.Enum = providers.Select(p => new OpenApiString(p)).Cast<IOpenApiAny>().ToList();
            providerProp.Default = new OpenApiString("OpenCode");
        }

        if (schema.Properties.TryGetValue("model", out var modelProp))
        {
            modelProp.Enum = models.Select(m => new OpenApiString(m)).Cast<IOpenApiAny>().ToList();
            modelProp.Default = new OpenApiString("deepseek-v4-flash-free");
        }

        if (schema.Properties.TryGetValue("topK", out var topKProp))
            topKProp.Default = new OpenApiInteger(2);

        if (schema.Properties.TryGetValue("collectionName", out var collProp))
            collProp.Default = new OpenApiString("documents");
    }
}
