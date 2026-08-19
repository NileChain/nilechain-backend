using NileChain.Application.Common;

namespace NileChain.Application.Errors;

public static class MockEscrowErrors
{
    public static readonly Error GatewayDisabled =
        new("MockEscrow.GatewayDisabled", "Mock payment gateway is disabled.");

    public static readonly Error UseMockPay =
        new(
            "MockEscrow.UseMockPay",
            "Offline mark-paid is disabled while mock escrow is enabled. Use Pay (Demo) instead.");

    public static readonly Error OfflineMarkPaidConflict =
        new(
            "MockEscrow.OfflineMarkPaidConflict",
            "Offline mark-paid is disabled while the payment gateway is enabled. Pay securely via the gateway session.");

    public static readonly Error WebhookRequiredConflict =
        new(
            "MockEscrow.WebhookRequiredConflict",
            "Browser confirm-paid is ignored. Funds are held only after the payment webhook (or sandbox simulator).");

    public static readonly Error ContractNotSignedConflict =
        new(
            "MockEscrow.ContractNotSignedConflict",
            "Paymob milestone pay requires a fully signed contract.");

    public static readonly Error NotFound =
        new("MockEscrow.NotFound", "Escrow transaction was not found.");

    public static readonly Error InvalidState =
        new("MockEscrow.InvalidState", "Escrow is not in a state that allows this action.");

    public static readonly Error ReleaseNotReady =
        new(
            "MockEscrow.ReleaseNotReady",
            "Release requires delivery received (or QC) for non-deposit milestones, and no open dispute.");

    public static readonly Error Forbidden =
        new("MockEscrow.Forbidden", "You are not allowed to perform this escrow action.");

    public static readonly Error Conflict =
        new("MockEscrow.Conflict", "Escrow changed concurrently — refresh and try again.");

    public static readonly Error MilestoneNotPayable =
        new("MockEscrow.MilestoneNotPayable", "Only pending milestones can be paid via mock escrow.");

    public static readonly Error CannotUnwindAfterReceive =
        new(
            "MockEscrow.CannotUnwindAfterReceive",
            "A signed contract can only be cancelled before the factory marks goods received. Open a dispute after receipt.");

    public static readonly Error UnwindBlockedByDispute =
        new(
            "MockEscrow.UnwindBlockedByDispute",
            "Resolve or reject the open dispute before cancelling this signed contract.");
}
