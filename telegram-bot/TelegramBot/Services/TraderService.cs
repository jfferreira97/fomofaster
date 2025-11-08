using Microsoft.EntityFrameworkCore;
using TelegramBot.Data;
using TelegramBot.Models;

namespace TelegramBot.Services;

public class TraderService : ITraderService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<TraderService> _logger;

    public TraderService(AppDbContext dbContext, ILogger<TraderService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Trader?> GetTraderByHandleAsync(string handle)
    {
        return await _dbContext.Traders.FirstOrDefaultAsync(t => t.Handle == handle);
    }

    public async Task<List<Trader>> GetAllTradersAsync()
    {
        return await _dbContext.Traders.OrderBy(t => t.Handle).ToListAsync();
    }

    public async Task<Trader> AddOrUpdateTraderAsync(string handle)
    {
        var trader = await GetTraderByHandleAsync(handle);

        if (trader == null)
        {
            trader = new Trader
            {
                Handle = handle,
                FirstSeenAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow
            };

            _dbContext.Traders.Add(trader);
            _logger.LogInformation("New trader added: Handle={Handle}", handle);
        }
        else
        {
            trader.LastSeenAt = DateTime.UtcNow;
            _logger.LogInformation("Trader updated: Handle={Handle}", handle);
        }

        await _dbContext.SaveChangesAsync();
        return trader;
    }
}
