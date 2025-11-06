namespace TelegramBot.Models;

public class User
{
    public int Id { get; set; }
    public long ChatId { get; set; }
    public string? Username { get; set; }
    public string? FirstName { get; set; }
    public DateTime JoinedAt { get; set; }
    public bool IsActive { get; set; }
}
