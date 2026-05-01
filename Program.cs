using Microsoft.EntityFrameworkCore;
using QuizGameShow.Data;
using QuizGameShow.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Render.com passes the port via the PORT environment variable.
// Locally falls back to 5100.
var port = Environment.GetEnvironmentVariable("PORT") ?? "5100";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// ─── Services ─────────────────────────────────────────────────────────────────

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        // Serialize enums as strings (e.g., "Lobby" instead of 0)
        opts.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// SQLite database via Entity Framework Core
builder.Services.AddDbContext<QuizDbContext>(opts =>
    opts.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
                   ?? "Data Source=quiz.db"));

// SignalR — real-time hub
builder.Services.AddSignalR(opts =>
{
    opts.EnableDetailedErrors = builder.Environment.IsDevelopment();
    opts.MaximumReceiveMessageSize = 32 * 1024; // 32 KB
});

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Quiz Game Show API",
        Version = "v1",
        Description = "REST API + SignalR Hub for the Quiz Game Show (Kahoot-like) application."
    });
});

// CORS — allow any origin in development (tighten for production)
builder.Services.AddCors(opts =>
    opts.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader()));

// ─── App pipeline ──────────────────────────────────────────────────────────────

var app = builder.Build();

// Auto-create / migrate database on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<QuizDbContext>();
    await dbContext.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Quiz Game Show API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseCors();

// IMPORTANT: UseDefaultFiles() MUST come before UseStaticFiles()
// It rewrites "/" → "/index.html" before the static file middleware reads the file.
app.UseDefaultFiles();   // "/" → "/index.html"
app.UseStaticFiles();    // serve wwwroot files

app.UseRouting();
app.UseAuthorization();

app.MapControllers();

// SignalR hub endpoint
app.MapHub<QuizHub>("/hubs/quiz");

// SPA fallback — any unmatched route also serves index.html
app.MapFallbackToFile("index.html");

app.Run();
