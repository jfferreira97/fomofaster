using TelegramBot.Models;

namespace TelegramBot.Services;

public interface ITraderService
{
    Task<Trader?> GetTraderByHandleAsync(string handle);
    Task<Trader?> GetTraderByIdAsync(int traderId);
    Task<List<Trader>> GetAllTradersAsync();
    Task<List<Trader>> GetTradersByUserIdAsync(int userId);
    Task<Trader> AddOrUpdateTraderAsync(string handle);
    Task<bool> FollowTraderAsync(int userId, int traderId);
    Task<bool> FollowTraderByHandleAsync(int userId, string handle);
    Task<bool> UnfollowTraderAsync(int userId, int traderId);
    Task<bool> UnfollowTraderByHandleAsync(int userId, string handle);
    Task<bool> IsFollowingAsync(int userId, int traderId);
    Task<List<int>> GetFollowerUserIdsForTraderAsync(int traderId);
    Task<List<int>> GetFollowerUserIdsForTraderHandleAsync(string handle);
}
