using Microsoft.Extensions.Options;
using PartyGame.Core.Interfaces;
using PartyGame.Core.Services;
using PartyGame.Server.Hubs;
using PartyGame.Server.Options;
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

// Add CORS for development (when Web runs separately or on LAN)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        // In development, allow any origin for LAN testing
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Add SignalR
builder.Services.AddSignalR();

// Configure room cleanup options
builder.Services.Configure<RoomCleanupOptions>(
    builder.Configuration.GetSection(RoomCleanupOptions.SectionName));

// Configure autoplay options
builder.Services.Configure<AutoplayOptions>(
    builder.Configuration.GetSection(AutoplayOptions.SectionName));

// Add game services
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IRoomCodeGenerator, RoomCodeGenerator>();
builder.Services.AddSingleton<IRoomStore, InMemoryRoomStore>();
builder.Services.AddSingleton<IConnectionIndex, ConnectionIndex>();

// Add quiz question bank
builder.Services.AddSingleton<IQuizQuestionBank, JsonQuizQuestionBank>();

// Add dictionary question provider
builder.Services.AddSingleton<IDictionaryQuestionProvider, DictionaryQuestionProvider>();

// Add ranking stars prompt provider
builder.Services.AddSingleton<IRankingStarsPromptProvider, RankingStarsPromptProvider>();

// Add quiz game engine and orchestrator
builder.Services.AddSingleton<IQuizGameEngine, QuizGameEngine>();
builder.Services.AddSingleton<IQuizGameOrchestrator, QuizGameOrchestrator>();

// Add lobby service (depends on orchestrator)
builder.Services.AddSingleton<ILobbyService, LobbyService>();
builder.Services.AddSingleton<IAutoplayService, AutoplayService>();

// Add hosted services
builder.Services.AddHostedService<RoomCleanupHostedService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Use CORS before other middleware
app.UseCors();

// Only use HTTPS redirection in production
// In development/LAN mode, we use HTTP to avoid certificate issues
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();

app.MapControllers();

// Map SignalR hub
app.MapHub<GameHub>("/hub/game");

// Health endpoint
app.MapGet("/health", (IHostEnvironment env, IOptions<AutoplayOptions> options) =>
        Results.Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            environment = env.EnvironmentName,
            autoplayEnabled = options.Value.Enabled
        }))
   .WithName("Health");

app.Run();

// Required for integration tests with WebApplicationFactory
public partial class Program { }
