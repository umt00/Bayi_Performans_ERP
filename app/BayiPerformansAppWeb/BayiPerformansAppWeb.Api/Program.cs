using BayiPerformansAppWeb.Api.Authentication;
using BayiPerformansAppWeb.Api.Configuration;
using BayiPerformansAppWeb.Api.Integration;
using BayiPerformansAppWeb.Business;
using BayiPerformansAppWeb.DataAccess;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

if (int.TryParse(Environment.GetEnvironmentVariable("PORT"), out int port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Configuration.AddJsonFile(
    Path.Combine(
        AppContext.BaseDirectory,
        "Configuration",
        "thresholds.json"),
    optional: false,
    reloadOnChange: false);

builder.Services.AddApiLayer(builder.Configuration);
builder.Services.AddSupabaseAuthentication(builder.Configuration);
builder.Services.AddPentaDataAccess(builder.Configuration);
builder.Services.AddElasticsearchInfrastructure(builder.Configuration);
builder.Services.AddOperationalHealthChecks();
builder.Services.AddBayiPerformansBusiness(builder.Configuration);
builder.Services.AddBusinessIntegrationFallbacks();

if (builder.Configuration.GetValue<bool>("BackgroundJobs:ScoreCalculationEnabled"))
{
    builder.Services.AddHostedService<
        BayiPerformansAppWeb.Api.BackgroundServices.ScoreCalculationBackgroundService>();
}

var app = builder.Build();

if (app.Environment.IsDevelopment()
    || builder.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHttpsRedirection();
app.UseRouting();
app.UseCors(ApiCorsOptions.PolicyName);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks(
        "/health",
        new HealthCheckOptions
        {
            Predicate = _ => false
        })
    .AllowAnonymous();
app.MapHealthChecks(
        "/health/ready",
        new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("ready")
        })
    .AllowAnonymous();

app.Run();

public partial class Program;
