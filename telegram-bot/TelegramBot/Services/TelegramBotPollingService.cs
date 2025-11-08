using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBot.Models;

namespace TelegramBot.Services;

public class TelegramBotPollingService : BackgroundService
{
    private readonly TelegramBotClient? _botClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TelegramBotPollingService> _logger;
    private int _offset = 0;

    public TelegramBotPollingService(
        IOptions<TelegramSettings> settings,
        IServiceProvider serviceProvider,
        ILogger<TelegramBotPollingService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        if (!string.IsNullOrEmpty(settings.Value.BotToken))
        {
            _botClient = new TelegramBotClient(settings.Value.BotToken);
            _logger.LogInformation("Telegram polling service initialized");
        }
        else
        {
            _logger.LogWarning("Bot token not configured, polling service will not start");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_botClient == null)
        {
            _logger.LogWarning("Bot client not initialized, polling service stopped");
            return;
        }

        _logger.LogInformation("Starting Telegram bot polling...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var updates = await _botClient.GetUpdatesAsync(
                    offset: _offset,
                    timeout: 30,
                    cancellationToken: stoppingToken
                );

                foreach (var update in updates)
                {
                    _offset = update.Id + 1;
                    await HandleUpdateAsync(update);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Polling cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during polling");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("Telegram bot polling stopped");
    }

    private async Task HandleUpdateAsync(Update update)
    {
        try
        {
            if (update.Message is { } message)
            {
                await HandleMessageAsync(message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling update {UpdateId}", update.Id);
        }
    }

    private async Task HandleMessageAsync(Message message)
    {
        var chatId = message.Chat.Id;
        var text = message.Text?.Trim();

        _logger.LogInformation("Received message from {ChatId}: {Text}", chatId, text);

        if (string.IsNullOrEmpty(text))
            return;

        using var scope = _serviceProvider.CreateScope();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();

        // Handle commands
        if (text.StartsWith("/"))
        {
            await HandleCommandAsync(message, userService);
        }
    }

    private async Task HandleCommandAsync(Message message, IUserService userService)
    {
        if (_botClient == null)
            return;

        var chatId = message.Chat.Id;
        var command = message.Text?.Split(' ')[0].ToLower();

        using var scope = _serviceProvider.CreateScope();
        var traderService = scope.ServiceProvider.GetRequiredService<ITraderService>();

        switch (command)
        {
            case "/start":
                await userService.AddOrUpdateUserAsync(
                    chatId,
                    message.From?.Username,
                    message.From?.FirstName
                );

                await _botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: @"🎉 Welcome to FOMOFAST!

You'll now receive real-time notifications when top Solana traders make moves.

Commands:
/help - Show available commands
/list - View all available traders
/mytraders - View traders you're following
/follow - Follow traders to get their notifications
/unfollow - Unfollow traders

Stay ahead of the curve! 🚀",
                    parseMode: ParseMode.Markdown
                );

                _logger.LogInformation("User started bot: ChatId={ChatId}, Username={Username}",
                    chatId, message.From?.Username);
                break;

            case "/help":
                await _botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: @"📚 FOMOFAST Commands:

/start - Subscribe to notifications
/help - Show this help message
/list - View all available traders
/mytraders - View traders you're following
/follow <ids/handles> - Follow traders (e.g., `/follow 1,2,3` or `/follow @trader1,@trader2`)
/unfollow <ids/handles> - Unfollow traders (e.g., `/unfollow 1,@trader2`)

You'll only receive notifications from traders you follow!",
                    parseMode: ParseMode.Markdown
                );
                break;

            case "/list":
                var user = await userService.GetUserByChatIdAsync(chatId);

                if (user == null)
                {
                    await _botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: "❌ Please use /start first to register.",
                        parseMode: ParseMode.Markdown
                    );
                    break;
                }

                var allTraders = await traderService.GetAllTradersAsync();

                if (allTraders.Count == 0)
                {
                    await _botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: "📭 No traders in the system yet. They'll appear as notifications come in!",
                        parseMode: ParseMode.Markdown
                    );
                    break;
                }

                var traderLines = new List<string>();
                foreach (var trader in allTraders)
                {
                    var isFollowing = await traderService.IsFollowingAsync(user.Id, trader.Id);
                    var status = isFollowing ? "✅" : "❌";
                    traderLines.Add($"{status} **{trader.Id}** - @{trader.Handle}");
                }

                var listMessage = $@"📊 **All Traders** ({allTraders.Count} total)

{string.Join("\n", traderLines)}

Use `/follow 1,2,3` or `/follow @trader1,@trader2` to follow traders.";

                await _botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: listMessage,
                    parseMode: ParseMode.Markdown
                );
                break;

            case "/mytraders":
                var userForMyTraders = await userService.GetUserByChatIdAsync(chatId);

                if (userForMyTraders == null)
                {
                    await _botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: "❌ Please use /start first to register.",
                        parseMode: ParseMode.Markdown
                    );
                    break;
                }

                var followedTraders = await traderService.GetTradersByUserIdAsync(userForMyTraders.Id);

                if (followedTraders.Count == 0)
                {
                    await _botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: "📭 You're not following any traders yet.\n\nUse `/list` to see all available traders, then `/follow` to start following them!",
                        parseMode: ParseMode.Markdown
                    );
                    break;
                }

                var myTraderLines = new List<string>();
                foreach (var trader in followedTraders)
                {
                    myTraderLines.Add($"✅ **{trader.Id}** - @{trader.Handle}");
                }

                var myTradersMessage = $@"📊 **Your Followed Traders** ({followedTraders.Count} total)

{string.Join("\n", myTraderLines)}

Use `/unfollow 1,2,3` or `/unfollow @trader1,@trader2` to unfollow traders.";

                await _botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: myTradersMessage,
                    parseMode: ParseMode.Markdown
                );
                break;

            case "/follow":
                var userForFollow = await userService.GetUserByChatIdAsync(chatId);

                if (userForFollow == null)
                {
                    await _botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: "❌ Please use /start first to register.",
                        parseMode: ParseMode.Markdown
                    );
                    break;
                }

                var followArgs = message.Text?.Split(' ', 2);
                if (followArgs == null || followArgs.Length < 2 || string.IsNullOrWhiteSpace(followArgs[1]))
                {
                    await _botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: "❌ Please specify traders to follow.\n\nExamples:\n`/follow 1,2,3`\n`/follow @trader1,@trader2`\n`/follow 1,@trader2,3`",
                        parseMode: ParseMode.Markdown
                    );
                    break;
                }

                var followInput = followArgs[1].Trim();
                var followParts = followInput.Split(',').Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToList();

                if (followParts.Count == 0)
                {
                    await _botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: "❌ Please specify traders to follow.\n\nExamples:\n`/follow 1,2,3`\n`/follow @trader1,@trader2`",
                        parseMode: ParseMode.Markdown
                    );
                    break;
                }

                var followedCount = 0;
                var alreadyFollowingCount = 0;
                var notFoundList = new List<string>();

                foreach (var part in followParts)
                {
                    bool success;

                    // Check if it's an ID (number) or handle (starts with @)
                    if (int.TryParse(part, out var traderId))
                    {
                        // Follow by ID
                        var trader = await traderService.GetTraderByIdAsync(traderId);
                        if (trader == null)
                        {
                            notFoundList.Add(part);
                            continue;
                        }
                        success = await traderService.FollowTraderAsync(userForFollow.Id, traderId);
                    }
                    else if (part.StartsWith("@"))
                    {
                        // Follow by handle
                        var handle = part.Substring(1); // Remove @ symbol
                        success = await traderService.FollowTraderByHandleAsync(userForFollow.Id, handle);

                        if (!success)
                        {
                            // Check if trader exists
                            var trader = await traderService.GetTraderByHandleAsync(handle);
                            if (trader == null)
                            {
                                notFoundList.Add(part);
                                continue;
                            }
                            // Trader exists but already following
                            alreadyFollowingCount++;
                            continue;
                        }
                    }
                    else
                    {
                        notFoundList.Add(part);
                        continue;
                    }

                    if (success)
                        followedCount++;
                    else
                        alreadyFollowingCount++;
                }

                var followResultParts = new List<string>();
                if (followedCount > 0)
                    followResultParts.Add($"✅ Now following {followedCount} trader(s)");
                if (alreadyFollowingCount > 0)
                    followResultParts.Add($"ℹ️ Already following {alreadyFollowingCount} trader(s)");
                if (notFoundList.Count > 0)
                    followResultParts.Add($"❌ Not found: {string.Join(", ", notFoundList)}");

                var followResultMessage = string.Join("\n", followResultParts);

                await _botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: followResultMessage,
                    parseMode: ParseMode.Markdown
                );
                break;

            case "/unfollow":
                var userForUnfollow = await userService.GetUserByChatIdAsync(chatId);

                if (userForUnfollow == null)
                {
                    await _botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: "❌ Please use /start first to register.",
                        parseMode: ParseMode.Markdown
                    );
                    break;
                }

                var unfollowArgs = message.Text?.Split(' ', 2);
                if (unfollowArgs == null || unfollowArgs.Length < 2 || string.IsNullOrWhiteSpace(unfollowArgs[1]))
                {
                    await _botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: "❌ Please specify traders to unfollow.\n\nExamples:\n`/unfollow 1,2,3`\n`/unfollow @trader1,@trader2`\n`/unfollow 1,@trader2,3`",
                        parseMode: ParseMode.Markdown
                    );
                    break;
                }

                var unfollowInput = unfollowArgs[1].Trim();
                var unfollowParts = unfollowInput.Split(',').Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToList();

                if (unfollowParts.Count == 0)
                {
                    await _botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: "❌ Please specify traders to unfollow.\n\nExamples:\n`/unfollow 1,2,3`\n`/unfollow @trader1,@trader2`",
                        parseMode: ParseMode.Markdown
                    );
                    break;
                }

                var unfollowedCount = 0;
                var notFollowingCount = 0;
                var unfollowNotFoundList = new List<string>();

                foreach (var part in unfollowParts)
                {
                    bool success;

                    // Check if it's an ID (number) or handle (starts with @)
                    if (int.TryParse(part, out var traderId))
                    {
                        // Unfollow by ID
                        var trader = await traderService.GetTraderByIdAsync(traderId);
                        if (trader == null)
                        {
                            unfollowNotFoundList.Add(part);
                            continue;
                        }
                        success = await traderService.UnfollowTraderAsync(userForUnfollow.Id, traderId);
                    }
                    else if (part.StartsWith("@"))
                    {
                        // Unfollow by handle
                        var handle = part.Substring(1); // Remove @ symbol
                        success = await traderService.UnfollowTraderByHandleAsync(userForUnfollow.Id, handle);

                        if (!success)
                        {
                            // Check if trader exists
                            var trader = await traderService.GetTraderByHandleAsync(handle);
                            if (trader == null)
                            {
                                unfollowNotFoundList.Add(part);
                                continue;
                            }
                            // Trader exists but not following
                            notFollowingCount++;
                            continue;
                        }
                    }
                    else
                    {
                        unfollowNotFoundList.Add(part);
                        continue;
                    }

                    if (success)
                        unfollowedCount++;
                    else
                        notFollowingCount++;
                }

                var unfollowResultParts = new List<string>();
                if (unfollowedCount > 0)
                    unfollowResultParts.Add($"✅ Unfollowed {unfollowedCount} trader(s)");
                if (notFollowingCount > 0)
                    unfollowResultParts.Add($"ℹ️ Weren't following {notFollowingCount} trader(s)");
                if (unfollowNotFoundList.Count > 0)
                    unfollowResultParts.Add($"❌ Not found: {string.Join(", ", unfollowNotFoundList)}");

                var unfollowResultMessage = string.Join("\n", unfollowResultParts);

                await _botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: unfollowResultMessage,
                    parseMode: ParseMode.Markdown
                );
                break;

            default:
                await _botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "❓ Unknown command. Use /help to see available commands.",
                    parseMode: ParseMode.Markdown
                );
                break;
        }
    }
}
