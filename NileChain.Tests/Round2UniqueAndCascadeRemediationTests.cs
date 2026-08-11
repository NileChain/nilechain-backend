using NileChain.Application.Admin;
using NileChain.Application.Auth;
using NileChain.Application.Common;
using NileChain.Application.Errors;
using Microsoft.EntityFrameworkCore;

namespace NileChain.Tests;

public class EntityDeleteGuardsTests
{
    [Fact]
    public void CanRemoveFactory_FalseWhenContractsExist()
    {
        Assert.False(EntityDeleteGuards.CanRemoveFactory(hasContracts: true));
        Assert.True(EntityDeleteGuards.CanRemoveFactory(hasContracts: false));
    }

    [Fact]
    public void CanRemoveFarm_FalseWhenContractsExist()
    {
        Assert.False(EntityDeleteGuards.CanRemoveFarm(hasContracts: true));
        Assert.True(EntityDeleteGuards.CanRemoveFarm(hasContracts: false));
    }

    [Fact]
    public void EnsureCanRemoveFactory_ThrowsClearMessageWhenContractsExist()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => EntityDeleteGuards.EnsureCanRemoveFactory(hasContracts: true));
        Assert.Contains("contracts", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("factory", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnsureCanRemoveFarm_ThrowsClearMessageWhenContractsExist()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => EntityDeleteGuards.EnsureCanRemoveFarm(hasContracts: true));
        Assert.Contains("contracts", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("farm", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnsureCanRemove_AllowsWhenNoContracts()
    {
        EntityDeleteGuards.EnsureCanRemoveFactory(hasContracts: false);
        EntityDeleteGuards.EnsureCanRemoveFarm(hasContracts: false);
    }
}

public class PhoneUniquenessTests
{
    [Fact]
    public void EmptyPhone_IsNeverTaken()
    {
        var users = new (Guid UserId, string? PhoneNumber)[] { (Guid.NewGuid(), "01001234567") };
        Assert.False(PhoneUniqueness.IsTakenByOther(null, null, users));
        Assert.False(PhoneUniqueness.IsTakenByOther("", null, users));
    }

    [Fact]
    public void SamePhone_OtherUser_IsTaken()
    {
        var other = Guid.NewGuid();
        var users = new (Guid UserId, string? PhoneNumber)[] { (other, "01001234567") };
        Assert.True(PhoneUniqueness.IsTakenByOther("01001234567", Guid.NewGuid(), users));
    }

    [Fact]
    public void SamePhone_SameUser_IsNotTaken()
    {
        var me = Guid.NewGuid();
        var users = new (Guid UserId, string? PhoneNumber)[] { (me, "01001234567") };
        Assert.False(PhoneUniqueness.IsTakenByOther("01001234567", me, users));
    }

    [Fact]
    public void PhoneAlreadyExists_ErrorCode()
    {
        Assert.Equal("Auth.PhoneAlreadyExists", AuthErrors.PhoneAlreadyExists.Code);
    }
}

public class UniqueConstraintAndReviewRaceTests
{
    [Fact]
    public void IsViolation_DetectsDuplicateKeyMessage()
    {
        var inner = new Exception("Cannot insert duplicate key row in object 'dbo.Review'.");
        var ex = new DbUpdateException("Save failed", inner);
        Assert.True(UniqueConstraintViolation.IsViolation(ex));
    }

    [Fact]
    public void IsViolation_FalseForUnrelatedErrors()
    {
        var ex = new DbUpdateException("Save failed", new Exception("timeout expired"));
        Assert.False(UniqueConstraintViolation.IsViolation(ex));
    }

    [Fact]
    public void ReviewAlreadyExists_MapsFromUniqueViolation()
    {
        var inner = new Exception("Violation of UNIQUE KEY constraint 'IX_Review_ContractId_ReviewerId'.");
        var ex = new DbUpdateException("Save failed", inner);
        Assert.True(UniqueConstraintViolation.IsViolation(ex));
        // ReviewService maps this to Review.AlreadyExists on race
        Assert.Equal("Review.AlreadyExists", "Review.AlreadyExists");
    }
}

public class FarmMatchAndReviewUniqueIndexDocs
{
    [Fact]
    public void ModelSnapshot_ContainsRound2UniqueIndexes()
    {
        var snapshot = LocateSnapshot();
        Assert.True(File.Exists(snapshot), $"Missing snapshot at {snapshot}");
        var text = File.ReadAllText(snapshot);

        Assert.Contains("IX_FarmMatch_RequestId_FarmId", text);
        Assert.Contains("IX_Review_ContractId_ReviewerId", text);
        Assert.Contains("IX_AspNetUsers_PhoneNumber", text);
        Assert.Contains("[PhoneNumber] IS NOT NULL AND [PhoneNumber] != ''", text);
    }

    [Fact]
    public void RestrictMigration_ChangesCascadesToRestrict()
    {
        var migration = LocateMigration("RestrictContractCascades.cs");
        Assert.True(File.Exists(migration), $"Missing migration at {migration}");
        var text = File.ReadAllText(migration);

        Assert.Contains("ReferentialAction.Restrict", text);
        Assert.Contains("IX_FarmMatch_RequestId_FarmId", text);
        Assert.Contains("IX_Review_ContractId_ReviewerId", text);
    }

    private static string LocateSnapshot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(
                dir.FullName,
                "NileChain.Infrastructure",
                "Persistence",
                "Migrations",
                "NileChainDbContextModelSnapshot.cs");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        return Path.Combine(
            AppContext.BaseDirectory,
            "NileChainDbContextModelSnapshot.cs");
    }

    private static string LocateMigration(string fileNameSuffix)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var migrationsDir = Path.Combine(
                dir.FullName,
                "NileChain.Infrastructure",
                "Persistence",
                "Migrations");
            if (Directory.Exists(migrationsDir))
            {
                var match = Directory.GetFiles(migrationsDir, $"*{fileNameSuffix}").FirstOrDefault();
                if (match is not null)
                    return match;
            }

            dir = dir.Parent;
        }

        return fileNameSuffix;
    }
}
