namespace TelegramBot.Models;

public enum ContractAddressSource
{
    Cache = 1,
    DexScreener = 2,
    Helius = 3,
    KnownToken = 4  // From the hardcoded known tokens list
}
