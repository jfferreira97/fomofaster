using TelegramBot.Models;

namespace TelegramBot.Services;

public interface ITraderService
{
    Task<Trader?> GetTraderByHandleAsync(string handle);
    Task<List<Trader>> GetAllTradersAsync();
    Task<Trader> AddOrUpdateTraderAsync(string handle);
}
