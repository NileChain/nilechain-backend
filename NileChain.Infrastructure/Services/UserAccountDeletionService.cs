using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NileChain.Application.Common;
using NileChain.Application.Errors;
using NileChain.Application.Interfaces;
using NileChain.Domain.Enums;
using NileChain.Domain.Identity;
using NileChain.Infrastructure.Persistence;

namespace NileChain.Infrastructure.Services;

public sealed class UserAccountDeletionService : IUserAccountDeletionService
{
    private readonly NileChainDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public UserAccountDeletionService(
        NileChainDbContext db,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<Result> DeleteUserAccountAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return Result.Failure(AdminErrors.UserNotFound);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await DeleteUserScopedDataAsync(userId, cancellationToken);

            var factoryId = await _db.Factory
                .Where(f => f.UserId == userId)
                .Select(f => f.FactoryId)
                .FirstOrDefaultAsync(cancellationToken);
            if (factoryId != Guid.Empty)
                await DeleteFactoryGraphAsync(factoryId, cancellationToken);

            var farmId = await _db.Farm
                .Where(f => f.UserId == userId)
                .Select(f => f.FarmId)
                .FirstOrDefaultAsync(cancellationToken);
            if (farmId != Guid.Empty)
                await DeleteFarmGraphAsync(farmId, cancellationToken);

            await _db.RefreshTokens
                .Where(t => t.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            var deleteResult = await _userManager.DeleteAsync(user);
            if (!deleteResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                var reason = deleteResult.Errors.FirstOrDefault()?.Description
                    ?? "Could not delete the user account.";
                return Result.Failure(new Error("Admin.DeleteUserFailed", reason));
            }

            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(new Error(
                "Admin.DeleteUserFailed",
                ExtractMessage(ex)));
        }
    }

    private async Task DeleteUserScopedDataAsync(Guid userId, CancellationToken cancellationToken)
    {
        await TryExecuteDeleteAsync(
            () => _db.KybDecisions
                .Where(d => d.UserId == userId || d.AdminUserId == userId)
                .ExecuteDeleteAsync(cancellationToken),
            "KybDecision");

        await TryExecuteDeleteAsync(
            () => _db.KybVerificationReports
                .Where(r => r.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken),
            "KybVerificationReport");

        await _db.SigningOtps
            .Where(o => o.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        await _db.Messages
            .Where(m => m.SenderId == userId || m.ReceiverId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        await _db.Reviews
            .Where(r => r.ReviewerId == userId || r.TargetId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        await _db.CropRequests
            .Where(c => c.RequestedByUserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        await _db.CropRequests
            .Where(c => c.ReviewedByUserId == userId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(c => c.ReviewedByUserId, (Guid?)null),
                cancellationToken);

        await _db.Disputes
            .Where(d => d.RaisedByUserId == userId
                || d.ReviewedByUserId == userId
                || d.ResolvedByUserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        await _db.ContractSignatureRecords
            .Where(s => s.SignerId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        await _db.ContractAuditLogs
            .Where(a => a.ActorId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        await _db.FulfillmentEvents
            .Where(e => e.ActorUserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        await _db.DisputeEvents
            .Where(e => e.ActorUserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        await _db.TransactionEvents
            .Where(e => e.ActorUserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        await _db.RagDocuments
            .Where(r => r.UploadedBy == userId)
            .ExecuteDeleteAsync(cancellationToken);

        await _db.Notifications
            .Where(n => n.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task DeleteFactoryGraphAsync(Guid factoryId, CancellationToken cancellationToken)
    {
        var requestIds = await _db.SupplyRequests
            .Where(r => r.FactoryId == factoryId)
            .Select(r => r.RequestId)
            .ToListAsync(cancellationToken);

        foreach (var requestId in requestIds)
            await DeleteSupplyRequestGraphAsync(requestId, cancellationToken);

        await DeleteWalletAsync(WalletOwnerType.Factory, factoryId, cancellationToken);

        await _db.FactoryDocuments
            .Where(d => d.FactoryId == factoryId)
            .ExecuteDeleteAsync(cancellationToken);

        await _db.Factory
            .Where(f => f.FactoryId == factoryId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task DeleteFarmGraphAsync(Guid farmId, CancellationToken cancellationToken)
    {
        var matchIds = await _db.FarmMatches
            .Where(m => m.FarmId == farmId)
            .Select(m => m.MatchId)
            .ToListAsync(cancellationToken);

        foreach (var matchId in matchIds)
            await DeleteMatchGraphAsync(matchId, cancellationToken);

        await DeleteWalletAsync(WalletOwnerType.Farm, farmId, cancellationToken);

        await _db.FarmDocuments
            .Where(d => d.FarmId == farmId)
            .ExecuteDeleteAsync(cancellationToken);

        await _db.FarmImages
            .Where(i => i.FarmId == farmId)
            .ExecuteDeleteAsync(cancellationToken);

        await _db.FarmCrops
            .Where(c => c.FarmId == farmId)
            .ExecuteDeleteAsync(cancellationToken);

        await _db.FarmCertifications
            .Where(c => c.FarmId == farmId)
            .ExecuteDeleteAsync(cancellationToken);

        await _db.RiskAssessmentReports
            .Where(r => r.FarmId == farmId)
            .ExecuteDeleteAsync(cancellationToken);

        await _db.Farm
            .Where(f => f.FarmId == farmId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task DeleteSupplyRequestGraphAsync(Guid requestId, CancellationToken cancellationToken)
    {
        var matchIds = await _db.FarmMatches
            .Where(m => m.RequestId == requestId)
            .Select(m => m.MatchId)
            .ToListAsync(cancellationToken);

        foreach (var matchId in matchIds)
            await DeleteMatchGraphAsync(matchId, cancellationToken);

        await _db.AgentRuns
            .Where(r => r.RequestId == requestId)
            .ExecuteDeleteAsync(cancellationToken);

        await _db.ComparisonReports
            .Where(r => r.RequestId == requestId)
            .ExecuteDeleteAsync(cancellationToken);

        await _db.SupplyRequests
            .Where(r => r.RequestId == requestId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task DeleteMatchGraphAsync(Guid matchId, CancellationToken cancellationToken)
    {
        var contractId = await _db.Contracts
            .Where(c => c.MatchId == matchId)
            .Select(c => c.ContractId)
            .FirstOrDefaultAsync(cancellationToken);

        if (contractId != Guid.Empty)
            await DeleteContractGraphAsync(contractId, cancellationToken);

        await _db.FarmMatches
            .Where(m => m.MatchId == matchId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task DeleteContractGraphAsync(Guid contractId, CancellationToken cancellationToken)
    {
        await _db.Disputes
            .Where(d => d.ContractId == contractId)
            .ExecuteDeleteAsync(cancellationToken);

        await _db.Fulfillments
            .Where(f => f.ContractId == contractId)
            .ExecuteDeleteAsync(cancellationToken);

        await _db.EscrowTransactions
            .Where(e => e.ContractId == contractId)
            .ExecuteDeleteAsync(cancellationToken);

        await _db.ContractSignatureRecords
            .Where(s => s.ContractId == contractId)
            .ExecuteDeleteAsync(cancellationToken);

        await _db.ContractAuditLogs
            .Where(a => a.ContractId == contractId)
            .ExecuteDeleteAsync(cancellationToken);

        await _db.ContractIntegrityAnchors
            .Where(a => a.ContractId == contractId)
            .ExecuteDeleteAsync(cancellationToken);

        await _db.SigningOtps
            .Where(o => o.ContractId == contractId)
            .ExecuteDeleteAsync(cancellationToken);

        var contract = await _db.Contracts
            .Include(c => c.RagDocuments)
            .FirstOrDefaultAsync(c => c.ContractId == contractId, cancellationToken);
        if (contract is not null)
        {
            contract.RagDocuments.Clear();
            await _db.SaveChangesAsync(cancellationToken);
        }

        await _db.Contracts
            .Where(c => c.ContractId == contractId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task DeleteWalletAsync(
        WalletOwnerType ownerType,
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        var walletId = await _db.Wallets
            .Where(w => w.OwnerType == ownerType && w.OwnerId == ownerId)
            .Select(w => w.WalletId)
            .FirstOrDefaultAsync(cancellationToken);

        if (walletId == Guid.Empty)
            return;

        await _db.WalletWithdrawals
            .Where(w => w.WalletId == walletId)
            .ExecuteDeleteAsync(cancellationToken);

        await _db.WalletTopUps
            .Where(t => t.WalletId == walletId)
            .ExecuteDeleteAsync(cancellationToken);

        await _db.WalletLedgerEntries
            .Where(e => e.WalletId == walletId)
            .ExecuteDeleteAsync(cancellationToken);

        await _db.Wallets
            .Where(w => w.WalletId == walletId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static string ExtractMessage(Exception ex) =>
        ex.InnerException?.Message ?? ex.Message;

    private static async Task TryExecuteDeleteAsync(
        Func<Task> delete,
        string tableName)
    {
        try
        {
            await delete();
        }
        catch (Exception ex) when (IsMissingTable(ex, tableName))
        {
            // Optional table not migrated yet — safe to skip for admin test cleanup.
        }
    }

    private static bool IsMissingTable(Exception ex, string tableName)
    {
        var message = ExtractMessage(ex);
        return message.Contains("Invalid object name", StringComparison.OrdinalIgnoreCase)
            && message.Contains(tableName, StringComparison.OrdinalIgnoreCase);
    }
}
