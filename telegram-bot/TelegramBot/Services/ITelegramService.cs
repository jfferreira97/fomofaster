using TelegramBot.Models;

namespace TelegramBot.Services;

public interface ITelegramService
{
    Task SendNotificationAsync(NotificationRequest notification, string? contractAddress = null);
    Task SendTestMessageAsync(long chatId, string message);
    Task<object> GetUpdatesAsync();
    bool IsConfigured();
}
