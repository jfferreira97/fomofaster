using System.Text.Json;
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
    public async Task<IActionResult> GetRecentNotifications([FromQuery] int limit = 50, [FromQuery] int? id = null)
    {
        try
        {
            // Get total active users count (single query)
            var totalUsers = await _dbContext.Users.CountAsync(u => u.IsActive);

            // Get recent notifications (single query)
            var recentNotifications = id.HasValue
                ? await _dbContext.Notifications.Where(n => n.Id == id.Value).ToListAsync()
                : await _dbContext.Notifications
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
                        chain = n.Chain?.ToString(),
                        sentAt = n.SentAt,
                        recipientCount = sentData?.RecipientCount ?? 0,
                        isManuallyEdited = sentData?.IsManuallyEdited ?? false,
                        isSystemEdited = sentData?.IsSystemEdited ?? false,
                        editedAt = sentData?.EditedAt,
                        totalUsers = totalUsers,
                        // Tracking data
                        contractAddressSource = n.ContractAddressSource?.ToString(),
                        timesCacheHit = n.TimesCacheHit,
                        timesDexScreenerApiHit = n.TimesDexScreenerApiHit,
                        timesHeliusApiHit = n.TimesHeliusApiHit,
                        lookupDuration = n.LookupDuration?.TotalMilliseconds,
                        wasRetried = n.WasRetried,
                        marketCapAtNotification = n.MarketCapAtNotification,
                        lookupDiagnostics = n.LookupDiagnostics != null
                            ? JsonSerializer.Deserialize<object>(n.LookupDiagnostics)
                            : null
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

    [HttpGet("notifications/{id}/recipients")]
    public async Task<IActionResult> GetNotificationRecipients(int id)
    {
        try
        {
            // Get all users who received this notification
            var recipients = await _dbContext.SentMessages
                .Where(sm => sm.NotificationId == id)
                .Join(_dbContext.Users,
                    sm => sm.ChatId,
                    u => u.ChatId,
                    (sm, u) => new
                    {
                        chatId = u.ChatId,
                        username = u.Username,
                        firstName = u.FirstName
                    })
                .ToListAsync();

            return Ok(new
            {
                status = "success",
                recipients
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching notification recipients");
            return StatusCode(500, new
            {
                status = "error",
                message = "Failed to fetch recipients"
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

    [HttpGet("payments")]
    public async Task<IActionResult> GetPayments()
    {
        try
        {
            var users = await _dbContext.Users.ToListAsync();
            var payments = await _dbContext.PendingPayments.ToListAsync();

            var now = DateTime.UtcNow;

            var rows = users
                .Where(u => payments.Any(p => p.ChatId == u.ChatId) || u.IsRegisteredNurse || u.IsRN4L)
                .Select(u =>
                {
                    var userPayments = payments.Where(p => p.ChatId == u.ChatId).ToList();
                    var latestPending = userPayments
                        .Where(p => !p.IsConfirmed && p.ExpiresAt > now)
                        .OrderByDescending(p => p.CreatedAt)
                        .FirstOrDefault();
                    var latestExpiredRequest = userPayments
                        .Where(p => !p.IsConfirmed && p.ExpiresAt <= now)
                        .OrderByDescending(p => p.ExpiresAt)
                        .FirstOrDefault();
                    var confirmed = userPayments.FirstOrDefault(p => p.IsConfirmed);

                    string status;
                    if (latestPending != null)
                        status = "pending";
                    else if (u.IsRN4L || u.IsRegisteredNurse)
                        status = "active";
                    else if (confirmed != null)
                        status = "expired_sub";
                    else if (latestExpiredRequest != null)
                        status = "expired_request";
                    else if (!u.IsActive && userPayments.Any())
                        status = "blocked";
                    else
                        status = "expired_request";

                    var relevantPayment = latestPending ?? confirmed ?? latestExpiredRequest;

                    return new
                    {
                        chatId = u.ChatId,
                        username = u.Username,
                        firstName = u.FirstName,
                        isRN4L = u.IsRN4L,
                        isRegisteredNurse = u.IsRegisteredNurse,
                        rnExpiresAt = u.RNExpiresAt,
                        isActive = u.IsActive,
                        status,
                        wallet = relevantPayment?.WalletPublicKey,
                        requestedAt = relevantPayment?.CreatedAt,
                        paymentDate = confirmed?.ConfirmedAt,
                        expiresAt = latestPending?.ExpiresAt
                    };
                })
                .ToList();

            return Ok(new { status = "success", payments = rows });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching payments");
            return StatusCode(500, new { status = "error", message = "Failed to fetch payments" });
        }
    }

    [HttpPost("payments/grant")]
    public async Task<IActionResult> GrantAccess([FromBody] GrantAccessRequest request)
    {
        try
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.ChatId == request.ChatId);
            if (user == null)
                return NotFound(new { status = "error", message = "User not found" });

            if (request.IsRN4L)
            {
                user.IsRN4L = true;
                user.IsRegisteredNurse = true;
                user.RNExpiresAt = null;
            }
            else
            {
                user.IsRegisteredNurse = true;
                user.RNExpiresAt = DateTime.UtcNow.AddDays(30);
            }

            await _dbContext.SaveChangesAsync();

            var msg = request.IsRN4L
                ? "✅ You've been granted lifetime access to FomoFaster. Welcome to the club."
                : $"✅ You've been granted 30 days of full access (until {user.RNExpiresAt:yyyy-MM-dd}). Enjoy.";

            await _telegramService.SendPlainMessageAsync(request.ChatId, msg);

            _logger.LogInformation("Manually granted {Type} to ChatId={ChatId}", request.IsRN4L ? "RN4L" : "RN30d", request.ChatId);
            return Ok(new { status = "success" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error granting access");
            return StatusCode(500, new { status = "error", message = "Failed to grant access" });
        }
    }

    [HttpPost("payments/revoke")]
    public async Task<IActionResult> RevokeAccess([FromBody] RevokeAccessRequest request)
    {
        try
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.ChatId == request.ChatId);
            if (user == null)
                return NotFound(new { status = "error", message = "User not found" });

            user.IsRegisteredNurse = false;
            user.IsRN4L = false;
            user.RNExpiresAt = null;

            await _dbContext.SaveChangesAsync();
            await _telegramService.SendPlainMessageAsync(request.ChatId, "Your FomoFaster access has been revoked. Use /subscribe to resubscribe.");

            _logger.LogInformation("Manually revoked access for ChatId={ChatId}", request.ChatId);
            return Ok(new { status = "success" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking access");
            return StatusCode(500, new { status = "error", message = "Failed to revoke access" });
        }
    }
}

public record EditCARequest(string ContractAddress, string Chain);
public record GrantAccessRequest(long ChatId, bool IsRN4L);
public record RevokeAccessRequest(long ChatId);