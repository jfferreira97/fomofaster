using TelegramBot.Models;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace TelegramBot.Services;

public class TelegramService : ITelegramService
{
    private readonly TelegramSettings _settings;
    private readonly TelegramBotClient? _botClient;
    private readonly ILogger<TelegramService> _logger;

    public TelegramService(IOptions<TelegramSettings> settings, ILogger<TelegramService> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        if (!string.IsNullOrEmpty(_settings.BotToken))
        {
            try
            {
                _botClient = new TelegramBotClient(_settings.BotToken);
                _logger.LogInformation("Telegram bot client initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Telegram bot client");
            }
        }
        else
        {
            _logger.LogWarning("Telegram bot token not configured");
        }
    }

    public bool IsConfigured()
    {
        return _botClient != null && _settings.ChannelId != 0;
    }

    public async Task SendNotificationAsync(NotificationRequest notification)
    {
        if (_botClient == null || _settings.ChannelId == 0)
        {
            _logger.LogWarning("Telegram not configured, skipping message send");
            return;
        }

        try
        {
            // Format message with contract address link
            var message = $@"{notification.Message}

📝 Contract: `{notification.ContractAddress}`
";

//🔗 [DEXScreener](https://dexscreener.com/solana/{notification.ContractAddress})
//🔗 [Buy on Jupiter](https://jup.ag/swap/SOL-{notification.ContractAddress})";

            await _botClient.SendTextMessageAsync(
                chatId: _settings.ChannelId,
                text: message,
                parseMode: ParseMode.Markdown,
                disableWebPagePreview: true
            );

            _logger.LogInformation("✅ Sent to Telegram: {Message}", notification.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to send message to Telegram");
        }
    }
}
