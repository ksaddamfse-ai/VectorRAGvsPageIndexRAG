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
        var providers = registry.Value.Where(e => e.Value.Enabled).Select(e => e.Key).ToList();
        var models = registry.Value.SelectMany(e => e.Value.Models).Distinct().ToList();

        if (schema.Properties.TryGetValue("provider", out var provProp))
        {
            provProp.Enum = providers.Select(p => new OpenApiString(p)).Cast<IOpenApiAny>().ToList();
            provProp.Default = new OpenApiString("NvidiaNim");
        }

        if (schema.Properties.TryGetValue("model", out var modelProp))
        {
            modelProp.Enum = models.Select(m => new OpenApiString(m)).Cast<IOpenApiAny>().ToList();
            modelProp.Default = new OpenApiString("meta/llama-3.3-70b-instruct");
        }

        if (schema.Properties.TryGetValue("topK", out var topKProp))
            topKProp.Default = new OpenApiInteger(2);
    }
}
