using TelegramBot.Models;

namespace TelegramBot.Services;

public interface ITelegramService
{
    Task SendNotificationToAllUsersAsync(NotificationRequest notification, string? contractAddress = null, string? traderHandle = null, string? ticker = null);
    Task SendTestMessageAsync(long chatId, string message);
    Task<object> GetUpdatesAsync();
    bool IsConfigured();
    Task EditNotificationMessagesAsync(int notificationId, string contractAddress, Chain chain);
}
