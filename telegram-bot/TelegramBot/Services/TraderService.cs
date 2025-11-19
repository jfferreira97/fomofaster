using Microsoft.EntityFrameworkCore;
using TelegramBot.Data;
using TelegramBot.Models;

namespace TelegramBot.Services;

public class TraderService : ITraderService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<TraderService> _logger;

    public TraderService(AppDbContext dbContext, ILogger<TraderService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
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
        return trader;
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
