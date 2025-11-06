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

    public async Task SendNotificationAsync(NotificationRequest notification, string? contractAddress = null)
    {
        if (_botClient == null || _settings.ChannelId == 0)
        {
            _logger.LogWarning("Telegram not configured, skipping message send");
            return;
        }

        try
        {
            string message;

            if (!string.IsNullOrEmpty(contractAddress))
            {
                // Format message with contract address and links
                message = $@"{notification.Message}

📝 Contract: `{contractAddress}`
🔗 [DEXScreener](https://dexscreener.com/solana/{contractAddress})
🔗 [Buy on Jupiter](https://jup.ag/swap/SOL-{contractAddress})";
            }
            else
            {
                // Send without contract address if not found
                message = $@"{notification.Message}

⚠️ Contract address not found";
            }

            await _botClient.SendTextMessageAsync(
                chatId: _settings.ChannelId,
                text: message,
                parseMode: ParseMode.Markdown,
                disableWebPagePreview: false // Enable preview for DEXScreener/Jupiter links
            );

            _logger.LogInformation("✅ Sent to Telegram: {Message}", notification.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to send message to Telegram");
        }
    }

    public async Task SendTestMessageAsync(long chatId, string message)
    {
        if (_botClient == null)
        {
            throw new InvalidOperationException("Telegram bot not configured");
        }

        await _botClient.SendTextMessageAsync(
            chatId: chatId,
            text: message,
            parseMode: ParseMode.Markdown
        );

        _logger.LogInformation("✅ Test message sent to chat: {ChatId}", chatId);
    }

    public async Task<object> GetUpdatesAsync()
    {
        if (_botClient == null)
        {
            throw new InvalidOperationException("Telegram bot not configured");
        }

        var updates = await _botClient.GetUpdatesAsync();
        return updates.Select(u => new
        {
            updateId = u.Id,
            message = u.Message != null ? new
            {
                chatId = u.Message.Chat.Id,
                text = u.Message.Text,
                from = u.Message.From != null ? new
                {
                    id = u.Message.From.Id,
                    username = u.Message.From.Username,
                    firstName = u.Message.From.FirstName
                } : null
            } : null
        }).ToList();
    }
}
