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

            // Send to users following this trader (or all if no trader extracted)
            await _telegramService.SendNotificationToAllUsersAsync(noti, contractAddress, trader);

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
}
