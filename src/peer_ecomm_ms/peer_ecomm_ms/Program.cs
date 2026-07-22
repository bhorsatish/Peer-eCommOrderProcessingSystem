using eComm_ms.DBA;
using eComm_ms.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: "AllowAll",
                      policy =>
                      {
                          policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
                      });
});

builder.Services.AddDbContext<ECommDbContext>(options =>
    options.UseSqlite("Data Source=\"H:\\eCommOrderProcessing\\src\\peer_database\\eCommDB.db\""));
builder.Services.AddControllers();
builder.Services.AddScoped<OrderDetailsService>();
builder.Services.AddOpenApi();

// Register the background service
builder.Services.AddHostedService<OrderStatusUpdateService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "eComm_ms API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.UseCors("AllowAll");
app.UseStaticFiles();
app.MapControllers();
app.Run();

// Exposes the top-level-statement entry point as a public type so
// WebApplicationFactory<Program> can boot the real app in integration tests.
public partial class Program { }