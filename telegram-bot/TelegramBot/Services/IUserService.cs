using TelegramBot.Models;

namespace TelegramBot.Services;

public interface IUserService
{
    Task<User?> GetUserByChatIdAsync(long chatId);
    Task<List<User>> GetAllActiveUsersAsync();
    Task<User> AddOrUpdateUserAsync(long chatId, string? username, string? firstName);
    Task DeactivateUserAsync(long chatId);
}
