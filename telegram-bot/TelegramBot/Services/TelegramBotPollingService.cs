using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBot.Models;

namespace TelegramBot.Services;

public class TelegramBotPollingService : BackgroundService
{
    private readonly TelegramBotClient? _botClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TelegramBotPollingService> _logger;
    private int _offset = 0;

    public TelegramBotPollingService(
        IOptions<TelegramSettings> settings,
        IServiceProvider serviceProvider,
        ILogger<TelegramBotPollingService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        if (!string.IsNullOrEmpty(settings.Value.BotToken))
        {
            _botClient = new TelegramBotClient(settings.Value.BotToken);
            _logger.LogInformation("Telegram polling service initialized");
        }
        else
        {
            _logger.LogWarning("Bot token not configured, polling service will not start");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_botClient == null)
        {
            _logger.LogWarning("Bot client not initialized, polling service stopped");
            return;
        }

        _logger.LogInformation("Starting Telegram bot polling...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var updates = await _botClient.GetUpdatesAsync(
                    offset: _offset,
                    timeout: 30,
                    cancellationToken: stoppingToken
                );

                foreach (var update in updates)
                {
                    _offset = update.Id + 1;
                    await HandleUpdateAsync(update);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Polling cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during polling");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("Telegram bot polling stopped");
    }

    private async Task HandleUpdateAsync(Update update)
    {
        try
        {
            if (update.Message is { } message)
            {
                await HandleMessageAsync(message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling update {UpdateId}", update.Id);
        }
    }

    private async Task HandleMessageAsync(Message message)
    {
        var chatId = message.Chat.Id;
        var text = message.Text?.Trim();

        _logger.LogInformation("Received message from {ChatId}: {Text}", chatId, text);

        if (string.IsNullOrEmpty(text))
            return;

        using var scope = _serviceProvider.CreateScope();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();

        // Handle commands
        if (text.StartsWith("/"))
        {
            await HandleCommandAsync(message, userService);
        }
    }

    private async Task HandleCommandAsync(Message message, IUserService userService)
    {
        if (_botClient == null)
            return;

        var chatId = message.Chat.Id;
        var command = message.Text?.Split(' ')[0].ToLower();

        switch (command)
        {
            case "/start":
                await userService.AddOrUpdateUserAsync(
                    chatId,
                    message.From?.Username,
                    message.From?.FirstName
                );

                await _botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: @"🎉 Welcome to FOMOFAST!

You'll now receive real-time notifications when top Solana traders make moves.

Commands:
/help - Show available commands
/mytraders - Show traders you're following (coming soon)
/list - Show all available traders (coming soon)

Stay ahead of the curve! 🚀",
                    parseMode: ParseMode.Markdown
                );

                _logger.LogInformation("User started bot: ChatId={ChatId}, Username={Username}",
                    chatId, message.From?.Username);
                break;

            case "/help":
                await _botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: @"📚 FOMOFAST Commands:

/start - Subscribe to notifications
/help - Show this help message
/mytraders - View your followed traders (coming soon)
/list - View all available traders (coming soon)
/follow @trader - Follow a trader (coming soon)
/unfollow @trader - Unfollow a trader (coming soon)

Currently, you'll receive all notifications. Trader filtering will be added soon!",
                    parseMode: ParseMode.Markdown
                );
                break;

            case "/mytraders":
            case "/list":
            case "/follow":
            case "/unfollow":
                await _botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "⚠️ This feature is coming soon! For now, you'll receive all trading notifications.",
                    parseMode: ParseMode.Markdown
                );
                break;

            default:
                await _botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "❓ Unknown command. Use /help to see available commands.",
                    parseMode: ParseMode.Markdown
                );
                break;
        }
    }
}
