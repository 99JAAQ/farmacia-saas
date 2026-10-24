using System.Text.Json.Serialization;
using Farmacia.Api.Authentication;
using Farmacia.Api.Development;
using Farmacia.Api.ErrorHandling;
using Farmacia.Api.OpenApi;
using Farmacia.Application;
using Farmacia.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddApplication(builder.Configuration["MediatR:LicenseKey"]);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddJwtAuthentication();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();

// Enums travel as text ("Credit", not 2): readable, typed as unions in TypeScript, safe if the enum is reordered.
// Two settings on purpose: controllers use the MVC JSON options, while the OpenAPI generator reads the
// minimal-API ones. Setting only the first would make the documentation say "integer" for a field that is text.
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddApiDocumentation();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapApiDocumentation();
    await DevelopmentSeeder.SeedAsync(app);
}

app.UseHttpsRedirection();

// Order matters: first find out who the caller is, then decide what they may do.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();
