using TelegramBot.Models;

namespace TelegramBot.Services;

public interface IDexScreenerService
{
    Task<DexScreenerResponse?> SearchTokenByTickerAsync(string ticker);

    Task<string?> GetContractAddressByTickerAndMarketCapAsync(string ticker, double expectedMarketCap);
}
