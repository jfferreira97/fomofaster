namespace TelegramBot.Models;

public class Notification
{
    public int Id { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Ticker { get; set; }
    public string? Trader { get; set; }
    public bool HasCA { get; set; }
    public string? ContractAddress { get; set; }
    public Chain? Chain { get; set; }
    public DateTime SentAt { get; set; }
}