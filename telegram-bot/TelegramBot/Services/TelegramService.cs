using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TelegramBot.Data;
using TelegramBot.Hubs;
using TelegramBot.Models;

namespace TelegramBot.Services;

public class TelegramService : ITelegramService
{
    private readonly TelegramSettings _settings;
    private readonly TelegramBotClient? _botClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TelegramService> _logger;
    private readonly IHubContext<DashboardHub> _hubContext;

    public TelegramService(
        IOptions<TelegramSettings> settings,
        IServiceProvider serviceProvider,
        ILogger<TelegramService> logger,
        IHubContext<DashboardHub> hubContext)
    {
        _settings = settings.Value;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _hubContext = hubContext;

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

    public async Task SendNotificationToAllUsersAsync(NotificationRequest notification, string? contractAddress = null, string? traderHandle = null, string? ticker = null, Chain? chain = null)
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

        // Replace @traderHandle with Twitter link to avoid Telegram interpreting it as a Telegram user
        var processedMessage = notification.Message;
        if (!string.IsNullOrEmpty(traderHandle))
        {
            processedMessage = processedMessage.Replace(
                $"@{traderHandle}",
                $"[{traderHandle}](https://x.com/{traderHandle})"
            );
        }

        string message;
        if (!string.IsNullOrEmpty(contractAddress))
        {
            // Get chain-specific DEXScreener URL (defaults to Solana if no chain specified)
            string dexScreenerUrl = (chain ?? Chain.SOL) switch
            {
                Chain.SOL => $"https://dexscreener.com/solana/{contractAddress}",
                Chain.BNB => $"https://dexscreener.com/bsc/{contractAddress}",
                Chain.BASE => $"https://dexscreener.com/base/{contractAddress}",
                _ => $"https://dexscreener.com/solana/{contractAddress}"
            };

            message = $@"{processedMessage}

📝 Contract: `{contractAddress}`
🔗 [DEXScreener]({dexScreenerUrl})";
        }
        else
        {
            message = $@"{processedMessage}";
        }

        int successCount = 0;
        int failCount = 0;

        // Create Notification record in database
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notificationRecord = new Models.Notification
        {
            Message = notification.Message,
            Ticker = ticker,
            Trader = traderHandle,
            HasCA = !string.IsNullOrEmpty(contractAddress),
            ContractAddress = contractAddress,
            Chain = chain,
            SentAt = DateTime.UtcNow
        };
        dbContext.Notifications.Add(notificationRecord);
        await dbContext.SaveChangesAsync();

        // Send messages and track MessageIds
        foreach (var user in users)
        {
            try
            {
                var sentMessage = await _botClient.SendTextMessageAsync(
                    chatId: user.ChatId,
                    text: message,
                    parseMode: ParseMode.Markdown,
                    disableWebPagePreview: true
                );

                // Save SentMessage record
                var sentMessageRecord = new Models.SentMessage
                {
                    NotificationId = notificationRecord.Id,
                    ChatId = user.ChatId,
                    MessageId = sentMessage.MessageId,
                    SentAt = DateTime.UtcNow,
                    IsManuallyEdited = false,
                    IsSystemEdited = false,
                    EditedAt = null
                };
                dbContext.SentMessages.Add(sentMessageRecord);

                successCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send message to user {ChatId}", user.ChatId);
                failCount++;
            }
        }

        // Save all SentMessage records in one batch
        await dbContext.SaveChangesAsync();

        // Get total active users for dashboard metadata
        var totalActiveUsers = await userService.GetAllActiveUsersAsync();

        // Broadcast notification to dashboard via SignalR
        await _hubContext.Clients.All.SendAsync("ReceiveNotification", new
        {
            id = notificationRecord.Id,
            message = notificationRecord.Message,
            ticker = notificationRecord.Ticker,
            trader = notificationRecord.Trader,
            hasCA = notificationRecord.HasCA,
            contractAddress = notificationRecord.ContractAddress,
            chain = notificationRecord.Chain?.ToString(),
            sentAt = notificationRecord.SentAt,
            recipientCount = successCount,
            totalUsers = totalActiveUsers.Count,
            isManuallyEdited = false,
            isSystemEdited = false,
            editedAt = (DateTime?)null
        });

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

    public async Task EditNotificationMessagesAsync(int notificationId, string contractAddress, Chain chain)
    {
        if (_botClient == null)
        {
            throw new InvalidOperationException("Telegram bot not configured");
        }

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();

        // Get notification
        var notification = await dbContext.Notifications.FindAsync(notificationId);
        if (notification == null)
        {
            throw new InvalidOperationException($"Notification {notificationId} not found");
        }

        // Get all sent messages for this notification
        var sentMessages = await dbContext.SentMessages.Where(sm => sm.NotificationId == notificationId).ToListAsync();

        if (sentMessages.Count == 0)
        {
            _logger.LogWarning("No sent messages found for notification {NotificationId}", notificationId);
            return;
        }

        // Format trader handle as clickable link if present
        var processedMessage = notification.Message;
        if (!string.IsNullOrEmpty(notification.Trader))
        {
            processedMessage = processedMessage.Replace(
                $"@{notification.Trader}",
                $"[{notification.Trader}](https://x.com/{notification.Trader})"
            );
        }

        // Get chain-specific DEXScreener URL
        string dexScreenerUrl = chain switch
        {
            Chain.SOL => $"https://dexscreener.com/solana/{contractAddress}",
            Chain.BNB => $"https://dexscreener.com/bsc/{contractAddress}",
            Chain.BASE => $"https://dexscreener.com/base/{contractAddress}",
            _ => $"https://dexscreener.com/solana/{contractAddress}"
        };

        // Build new message with CA
        var newMessage = $@"{processedMessage}

📝 Contract: `{contractAddress}`
🔗 [DEXScreener]({dexScreenerUrl})";

        int successCount = 0;
        int failCount = 0;

        // Edit all messages
        foreach (var sentMessage in sentMessages)
        {
            try
            {
                await _botClient.EditMessageTextAsync(
                    chatId: sentMessage.ChatId,
                    messageId: sentMessage.MessageId,
                    text: newMessage,
                    parseMode: ParseMode.Markdown,
                    disableWebPagePreview: true
                );

                // Update SentMessage record (manual edit via API)
                sentMessage.IsManuallyEdited = true;
                sentMessage.EditedAt = DateTime.UtcNow;
                successCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to edit message {MessageId} for chat {ChatId}",
                    sentMessage.MessageId, sentMessage.ChatId);
                failCount++;
            }
        }

        // Update notification record
        notification.HasCA = true;
        notification.ContractAddress = contractAddress;
        notification.Chain = chain;

        // Save all changes
        await dbContext.SaveChangesAsync();

        // Get total active users for dashboard
        var totalActiveUsers = await userService.GetAllActiveUsersAsync();

        // Broadcast update to dashboard via SignalR
        await _hubContext.Clients.All.SendAsync("NotificationUpdated", new
        {
            id = notification.Id,
            message = notification.Message,
            ticker = notification.Ticker,
            trader = notification.Trader,
            hasCA = notification.HasCA,
            contractAddress = notification.ContractAddress,
            chain = notification.Chain?.ToString(),
            sentAt = notification.SentAt,
            recipientCount = successCount,
            totalUsers = totalActiveUsers.Count,
            isManuallyEdited = true,
            isSystemEdited = false,
            editedAt = DateTime.UtcNow
        });

        _logger.LogInformation("✅ Edited {Success}/{Total} messages for notification {NotificationId} ({Failed} failed)",
            successCount, sentMessages.Count, notificationId, failCount);
    }
}
