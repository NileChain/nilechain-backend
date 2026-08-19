using NileChain.Domain.Entities;
using NileChain.Domain.Enums;

namespace NileChain.Domain.Interfaces;

public interface IChannelMessageRepository
{
    Task AddAsync(ChannelMessage message);
    Task<IReadOnlyList<ChannelMessage>> ListRecentAsync(
        ChannelMessageStatus? status,
        int take = 100);
}
