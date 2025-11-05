using TelegramBot.Models;

namespace TelegramBot.Services;

public interface ITelegramService
{
    Task SendNotificationAsync(NotificationRequest notification);
    bool IsConfigured();
}
