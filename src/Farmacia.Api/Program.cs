using Farmacia.Api.Development;
using Farmacia.Api.ErrorHandling;
using Farmacia.Application;
using Farmacia.Application.Abstractions;
using Farmacia.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddApplication(builder.Configuration["MediatR:LicenseKey"]);
builder.Services.AddInfrastructure(builder.Configuration);

// Until JWT auth exists, ICurrentPharmacy is only available in Development:
// in any other environment the use cases fail loudly instead of acting on a wrong tenant.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<DevelopmentCurrentPharmacy>();
    builder.Services.AddSingleton<ICurrentPharmacy>(sp => sp.GetRequiredService<DevelopmentCurrentPharmacy>());
}

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    await DevelopmentSeeder.SeedAsync(app);
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
