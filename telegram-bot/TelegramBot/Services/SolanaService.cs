using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using TelegramBot.Models;

namespace TelegramBot.Services;

public class SolanaService : ISolanaService
{
    private readonly HeliusSettings _settings;
    private readonly HttpClient _httpClient;
    private readonly ILogger<SolanaService> _logger;

    // Cache: ticker -> (contract address, last accessed time)
    private readonly ConcurrentDictionary<string, (string contractAddress, DateTime lastAccessed)> _tickerCache;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromHours(1);

    public SolanaService(
        IOptions<HeliusSettings> settings,
        IHttpClientFactory httpClientFactory,
        ILogger<SolanaService> logger)
    {
        _settings = settings.Value;
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(5); // Fast timeout for speed
        _logger = logger;
        _tickerCache = new ConcurrentDictionary<string, (string, DateTime)>();

        // Start background task to clean expired cache entries
        Task.Run(CleanupExpiredCacheEntries);
    }

    public async Task<string?> GetContractAddressByTickerAsync(string ticker)
    {
        if (string.IsNullOrEmpty(ticker))
        {
            _logger.LogWarning("Empty ticker provided");
            return null;
        }

        // Check cache first
        if (_tickerCache.TryGetValue(ticker, out var cached))
        {
            // Update last accessed time
            _tickerCache[ticker] = (cached.contractAddress, DateTime.UtcNow);
            _logger.LogInformation("💾 Cache hit for ticker: {Ticker} -> {Address}", ticker, cached.contractAddress);
            return cached.contractAddress;
        }

        try
        {
            _logger.LogInformation("🔍 Searching for contract address for ticker: {Ticker}", ticker);

            // Get recent transactions to extract mint addresses
            var txUrl = $"https://api.helius.xyz/v0/addresses/{_settings.FomoAggregatorWallet}/transactions?api-key={_settings.ApiKey}&limit=70";
            var txResponse = await _httpClient.GetAsync(txUrl);

            if (!txResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Helius transactions API returned {StatusCode}", txResponse.StatusCode);
                return null;
            }

            var txBody = await txResponse.Content.ReadAsStringAsync();
            var txDoc = JsonDocument.Parse(txBody);

            // Collect unique mint addresses from recent transactions
            var mintAddresses = new HashSet<string>();
            foreach (var tx in txDoc.RootElement.EnumerateArray())
            {
                if (!tx.TryGetProperty("tokenTransfers", out var tokenTransfers))
                    continue;

                foreach (var transfer in tokenTransfers.EnumerateArray())
                {
                    if (transfer.TryGetProperty("mint", out var mintElement))
                    {
                        var mint = mintElement.GetString();
                        if (!string.IsNullOrEmpty(mint) && mint != "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v") // Skip USDC
                        {
                            mintAddresses.Add(mint);
                        }
                    }
                }
            }

            _logger.LogInformation("Found {Count} unique mint addresses in recent transactions", mintAddresses.Count);

            // Query metadata for each mint to find matching ticker
            foreach (var mint in mintAddresses)
            {
                try
                {
                    var metadataUrl = $"https://api.helius.xyz/v0/token-metadata?api-key={_settings.ApiKey}";
                    var metadataRequest = new
                    {
                        mintAccounts = new[] { mint }
                    };

                    var content = new StringContent(
                        JsonSerializer.Serialize(metadataRequest),
                        Encoding.UTF8,
                        "application/json");

                    var metadataResponse = await _httpClient.PostAsync(metadataUrl, content);
                    if (!metadataResponse.IsSuccessStatusCode)
                        continue;

                    var metadataBody = await metadataResponse.Content.ReadAsStringAsync();
                    var metadataDoc = JsonDocument.Parse(metadataBody);

                    if (metadataDoc.RootElement.GetArrayLength() > 0)
                    {
                        var tokenInfo = metadataDoc.RootElement[0];

                        // Check symbol in onChainMetadata or account.data.parsed.info
                        string? symbol = null;

                        if (tokenInfo.TryGetProperty("onChainMetadata", out var onChainMetadata) &&
                            onChainMetadata.ValueKind != JsonValueKind.Null &&
                            onChainMetadata.TryGetProperty("metadata", out var metadata) &&
                            metadata.ValueKind != JsonValueKind.Null &&
                            metadata.TryGetProperty("data", out var data) &&
                            data.ValueKind != JsonValueKind.Null &&
                            data.TryGetProperty("symbol", out var symbolElement) &&
                            symbolElement.ValueKind != JsonValueKind.Null)
                        {
                            symbol = symbolElement.GetString();
                        }

                        if (!string.IsNullOrEmpty(symbol) && symbol.Equals(ticker, StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogInformation("✅ Found contract address: {Address} for ticker: {Ticker}", mint, ticker);

                            // Cache the result
                            _tickerCache[ticker] = (mint, DateTime.UtcNow);

                            return mint;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error fetching metadata for mint: {Mint}", mint);
                }
            }

            _logger.LogWarning("❌ No matching contract address found for ticker: {Ticker}", ticker);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching contract address for ticker: {Ticker}", ticker);
            return null;
        }
    }

    private async Task CleanupExpiredCacheEntries()
    {
        while (true)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(10)); // Check every 10 minutes

                var now = DateTime.UtcNow;
                var expiredKeys = _tickerCache
                    .Where(kvp => now - kvp.Value.lastAccessed > _cacheExpiration)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    if (_tickerCache.TryRemove(key, out var removed))
                    {
                        _logger.LogInformation("🗑️  Removed expired cache entry for ticker: {Ticker} (last accessed: {LastAccessed})",
                            key, removed.lastAccessed);
                    }
                }

                if (expiredKeys.Count > 0)
                {
                    _logger.LogInformation("🧹 Cleaned up {Count} expired cache entries", expiredKeys.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cache cleanup");
            }
        }
    }

}
