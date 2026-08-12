namespace NileChain.Domain.Enums;

public enum WalletOwnerType
{
    Factory = 1,
    Farm = 2,
    Platform = 3
}

public enum WalletLedgerType
{
    TopUp = 1,
    MilestoneHold = 2,
    MilestoneReleaseToFarm = 3,
    PlatformFee = 4,
    Withdrawal = 5,
    RefundToFactory = 6,
    Adjustment = 7,
    /// <summary>Full deal amount held when both parties signed the contract.</summary>
    ContractDealHold = 8
}

public enum WalletTopUpStatus
{
    Created = 0,
    Pending = 1,
    Paid = 2,
    Failed = 3,
    Expired = 4
}

public enum WalletWithdrawalStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4
}
