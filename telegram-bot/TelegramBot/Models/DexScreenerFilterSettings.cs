namespace TelegramBot.Models;

/// <summary>
/// Configurable settings for filtering DexScreener pairs to find the correct token
/// </summary>
public class DexScreenerFilterSettings
{
    /// <summary>
    /// Marketcap tolerance ranges - different tolerance percentages for different marketcap tiers
    /// Format: [MaxMarketcap, TolerancePercentage]
    /// </summary>
    public List<MarketCapToleranceRange> MarketCapToleranceRanges { get; set; } = new()
    {
        // Under 2M: ±500% tolerance (very volatile, price impact heavy)
        new MarketCapToleranceRange { MaxMarketCap = 2_000_000, TolerancePercent = 500 },

        // 2M - 10M: ±300% tolerance
        new MarketCapToleranceRange { MaxMarketCap = 10_000_000, TolerancePercent = 300 },

        // 10M - 50M: ±200% tolerance
        new MarketCapToleranceRange { MaxMarketCap = 50_000_000, TolerancePercent = 200 },

        // Above 50M: ±100% tolerance (more stable)
        new MarketCapToleranceRange { MaxMarketCap = double.MaxValue, TolerancePercent = 100 }
    };

    /// <summary>
    /// Minimum liquidity to marketcap ratio (as percentage)
    /// Example: 5 = liquidity should be at least 5% of marketcap
    /// Prevents fake pools with billion dollar marketcaps but only $10K liquidity
    /// </summary>
    public double MinLiquidityToMarketCapRatioPercent { get; set; } = 5.0;

    /// <summary>
    /// Maximum liquidity to marketcap ratio (as percentage)
    /// Example: 200 = liquidity shouldn't exceed 200% of marketcap
    /// Prevents suspicious pools
    /// </summary>
    public double MaxLiquidityToMarketCapRatioPercent { get; set; } = 200.0;

    /// <summary>
    /// Minimum absolute liquidity in USD
    /// Filters out extremely low liquidity pairs
    /// </summary>
    public double MinAbsoluteLiquidityUsd { get; set; } = 1000.0;

    /// <summary>
    /// Allowed chains (hard filter - only these chains will be accepted)
    /// Supported values: "solana", "bsc", "base"
    /// </summary>
    public List<string> AllowedChains { get; set; } = new()
    {
        "solana",
        "bsc",
        "base"
    };
}

/// <summary>
/// Defines a marketcap range and its tolerance percentage
/// </summary>
public class MarketCapToleranceRange
{
    /// <summary>
    /// Maximum marketcap for this range (exclusive upper bound)
    /// </summary>
    public double MaxMarketCap { get; set; }

    /// <summary>
    /// Tolerance percentage for this range
    /// Example: 500 = ±500% (marketcap can be 1/6th to 6x the expected value)
    /// </summary>
    public double TolerancePercent { get; set; }
}
