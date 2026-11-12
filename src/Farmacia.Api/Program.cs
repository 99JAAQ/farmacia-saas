using System.Text.Json.Serialization;
using Farmacia.Api.Authentication;
using Farmacia.Api.Development;
using Farmacia.Api.ErrorHandling;
using Farmacia.Api.Hosting;
using Farmacia.Api.OpenApi;
using Farmacia.Api.Realtime;
using Farmacia.Application;
using Farmacia.Application.Abstractions.Realtime;
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

// Real time (phase 4): thin "changed" notices to each pharmacy's open screens. Replaces the no-op publisher.
builder.Services.AddSignalR();
builder.Services.AddSingleton<IRealtimePublisher, SignalRRealtimePublisher>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapApiDocumentation();
    await DevelopmentSeeder.SeedAsync(app);
}
else if (!app.Environment.IsEnvironment("Testing"))
{
    // Production: TLS ends at Azure's ingress, which forwards the original scheme and client IP in X-Forwarded-*
    // headers (read because ASPNETCORE_FORWARDEDHEADERS_ENABLED=true, set in the Dockerfile).
    app.UseHsts();
    await ProductionBootstrap.RunAsync(app.Services, app.Configuration, app.Logger);
}

app.UseSecurityHeaders();
app.UseHttpsRedirection();

// The React build (wwwroot, copied by the Dockerfile): public files, served before authentication runs.
app.UseDefaultFiles(); // "/" serves index.html
app.UseStaticFiles();

// Explicit, and after the static files: otherwise minimal hosting adds routing at the very start, the SPA fallback
// matches /assets/app.js first, and the static files middleware skips any request that already has an endpoint
// (every script would come back as index.html).
app.UseRouting();

// Order matters: first find out who the caller is, then decide what they may do.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<PharmacyHub>(PharmacyHub.Path);
app.MapHealthChecks("/health").AllowAnonymous();
app.MapSpa();

app.Run();
