using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TelegramBot.Data;
using TelegramBot.Models;

namespace TelegramBot.Services;

public class TraderService : ITraderService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<TraderService> _logger;
    private readonly TelegramBotClient? _botClient;
    private readonly IServiceProvider _serviceProvider;

    public TraderService(
        AppDbContext dbContext,
        ILogger<TraderService> logger,
        IOptions<TelegramSettings> settings,
        IServiceProvider serviceProvider)
    {
        _dbContext = dbContext;
        _logger = logger;
        _serviceProvider = serviceProvider;

        if (!string.IsNullOrEmpty(settings.Value.BotToken))
        {
            _botClient = new TelegramBotClient(settings.Value.BotToken);
        }
    }

    public async Task<Trader?> GetTraderByHandleAsync(string handle)
    {
        return await _dbContext.Traders.FirstOrDefaultAsync(t => t.Handle == handle);
    }

    public async Task<Trader?> GetTraderByIdAsync(int traderId)
    {
        return await _dbContext.Traders.FindAsync(traderId);
    }

    public async Task<List<Trader>> GetAllTradersAsync()
    {
        return await _dbContext.Traders.OrderBy(t => t.Id).ToListAsync();
    }

    public async Task<List<Trader>> GetTradersByUserIdAsync(int userId)
    {
        return await _dbContext.UserTraders
            .Where(ut => ut.UserId == userId)
            .Include(ut => ut.Trader)
            .Select(ut => ut.Trader)
            .OrderBy(t => t.Id)
            .ToListAsync();
    }

    public async Task<Trader> AddOrUpdateTraderAsync(string handle)
    {
        var trader = await GetTraderByHandleAsync(handle);
        var isNewTrader = trader == null;

        if (trader == null)
        {
            trader = new Trader
            {
                Handle = handle,
                FirstSeenAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow
            };

            _dbContext.Traders.Add(trader);
            _logger.LogInformation("New trader added: Handle={Handle}", handle);
        }
        else
        {
            trader.LastSeenAt = DateTime.UtcNow;
            _logger.LogInformation("Trader updated: Handle={Handle}", handle);
        }

        await _dbContext.SaveChangesAsync();

        // Broadcast message to all users if this is a new trader
        if (isNewTrader && _botClient != null)
        {
            await BroadcastNewTraderMessageAsync(trader);
        }

        return trader;
    }

    private async Task BroadcastNewTraderMessageAsync(Trader trader)
    {
        if (_botClient == null)
            return;

        // Get all active users
        var activeUsers = await _dbContext.Users
            .Where(u => u.IsActive)
            .ToListAsync();

        _logger.LogInformation("Broadcasting new trader {Handle} to {Count} active users", trader.Handle, activeUsers.Count);

        foreach (var user in activeUsers)
        {
            try
            {
                string message;

                if (user.AutoFollowNewTraders)
                {
                    // Auto-follow the user to this trader
                    await FollowTraderAsync(user.Id, trader.Id);

                    message = $@"🎯 A new sharp FOMO APP trader, [{trader.Handle}](https://x.com/{trader.Handle}), was just added to our services!

✅ This trader's trades will be tracked by you since you have auto-follow ON.

Use /unfollow {trader.Handle} or /unfollow {trader.Id} if you do not desire this trader.
Use /autofollow off if you want to opt out completely of auto-following new traders.";
                }
                else
                {
                    message = $@"🎯 A new sharp FOMO APP trader, [{trader.Handle}](https://x.com/{trader.Handle}), was just added to our services!

ℹ️ You are NOT following this trader since you have auto-follow OFF.

Use /follow {trader.Handle} or /follow {trader.Id} if you want to follow them.
Use /autofollow on if you want to opt in to auto-following new traders.";
                }

                await _botClient.SendTextMessageAsync(
                    chatId: user.ChatId,
                    text: message,
                    parseMode: ParseMode.Markdown,
                    disableWebPagePreview: true
                );

                _logger.LogInformation("Sent new trader notification to user {ChatId}", user.ChatId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send new trader notification to user {ChatId}", user.ChatId);
            }
        }
    }

    public async Task<bool> FollowTraderAsync(int userId, int traderId)
    {
        // Check if already following - O(log n) thanks to composite index
        var existing = await _dbContext.UserTraders
            .FirstOrDefaultAsync(ut => ut.UserId == userId && ut.TraderId == traderId);

        if (existing != null)
            return false; // Already following

        var userTrader = new UserTrader
        {
            UserId = userId,
            TraderId = traderId,
            FollowedAt = DateTime.UtcNow
        };

        _dbContext.UserTraders.Add(userTrader);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("User {UserId} now following trader {TraderId}", userId, traderId);
        return true;
    }

    public async Task<bool> FollowTraderByHandleAsync(int userId, string handle)
    {
        var trader = await GetTraderByHandleAsync(handle);
        if (trader == null)
            return false;

        return await FollowTraderAsync(userId, trader.Id);
    }

    public async Task<bool> UnfollowTraderAsync(int userId, int traderId)
    {
        var userTrader = await _dbContext.UserTraders
            .FirstOrDefaultAsync(ut => ut.UserId == userId && ut.TraderId == traderId);

        if (userTrader == null)
            return false; // Not following

        _dbContext.UserTraders.Remove(userTrader);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("User {UserId} unfollowed trader {TraderId}", userId, traderId);
        return true;
    }

    public async Task<bool> UnfollowTraderByHandleAsync(int userId, string handle)
    {
        var trader = await GetTraderByHandleAsync(handle);
        if (trader == null)
            return false;

        return await UnfollowTraderAsync(userId, trader.Id);
    }

    public async Task<bool> IsFollowingAsync(int userId, int traderId)
    {
        // O(log n) lookup thanks to composite index
        return await _dbContext.UserTraders
            .AnyAsync(ut => ut.UserId == userId && ut.TraderId == traderId);
    }

    // CRITICAL FOR NOTIFICATION FILTERING - O(log n) thanks to TraderId index
    public async Task<List<int>> GetFollowerUserIdsForTraderAsync(int traderId)
    {
        return await _dbContext.UserTraders
            .Where(ut => ut.TraderId == traderId)
            .Select(ut => ut.UserId)
            .ToListAsync();
    }

    public async Task<List<int>> GetFollowerUserIdsForTraderHandleAsync(string handle)
    {
        var trader = await GetTraderByHandleAsync(handle);
        if (trader == null)
            return new List<int>();

        return await GetFollowerUserIdsForTraderAsync(trader.Id);
    }

    public async Task<int> FollowAllTradersAsync(int userId)
    {
        var allTraders = await GetAllTradersAsync();
        var followedCount = 0;

        foreach (var trader in allTraders)
        {
            var success = await FollowTraderAsync(userId, trader.Id);
            if (success)
                followedCount++;
        }

        // Update AutoFollowNewTraders flag
        var user = await _dbContext.Users.FindAsync(userId);
        if (user != null)
        {
            user.AutoFollowNewTraders = true;
            await _dbContext.SaveChangesAsync();
        }

        _logger.LogInformation("User {UserId} followed {Count} traders (all)", userId, followedCount);
        return followedCount;
    }

    public async Task<int> UnfollowAllTradersAsync(int userId)
    {
        var followedTraders = await GetTradersByUserIdAsync(userId);
        var unfollowedCount = 0;

        foreach (var trader in followedTraders)
        {
            var success = await UnfollowTraderAsync(userId, trader.Id);
            if (success)
                unfollowedCount++;
        }

        // Update AutoFollowNewTraders flag
        var user = await _dbContext.Users.FindAsync(userId);
        if (user != null)
        {
            user.AutoFollowNewTraders = false;
            await _dbContext.SaveChangesAsync();
        }

        _logger.LogInformation("User {UserId} unfollowed {Count} traders (all)", userId, unfollowedCount);
        return unfollowedCount;
    }

    public async Task<bool> DeleteTraderAsync(int traderId)
    {
        var trader = await GetTraderByIdAsync(traderId);
        if (trader == null)
            return false;

        _dbContext.Traders.Remove(trader);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Deleted trader {TraderId} ({Handle})", traderId, trader.Handle);
        return true;
    }

    public async Task<bool> DeleteTraderByHandleAsync(string handle)
    {
        var trader = await GetTraderByHandleAsync(handle);
        if (trader == null)
            return false;

        return await DeleteTraderAsync(trader.Id);
    }
}
