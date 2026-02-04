var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

// Serve static files from wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

// Route /tv to tv.html
app.MapGet("/tv", async context =>
{
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync(Path.Combine(app.Environment.WebRootPath, "tv.html"));
});

// Route /join/{code} to join.html
app.MapGet("/join/{code?}", async context =>
{
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync(Path.Combine(app.Environment.WebRootPath, "join.html"));
});

// Fallback route for SPA - serves index.html for any unmatched routes
app.MapFallbackToFile("index.html");

app.Run();
