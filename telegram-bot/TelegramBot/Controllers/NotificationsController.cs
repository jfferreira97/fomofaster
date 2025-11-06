using Microsoft.AspNetCore.Mvc;
using TelegramBot.Models;
using TelegramBot.Services;

namespace TelegramBot.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly ITelegramService _telegramService;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        ITelegramService telegramService,
        ILogger<NotificationsController> logger)
    {
        _telegramService = telegramService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> ReceiveNotification([FromBody] NotificationRequest noti)
    {
        try
        {
            _logger.LogInformation("📱 FOMO NOTIFICATION RECEIVED");
            _logger.LogInformation("Message: {Message}", noti.Message);

            await _telegramService.SendNotificationAsync(noti);

            return Ok(new
            {
                status = "success",
                message = "Notification sent to Telegram"
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
}
