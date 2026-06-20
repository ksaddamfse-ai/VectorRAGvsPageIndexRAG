using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using VectorRAGvsPageIndexRAG.Settings;

namespace VectorRAGvsPageIndexRAG;

public class ProviderDropdownFilter(IOptions<Dictionary<string, ProviderRegistryEntry>> registry) : IParameterFilter
{
    public void Apply(OpenApiParameter parameter, ParameterFilterContext context)
    {
        if (parameter.Name is not ("provider" or "model")) return;

        var entry = registry.Value;

        if (parameter.Name == "provider")
        {
            var providers = entry.Where(e => e.Value.Enabled).Select(e => e.Key).ToList();
            parameter.Schema.Enum = providers.Select(k => new OpenApiString(k)).Cast<IOpenApiAny>().ToList();
        }
        else
        {
            var models = entry.Where(e => e.Value.Enabled)
                .SelectMany(e => e.Value.Models).Distinct().ToList();
            parameter.Schema.Enum = models.Select(m => new OpenApiString(m)).Cast<IOpenApiAny>().ToList();
        }
    }
}
