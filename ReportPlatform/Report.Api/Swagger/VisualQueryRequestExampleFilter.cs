using Microsoft.OpenApi;
using Report.Contracts.Requests;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text.Json.Nodes;

namespace Report.Api.Swagger;

public sealed class VisualQueryRequestExampleFilter : IRequestBodyFilter
{
    public void Apply(IOpenApiRequestBody requestBody, RequestBodyFilterContext context)
    {
        if (context.BodyParameterDescription?.Type != typeof(VisualQueryRequest))
        {
            return;
        }

        if (requestBody.Content is null ||
            !requestBody.Content.TryGetValue("application/json", out var mediaType))
        {
            return;
        }

        mediaType.Example = new JsonObject
        {
            ["connectionId"] = "conn_001",
            ["datasetId"] = "sales",
            ["reportId"] = "rpt_001",
            ["visualType"] = "table",
            ["rows"] = new JsonArray("customer.name", "date.year"),
            ["columns"] = new JsonArray(),
            ["values"] = new JsonArray("total_sales"),
            ["filters"] = new JsonArray(),
            ["sort"] = new JsonArray
            {
                new JsonObject
                {
                    ["field"] = "TotalSales",
                    ["direction"] = "DESC"
                }
            },
            ["limit"] = 100,
            ["offset"] = 0
        };
    }
}
