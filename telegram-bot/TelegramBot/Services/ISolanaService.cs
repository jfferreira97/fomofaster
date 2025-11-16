namespace TelegramBot.Services;

public interface ISolanaService
{
    Task<string?> GetContractAddressByTickerAsync(string ticker);
    Task<string?> GetContractAddressByTickerAndMarketCapAsync(string ticker, double? marketCap);
    void AddToCache(string ticker, string contractAddress);
}
