using TelegramBot.Models;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace TelegramBot.Services;

public class TelegramService : ITelegramService
{
    private readonly TelegramSettings _settings;
    private readonly TelegramBotClient? _botClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TelegramService> _logger;

    public TelegramService(
        IOptions<TelegramSettings> settings,
        IServiceProvider serviceProvider,
        ILogger<TelegramService> logger)
    {
        _settings = settings.Value;
        _serviceProvider = serviceProvider;
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
        return _botClient != null;
    }

    public async Task SendNotificationToAllUsersAsync(NotificationRequest notification, string? contractAddress = null, string? traderHandle = null)
    {
        if (_botClient == null)
        {
            _logger.LogWarning("Telegram bot not configured, skipping message send");
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        var traderService = scope.ServiceProvider.GetRequiredService<ITraderService>();

        List<Models.User> users;

        // CRITICAL: Filter by trader followers if trader handle provided - O(log n)
        if (!string.IsNullOrEmpty(traderHandle))
        {
            var followerUserIds = await traderService.GetFollowerUserIdsForTraderHandleAsync(traderHandle);

            if (followerUserIds.Count == 0)
            {
                _logger.LogInformation("No users following trader {Trader}, skipping notification", traderHandle);
                return;
            }

            // Get only active users who follow this trader
            var allUsers = await userService.GetAllActiveUsersAsync();
            users = allUsers.Where(u => followerUserIds.Contains(u.Id)).ToList();

            _logger.LogInformation("Filtered to {Count} users following trader {Trader}", users.Count, traderHandle);
        }
        else
        {
            // No trader specified - send to all active users (backward compatible)
            users = await userService.GetAllActiveUsersAsync();
        }

        if (users.Count == 0)
        {
            _logger.LogWarning("No active users to send notification to");
            return;
        }

        string message;
        if (!string.IsNullOrEmpty(contractAddress))
        {
            message = $@"{notification.Message}

📝 Contract: `{contractAddress}`
🔗 [DEXScreener](https://dexscreener.com/solana/{contractAddress})";
        }
        else
        {
            message = $@"{notification.Message}";
        }

        int successCount = 0;
        int failCount = 0;

        foreach (var user in users)
        {
            try
            {
                await _botClient.SendTextMessageAsync(
                    chatId: user.ChatId,
                    text: message,
                    parseMode: ParseMode.Markdown,
                    disableWebPagePreview: true
                );

                successCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send message to user {ChatId}", user.ChatId);
                failCount++;
            }
        }

        _logger.LogInformation("✅ Notification sent to {Success}/{Total} users ({Failed} failed)",
            successCount, users.Count, failCount);
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
