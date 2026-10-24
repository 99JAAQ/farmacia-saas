using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace Farmacia.Api.OpenApi;

internal static class OpenApiExtensions
{
    private const string BearerScheme = "Bearer";

    public static IServiceCollection AddApiDocumentation(this IServiceCollection services) =>
        services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
        {
            // Declares the JWT scheme so the API reference shows where to paste the access token,
            // and marks every operation as requiring it (the API's fallback policy does the same).
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes[BearerScheme] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Access token de POST /api/auth/login (dura 15 minutos).",
            };

            document.Security =
            [
                new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(BearerScheme, document)] = [] },
            ];

            return Task.CompletedTask;
        }));

    /// <summary>Development only: the OpenAPI document and the Scalar API reference at /scalar.</summary>
    public static WebApplication MapApiDocumentation(this WebApplication app)
    {
        app.MapOpenApi().AllowAnonymous();
        app.MapScalarApiReference(options => options
                .WithTitle("Farmacia API")
                .AddPreferredSecuritySchemes(BearerScheme))
            .AllowAnonymous();

        return app;
    }
}
