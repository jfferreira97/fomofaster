using Microsoft.AspNetCore.Mvc;
using TelegramBot.Services;

namespace TelegramBot.Controllers;

// This API Controller isn't publicly exposed, mostly used for internal testing, and simulating user actions.
[ApiController]
[Route("api/[controller]")]
public class TradersController : ControllerBase
{
    private readonly ITraderService _traderService;
    private readonly IUserService _userService;
    private readonly ILogger<TradersController> _logger;

    public TradersController(
        ITraderService traderService,
        IUserService userService,
        ILogger<TradersController> logger)
    {
        _traderService = traderService;
        _userService = userService;
        _logger = logger;
    }

    [HttpPost("follow")]
    public async Task<IActionResult> FollowTrader([FromBody] FollowRequest request)
    {
        try
        {
            var user = await _userService.GetUserByChatIdAsync(request.ChatId);
            if (user == null)
            {
                return NotFound(new { status = "error", message = "User not found" });
            }

            var success = await _traderService.FollowTraderAsync(user.Id, request.TraderId);

            return Ok(new
            {
                status = "success",
                message = success ? "Now following trader" : "Already following trader",
                followed = success
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error following trader");
            return StatusCode(500, new { status = "error", message = ex.Message });
        }
    }

    [HttpPost("unfollow")]
    public async Task<IActionResult> UnfollowTrader([FromBody] FollowRequest request)
    {
        try
        {
            var user = await _userService.GetUserByChatIdAsync(request.ChatId);
            if (user == null)
            {
                return NotFound(new { status = "error", message = "User not found" });
            }

            var success = await _traderService.UnfollowTraderAsync(user.Id, request.TraderId);

            return Ok(new
            {
                status = "success",
                message = success ? "Unfollowed trader" : "Was not following trader",
                unfollowed = success
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unfollowing trader");
            return StatusCode(500, new { status = "error", message = ex.Message });
        }
    }

    [HttpPost("bulk-add")]
    public async Task<IActionResult> BulkAddTraders([FromBody] BulkAddTradersRequest request)
    {
        try
        {
            var added = new List<string>();
            var skipped = new List<string>();

            foreach (var handle in request.Handles)
            {
                // Strip @ if present
                var cleanHandle = handle.TrimStart('@');

                // Add new trader
                await _traderService.AddOrUpdateTraderAsync(cleanHandle);
                added.Add(cleanHandle);
            }

            return Ok(new
            {
                status = "success",
                added = added.Count,
                skipped = skipped.Count,
                total = request.Handles.Length,
                addedHandles = added,
                skippedHandles = skipped
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk adding traders");
            return StatusCode(500, new { status = "error", message = ex.Message });
        }
    }
}

public record FollowRequest(long ChatId, int TraderId);
public record BulkAddTradersRequest(string[] Handles);