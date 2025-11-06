namespace TelegramBot.Services;

public interface ISolanaService
{
    Task<string?> GetContractAddressByTickerAsync(string ticker);
}
