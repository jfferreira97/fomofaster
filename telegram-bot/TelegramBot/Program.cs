using Microsoft.EntityFrameworkCore;
using TelegramBot.Data;
using TelegramBot.Models;
using TelegramBot.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=fomofaster.db"));

// Configure CORS for development
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
});

// Register services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddSingleton<ITelegramService, TelegramService>();
builder.Services.AddSingleton<ISolanaService, SolanaService>();
builder.Services.AddHostedService<TelegramBotPollingService>(); // Background polling service
builder.Services.AddHttpClient(); // For Helius API calls

// Configure settings from appsettings.json or environment variables
builder.Services.Configure<TelegramSettings>(
    builder.Configuration.GetSection("Telegram"));
builder.Services.Configure<HeliusSettings>(
    builder.Configuration.GetSection("Helius"));

var app = builder.Build();

// Create database if it doesn't exist
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.EnsureCreated();
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.MapGet("/", () => new { message = "FomoFaster Backend Running", version = "1.0.0" });

app.MapGet("/health", (ITelegramService telegramService) =>
{
    return new
    {
        status = "healthy",
        telegramConfigured = telegramService.IsConfigured()
    };
});

app.MapGet("/bot-info", async (ITelegramService telegramService) =>
{
    try
    {
        var updates = await telegramService.GetUpdatesAsync();
        return Results.Ok(new { status = "success", updates });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { status = "error", message = ex.Message });
    }
});

app.MapPost("/test-bot", async (ITelegramService telegramService, long chatId) =>
{
    try
    {
        await telegramService.SendTestMessageAsync(chatId, "🎉 Bot is working! This is a test message from FOMOFAST.");
        return Results.Ok(new { status = "success", message = "Test message sent" });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { status = "error", message = ex.Message });
    }
});

app.Run();
