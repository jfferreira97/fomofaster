using Microsoft.AspNetCore.Mvc;
using TelegramBot.Services;

namespace TelegramBot.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ITelegramService _telegramService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IUserService userService, ITelegramService telegramService, ILogger<UsersController> logger)
    {
        _userService = userService;
        _telegramService = telegramService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllUsers()
    {
        try
        {
            var users = await _userService.GetAllActiveUsersAsync();
            return Ok(new
            {
                status = "success",
                count = users.Count,
                users = users.Select(u => new
                {
                    chatId = u.ChatId,
                    username = u.Username,
                    firstName = u.FirstName,
                    joinedAt = u.JoinedAt,
                    isActive = u.IsActive
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users");
            return StatusCode(500, new { status = "error", message = ex.Message });
        }
    }

    [HttpGet("{chatId}")]
    public async Task<IActionResult> GetUser(long chatId)
    {
        try
        {
            var user = await _userService.GetUserByChatIdAsync(chatId);
            if (user == null)
            {
                return NotFound(new { status = "error", message = "User not found" });
            }

            return Ok(new
            {
                status = "success",
                user = new
                {
                    chatId = user.ChatId,
                    username = user.Username,
                    firstName = user.FirstName,
                    joinedAt = user.JoinedAt,
                    isActive = user.IsActive
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user");
            return StatusCode(500, new { status = "error", message = ex.Message });
        }
    }

    [HttpPost("add")]
    public async Task<IActionResult> AddUser([FromBody] AddUserRequest request)
    {
        try
        {
            var user = await _userService.AddOrUpdateUserAsync(
                request.ChatId,
                request.Username,
                request.FirstName
            );

            return Ok(new
            {
                status = "success",
                message = "User added/updated",
                user = new
                {
                    chatId = user.ChatId,
                    username = user.Username,
                    firstName = user.FirstName,
                    joinedAt = user.JoinedAt,
                    isActive = user.IsActive
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding user");
            return StatusCode(500, new { status = "error", message = ex.Message });
        }
    }

    [HttpPost("{chatId}/deactivate")]
    public async Task<IActionResult> DeactivateUser(long chatId)
    {
        try
        {
            await _userService.DeactivateUserAsync(chatId);
            return Ok(new { status = "success", message = "User deactivated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating user");
            return StatusCode(500, new { status = "error", message = ex.Message });
        }
    }

    [HttpPost("send-message")]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
    {
        try
        {
            // Validate user exists and is active
            var user = await _userService.GetUserByChatIdAsync(request.ChatId);
            if (user == null)
            {
                return NotFound(new { status = "error", message = "User not found" });
            }

            if (!user.IsActive)
            {
                return BadRequest(new { status = "error", message = "User is not active" });
            }

            // Send plain text message (no ticker logic, no formatting)
            var success = await _telegramService.SendPlainMessageAsync(request.ChatId, request.Message);

            if (success)
            {
                _logger.LogInformation("Plain message sent to user {ChatId}", request.ChatId);
                return Ok(new { status = "success", message = "Message sent successfully" });
            }
            else
            {
                return StatusCode(500, new { status = "error", message = "Failed to send message" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to user {ChatId}", request.ChatId);
            return StatusCode(500, new { status = "error", message = ex.Message });
        }
    }
}

public record AddUserRequest(long ChatId, string? Username, string? FirstName);
public record SendMessageRequest(long ChatId, string Message);
