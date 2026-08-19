using NileChain.Application.Common;

namespace NileChain.Application.Errors;

public static class WalletErrors
{
    public static readonly Error NotFound =
        new("Wallet.NotFound", "Wallet was not found.");

    public static readonly Error InsufficientBalance =
        new(
            "Wallet.InsufficientBalance",
            "Factory wallet balance is too low for this deal. Top up the NileChain wallet, then try again.");

    public static readonly Error DealValueInvalid =
        new(
            "Wallet.DealValueInvalid",
            "Cannot sign: deal quantity and price must be positive to lock funds.");

    public static readonly Error InvalidAmount =
        new("Wallet.InvalidAmount", "Amount is outside the allowed range.");

    public static readonly Error TopUpFailed =
        new("Wallet.TopUpFailed", "Could not start wallet top-up.");

    public static readonly Error TopUpNotFound =
        new("Wallet.TopUpNotFound", "Top-up session was not found.");

    public static readonly Error TopUpInvalidState =
        new("Wallet.TopUpInvalidState", "Top-up is not in a payable state.");

    public static readonly Error PaymobNotConfigured =
        new(
            "Wallet.PaymobNotConfigured",
            "Payment gateway is not configured. Add Paymob keys under Paymob in appsettings.Development.local.json and restart the API.");

    public static readonly Error PaymobHmacInvalid =
        new("Wallet.PaymobHmacInvalid", "Payment confirmation signature is invalid.");

    public static readonly Error WithdrawFailed =
        new("Wallet.WithdrawFailed", "Withdrawal could not be completed.");

    public static readonly Error WithdrawalNotFound =
        new("Wallet.WithdrawalNotFound", "Withdrawal was not found.");

    public static readonly Error WithdrawalInvalidState =
        new("Wallet.WithdrawalInvalidState", "Withdrawal is not pending.");

    public static readonly Error Conflict =
        new("Wallet.Conflict", "Wallet changed concurrently — refresh and try again.");

    public static readonly Error Forbidden =
        new("Wallet.Forbidden", "You are not allowed to access this wallet.");

    public static readonly Error SubscriptionFarmNotApplicable =
        new(
            "Wallet.SubscriptionFarmNotApplicable",
            "The monthly NileChain plan is billed to factories. Farm accounts do not subscribe.");

    public static readonly Error SubscriptionInsufficient =
        new(
            "Wallet.SubscriptionInsufficient",
            "Available wallet balance is too low to pay this month's Pro plan. Top up the NileChain wallet, then try again.");
}
