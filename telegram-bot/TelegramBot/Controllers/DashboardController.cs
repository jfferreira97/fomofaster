using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelegramBot.Data;

namespace TelegramBot.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(AppDbContext dbContext, ILogger<DashboardController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> GetRecentNotifications([FromQuery] int limit = 50)
    {
        try
        {
            var notifications = await _dbContext.Notifications
                .OrderByDescending(n => n.SentAt)
                .Take(limit)
                .Select(n => new
                {
                    id = n.Id,
                    message = n.Message,
                    ticker = n.Ticker,
                    trader = n.Trader,
                    hasCA = n.HasCA,
                    contractAddress = n.ContractAddress,
                    sentAt = n.SentAt,
                    recipientCount = _dbContext.SentMessages
                        .Count(sm => sm.NotificationId == n.Id)
                })
                .ToListAsync();

            return Ok(new
            {
                status = "success",
                notifications = notifications.OrderBy(n => n.sentAt).ToList() // Reverse to chronological order
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
}