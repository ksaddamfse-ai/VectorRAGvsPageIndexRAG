using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using RAGBench.DTOs;
using RAGBench.Settings;

namespace RAGBench;

public class ProviderModelSchemaFilter(
    IOptions<Dictionary<string, ProviderRegistryEntry>> registry) : ISchemaFilter
{
    private static readonly HashSet<string> HandledTypes =
    [
        nameof(RagQueryRequest),
        nameof(PageIndexQueryRequest),
        nameof(CompareQueryRequest)
    ];

    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (!HandledTypes.Contains(context.Type.Name)) return;

        var providers = registry.Value.Where(e => e.Value.Enabled).Select(e => e.Key).ToList();
        var models = registry.Value.SelectMany(e => e.Value.Models).Distinct().ToList();

        if (schema.Properties.TryGetValue("provider", out var providerProp))
        {
            providerProp.Enum = providers.Select(p => new OpenApiString(p)).Cast<IOpenApiAny>().ToList();
            providerProp.Default = new OpenApiString("GoogleAI");
        }

        if (schema.Properties.TryGetValue("model", out var modelProp))
        {
            modelProp.Enum = models.Select(m => new OpenApiString(m)).Cast<IOpenApiAny>().ToList();
            modelProp.Default = new OpenApiString("gemini-3.5-flash");
        }

        if (schema.Properties.TryGetValue("topK", out var topKProp))
            topKProp.Default = new OpenApiInteger(2);

        if (schema.Properties.TryGetValue("collectionName", out var collProp))
            collProp.Default = new OpenApiString("PDFs");

        if (schema.Properties.TryGetValue("groupName", out var groupProp))
            groupProp.Default = new OpenApiString("PDFs");
    }
}
