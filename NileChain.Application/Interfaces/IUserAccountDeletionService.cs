using NileChain.Application.Common;

namespace NileChain.Application.Interfaces;

public interface IUserAccountDeletionService
{
    Task<Result> DeleteUserAccountAsync(Guid userId, CancellationToken cancellationToken = default);
}
