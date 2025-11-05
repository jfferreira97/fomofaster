using TelegramBot.Models;
using TelegramBot.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

// Register Telegram service
builder.Services.AddSingleton<ITelegramService, TelegramService>();

// Configure settings from appsettings.json or environment variables
builder.Services.Configure<TelegramSettings>(
    builder.Configuration.GetSection("Telegram"));

var app = builder.Build();

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

app.Run();
