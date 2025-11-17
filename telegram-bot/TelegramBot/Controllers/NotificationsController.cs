using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelegramBot.Data;
using TelegramBot.Models;
using TelegramBot.Services;

namespace TelegramBot.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly ITelegramService _telegramService;
    private readonly ISolanaService _solanaService;
    private readonly ITraderService _traderService;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<NotificationsController> _logger;

    // Cache of known token symbols - loaded once and refreshed on table updates
    private static HashSet<string>? _knownTokenSymbolsCache;
    private static Dictionary<string, KnownToken>? _knownTokensCache;
    private static readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);

    public NotificationsController(
        ITelegramService telegramService,
        ISolanaService solanaService,
        ITraderService traderService,
        AppDbContext dbContext,
        ILogger<NotificationsController> logger)
    {
        _telegramService = telegramService;
        _solanaService = solanaService;
        _traderService = traderService;
        _dbContext = dbContext;
        _logger = logger;
    }

    // Public method to refresh cache (called when KnownTokens table is updated)
    public static void RefreshKnownTokensCache()
    {
        _knownTokenSymbolsCache = null;
        _knownTokensCache = null;
    }

    private async Task<Dictionary<string, KnownToken>> GetKnownTokensCacheAsync()
    {
        if (_knownTokensCache != null && _knownTokenSymbolsCache != null)
        {
            return _knownTokensCache;
        }

        await _cacheLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (_knownTokensCache != null && _knownTokenSymbolsCache != null)
            {
                return _knownTokensCache;
            }

            // Load from database
            var knownTokens = await _dbContext.KnownTokens.ToListAsync();
            _knownTokensCache = knownTokens.ToDictionary(kt => kt.Symbol, kt => kt);
            _knownTokenSymbolsCache = knownTokens.Select(kt => kt.Symbol).ToHashSet();

            _logger.LogInformation("Loaded {Count} known tokens into cache", knownTokens.Count);

            return _knownTokensCache;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    [HttpPost]
    public async Task<IActionResult> ReceiveNotification([FromBody] NotificationRequest noti)
     {
        try
        {
            _logger.LogInformation("📱 FOMO NOTIFICATION RECEIVED");
            _logger.LogInformation("Message: {Message}", noti.Message);

            // Validate message is not empty
            if (string.IsNullOrWhiteSpace(noti.Message))
            {
                _logger.LogWarning("Received empty notification message, skipping");
                return BadRequest(new
                {
                    status = "error",
                    message = "Notification message cannot be empty"
                });
            }

            string? ticker = null;
            string? trader = null;
            string? contractAddress = null;
            Chain? chain = null;

            // Check if this is a thesis notification
            if (IsThesisNotification(noti.Message))
            {
                _logger.LogInformation("📝 Detected THESIS notification");
                ticker = ExtractThesisTicker(noti.Message);
                trader = ExtractThesisTrader(noti.Message);
            }
            else
            {
                // Regular buy/sell notification
                ticker = ExtractTicker(noti.Message);
                trader = ExtractTrader(noti.Message);
            }

            // Save trader to database if found
            if (!string.IsNullOrEmpty(trader))
            {
                _logger.LogInformation("Extracted trader: {Trader}", trader);
                await _traderService.AddOrUpdateTraderAsync(trader);
            }
            else
            {
                _logger.LogWarning("Could not extract trader from message");
            }

            // Extract market cap once (will be used for retry if needed)
            long? marketCap = null;

            if (!string.IsNullOrEmpty(ticker))
            {
                _logger.LogInformation("Extracted ticker: {Ticker}", ticker);

                // Extract market cap from the notification
                marketCap = ExtractMarketCap(noti.Message);

                // Load known tokens cache
                var knownTokensCache = await GetKnownTokensCacheAsync();

                // Check if this ticker is in the known tokens list
                if (knownTokensCache.ContainsKey(ticker))
                {
                    _logger.LogInformation("Ticker {Ticker} found in known tokens list, checking market cap", ticker);

                    if (marketCap.HasValue)
                    {
                        var knownToken = knownTokensCache[ticker];
                        _logger.LogInformation("Extracted market cap: ${0:N0}, Min expected: ${1:N0}", marketCap.Value, knownToken.MinMarketCap);

                        // Check if market cap matches the expected range
                        if (marketCap.Value >= knownToken.MinMarketCap)
                        {
                            _logger.LogInformation("✅ Market cap matches! Using hardcoded contract: {Address} (Chain: {Chain})", knownToken.ContractAddress, knownToken.Chain);
                            contractAddress = knownToken.ContractAddress;
                            chain = knownToken.Chain;
                        }
                        else
                        {
                            _logger.LogInformation("⚠️  Market cap too low (${0:N0} < ${1:N0}), falling back to lookup", marketCap.Value, knownToken.MinMarketCap);
                            (contractAddress, chain) = await _solanaService.GetContractAddressAndChainByTickerAndMarketCapAsync(ticker, marketCap);
                        }
                    }
                    else
                    {
                        // No market cap found (thesis notifications don't have MC)
                        _logger.LogInformation("No market cap found in message, trying lookup without marketcap");
                        (contractAddress, chain) = await _solanaService.GetContractAddressAndChainByTickerAndMarketCapAsync(ticker, null);
                    }
                }
                else
                {
                    // Not a known token, use extracted MC
                    _logger.LogInformation("Ticker not in known tokens list, trying lookup (MarketCap: ${MarketCap:N0})", marketCap);
                    (contractAddress, chain) = await _solanaService.GetContractAddressAndChainByTickerAndMarketCapAsync(ticker, marketCap);
                }

                if (!string.IsNullOrEmpty(contractAddress))
                {
                    _logger.LogInformation("✅ Resolved contract address: {Address} (Chain: {Chain})", contractAddress, chain);
                }
                else
                {
                    _logger.LogWarning("⚠️  Could not resolve contract address for ticker: {Ticker}", ticker);
                }
            }
            else
            {
                _logger.LogWarning("Could not extract ticker from message");
            }

            // Send to users following this trader (or all if no trader extracted)
            // Pass marketcap for retry service
            await _telegramService.SendNotificationToAllUsersAsync(noti, contractAddress, trader, ticker, chain, marketCap);

            return Ok(new
            {
                status = "success",
                message = "Notification sent to Telegram",
                ticker = ticker ?? "",
                trader = trader ?? "",
                contractAddress = contractAddress ?? ""
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing notification");
            return StatusCode(500, new
            {
                status = "error",
                message = "Internal server error"
            });
        }
    }

    private string? ExtractTicker(string message)
    {
        // Format: "TICKER at $XXm MC ..." or "TICKER at $XXk MC ..."
        // Example: "KLED at $31.2m MC 🟢 @frankdegods bought $9,955.55"
        int atIndex = message.IndexOf(" at $", StringComparison.OrdinalIgnoreCase);
        if (atIndex > 0)
        {
            // Extract everything before " at" and trim whitespace
            string ticker = message.Substring(0, atIndex).Trim();
            return ticker.ToUpper();
        }
        return null;
    }

    private string? ExtractTrader(string message)
    {
        // Format: "TICKER at $XXm MC 🟢 @trader bought/sold $XXX"
        // Example: "KLED at $31.2m MC 🟢 @frankdegods bought $9,955.55"
        // Look for @ symbol after "MC" and before "bought" or "sold"
        var match = Regex.Match(message, @"MC\s+\S+\s+@(\w+)\s+(?:bought|sold)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value; // Returns "frankdegods" without the @
        }
        return null;
    }

    private bool IsThesisNotification(string message)
    {
        // Thesis notifications contain "thesis" but NOT "MC" and NOT "bought" or "sold"
        // Examples:
        // "Blobby thesis by 0xuberM I tailed nosanity I have no idea what's happening"
        // "BANGERS thesis by jotagezin AND I KNOW BANGERS"
        return message.Contains("thesis", StringComparison.OrdinalIgnoreCase) &&
               !message.Contains("MC", StringComparison.OrdinalIgnoreCase) &&
               !message.Contains("bought", StringComparison.OrdinalIgnoreCase) &&
               !message.Contains("sold", StringComparison.OrdinalIgnoreCase);
    }

    private string? ExtractThesisTicker(string message)
    {
        // Format: "TICKER thesis by trader ..."
        // Example: "Blobby thesis by 0xuberM I tailed nosanity I have no idea what's happening"
        int thesisIndex = message.IndexOf(" thesis by", StringComparison.OrdinalIgnoreCase);
        if (thesisIndex > 0)
        {
            // Extract everything before " thesis by" and trim whitespace
            string ticker = message.Substring(0, thesisIndex).Trim();
            return ticker.ToUpper();
        }
        return null;
    }

    private string? ExtractThesisTrader(string message)
    {
        // Format: "TICKER thesis by trader ..."
        // Example: "Blobby thesis by 0xuberM I tailed nosanity I have no idea what's happening"
        // Note: No @ symbol before trader name in thesis notifications
        var match = Regex.Match(message, @"thesis by\s+(\w+)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value; // Returns "0xuberM"
        }
        return null;
    }

    private long? ExtractMarketCap(string message)
    {
        // Format: "$XXm MC" or "$XX.XXb MC" or "$XXk MC"
        // Example: "PUMP at $1.44b MC 🟢 trader bought $1000"

        // Find " MC" in the message
        int mcIndex = message.IndexOf(" MC");
        if (mcIndex == -1) return null;

        // Work backwards to find the $ sign
        int dollarIndex = message.LastIndexOf('$', mcIndex);
        if (dollarIndex == -1) return null;

        // Extract the string between $ and MC (e.g., "2.60m")
        string mcString = message.Substring(dollarIndex + 1, mcIndex - dollarIndex - 1).Trim();

        if (mcString.Length == 0) return null;

        // Get the last character (unit: k, m, or b)
        char unit = mcString[mcString.Length - 1];

        // Get the number part (everything except last char)
        string numberPart = mcString.Substring(0, mcString.Length - 1);

        // Parse the number
        if (!double.TryParse(numberPart, out double value)) return null;

        // Multiply by unit
        long multiplier = unit switch
        {
            'k' => 1_000,
            'm' => 1_000_000,
            'b' => 1_000_000_000,
            _ => 0
        };

        if (multiplier == 0) return null;

        return (long)(value * multiplier);
    }
}
