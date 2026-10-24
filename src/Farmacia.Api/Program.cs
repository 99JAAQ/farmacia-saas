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

builder.Services.AddControllers();
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
