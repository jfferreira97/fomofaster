using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
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
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        ITelegramService telegramService,
        ISolanaService solanaService,
        ITraderService traderService,
        ILogger<NotificationsController> logger)
    {
        _telegramService = telegramService;
        _solanaService = solanaService;
        _traderService = traderService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> ReceiveNotification([FromBody] NotificationRequest noti)
     {
        try
        {
            _logger.LogInformation("📱 FOMO NOTIFICATION RECEIVED");
            _logger.LogInformation("Message: {Message}", noti.Message);

            var ticker = ExtractTicker(noti.Message);
            var trader = ExtractTrader(noti.Message);
            string? contractAddress = null;

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

            if (!string.IsNullOrEmpty(ticker))
            {
                _logger.LogInformation("Extracted ticker: {Ticker}", ticker);

                // Try to resolve contract address from Solana
                contractAddress = await _solanaService.GetContractAddressByTickerAsync(ticker);

                if (!string.IsNullOrEmpty(contractAddress))
                {
                    _logger.LogInformation("✅ Resolved contract address: {Address}", contractAddress);
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

            // Send to all active Telegram users (with or without contract address)
            await _telegramService.SendNotificationToAllUsersAsync(noti, contractAddress);

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
}
