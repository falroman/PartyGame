using PartyGame.Core.Interfaces;
using PartyGame.Core.Services;
using PartyGame.Server.Hubs;
using PartyGame.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "PartyGame API",
        Version = "v1",
        Description = "REST API for PartyGame platform"
    });
});

// Add CORS for development (when Web runs separately)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                // Visual Studio HTTPS ports
                "https://localhost:7147",
                "http://localhost:5041",
                // dotnet run ports
                "https://localhost:5002",
                "http://localhost:5002",
                // IIS Express ports
                "https://localhost:44328",
                "http://localhost:22775"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Add SignalR
builder.Services.AddSignalR();

// Add game services
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IRoomCodeGenerator, RoomCodeGenerator>();
builder.Services.AddSingleton<IRoomStore, InMemoryRoomStore>();
builder.Services.AddSingleton<IConnectionIndex, ConnectionIndex>();
builder.Services.AddSingleton<ILobbyService, LobbyService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Use CORS before other middleware
app.UseCors();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// Map SignalR hub
app.MapHub<GameHub>("/hub/game");

// Health endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
   .WithName("Health");

app.Run();

// Required for integration tests with WebApplicationFactory
public partial class Program { }
