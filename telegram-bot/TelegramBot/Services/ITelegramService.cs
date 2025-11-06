using TelegramBot.Models;

namespace TelegramBot.Services;

public interface ITelegramService
{
    Task SendNotificationAsync(NotificationRequest notification, string? contractAddress = null);
    bool IsConfigured();
}
