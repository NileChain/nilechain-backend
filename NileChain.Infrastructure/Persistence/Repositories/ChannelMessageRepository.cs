using Microsoft.EntityFrameworkCore;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Interfaces;

namespace NileChain.Infrastructure.Persistence.Repositories;

public sealed class ChannelMessageRepository : IChannelMessageRepository
{
    private readonly NileChainDbContext _db;

    public ChannelMessageRepository(NileChainDbContext db) => _db = db;

    public Task AddAsync(ChannelMessage message) =>
        _db.ChannelMessages.AddAsync(message).AsTask();

    public async Task<IReadOnlyList<ChannelMessage>> ListRecentAsync(
        ChannelMessageStatus? status,
        int take = 100)
    {
        take = Math.Clamp(take, 1, 500);
        var query = _db.ChannelMessages.AsNoTracking().AsQueryable();
        if (status is not null)
            query = query.Where(m => m.Status == status.Value);

        return await query
            .OrderByDescending(m => m.CreatedAt)
            .Take(take)
            .ToListAsync();
    }
}
