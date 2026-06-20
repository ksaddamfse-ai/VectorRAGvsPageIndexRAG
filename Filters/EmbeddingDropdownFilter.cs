using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using VectorRAGvsPageIndexRAG.Settings;

namespace VectorRAGvsPageIndexRAG;

public class EmbeddingDropdownFilter(IOptions<ActiveEmbeddingOptions> options) : IParameterFilter
{
    public void Apply(OpenApiParameter parameter, ParameterFilterContext context)
    {
        if (parameter.Name is not ("embeddingProvider" or "embeddingModel")) return;

        if (parameter.Name == "embeddingProvider")
            parameter.Schema.Enum = [new OpenApiString(options.Value.ProviderKey)];
        else
            parameter.Schema.Enum = [new OpenApiString(options.Value.Model)];
    }
}
