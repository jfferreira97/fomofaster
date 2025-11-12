using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelegramBot.Data;
using TelegramBot.Models;
using TelegramBot.Services;

namespace TelegramBot.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<DashboardController> _logger;
    private readonly ITelegramService _telegramService;
    private readonly ISolanaService _solanaService;

    public DashboardController(
        AppDbContext dbContext,
        ILogger<DashboardController> logger,
        ITelegramService telegramService,
        ISolanaService solanaService)
    {
        _dbContext = dbContext;
        _logger = logger;
        _telegramService = telegramService;
        _solanaService = solanaService;
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> GetRecentNotifications([FromQuery] int limit = 50)
    {
        try
        {
            // Get total active users count (single query)
            var totalUsers = await _dbContext.Users.CountAsync(u => u.IsActive);

            // Get recent notifications (single query)
            var recentNotifications = await _dbContext.Notifications
                .OrderByDescending(n => n.SentAt)
                .Take(limit)
                .ToListAsync();

            var notificationIds = recentNotifications.Select(n => n.Id).ToList();

            // Get all sent messages for these notifications (single query)
            var sentMessagesData = await _dbContext.SentMessages
                .Where(sm => notificationIds.Contains(sm.NotificationId))
                .GroupBy(sm => sm.NotificationId)
                .Select(g => new
                {
                    NotificationId = g.Key,
                    RecipientCount = g.Count(),
                    IsManuallyEdited = g.Any(sm => sm.IsManuallyEdited),
                    IsSystemEdited = g.Any(sm => sm.IsSystemEdited),
                    EditedAt = g.Where(sm => (sm.IsManuallyEdited || sm.IsSystemEdited) && sm.EditedAt != null)
                                .Select(sm => sm.EditedAt)
                                .Max()
                })
                .ToListAsync();

            // Create lookup dictionary for O(1) access
            var sentMessagesLookup = sentMessagesData.ToDictionary(x => x.NotificationId);

            // Combine data in memory
            var notifications = recentNotifications
                .Select(n =>
                {
                    var sentData = sentMessagesLookup.GetValueOrDefault(n.Id);
                    return new
                    {
                        id = n.Id,
                        message = n.Message,
                        ticker = n.Ticker,
                        trader = n.Trader,
                        hasCA = n.HasCA,
                        contractAddress = n.ContractAddress,
                        sentAt = n.SentAt,
                        recipientCount = sentData?.RecipientCount ?? 0,
                        isManuallyEdited = sentData?.IsManuallyEdited ?? false,
                        isSystemEdited = sentData?.IsSystemEdited ?? false,
                        editedAt = sentData?.EditedAt,
                        totalUsers = totalUsers
                    };
                })
                .OrderBy(n => n.sentAt) // Chronological order
                .ToList();

            return Ok(new
            {
                status = "success",
                notifications
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching dashboard notifications");
            return StatusCode(500, new
            {
                status = "error",
                message = "Failed to fetch notifications"
            });
        }
    }

    [HttpPost("notifications/{id}/edit-ca")]
    public async Task<IActionResult> EditNotificationCA(
        int id,
        [FromBody] EditCARequest request)
    {
        try
        {
            // Validate request
            if (string.IsNullOrEmpty(request.ContractAddress))
            {
                return BadRequest(new { status = "error", message = "Contract address is required" });
            }

            // Parse chain enum
            if (!Enum.TryParse<Chain>(request.Chain, true, out var chain))
            {
                return BadRequest(new { status = "error", message = "Invalid chain. Must be SOL, BNB, or BASE" });
            }

            // Get notification to verify it exists
            var notification = await _dbContext.Notifications.FindAsync(id);
            if (notification == null)
            {
                return NotFound(new { status = "error", message = "Notification not found" });
            }

            // Add to cache if ticker exists
            if (!string.IsNullOrEmpty(notification.Ticker))
            {
                _solanaService.AddToCache(notification.Ticker, request.ContractAddress);
            }

            // Edit all Telegram messages
            await _telegramService.EditNotificationMessagesAsync(id, request.ContractAddress, chain);

            return Ok(new
            {
                status = "success",
                message = "Notification edited successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editing notification CA");
            return StatusCode(500, new
            {
                status = "error",
                message = "Failed to edit notification"
            });
        }
    }
}

public record EditCARequest(string ContractAddress, string Chain);