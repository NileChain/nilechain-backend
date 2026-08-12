namespace NileChain.Application.Options;

/// <summary>
/// Escrow payments + platform wallet (Paymob top-up → pay milestones from balance).
/// </summary>
public class MockPaymentOptions
{
    public const string SectionName = "Payments";

    /// <summary>
    /// When true, factory uses escrow Pay CTA (wallet or legacy mock); offline mark-paid is rejected.
    /// </summary>
    public bool MockGatewayEnabled { get; set; } = true;

    /// <summary>
    /// When true: milestone pay debits NileChain wallet; release credits farm wallet.
    /// Top-up uses Paymob (or local simulator if keys missing).
    /// </summary>
    public bool WalletEnabled { get; set; } = true;

    /// <summary>
    /// Platform fee percent charged to the factory on top of each milestone
    /// (e.g. 2.5 → factory pays milestone + 2.5%, farm still receives full milestone).
    /// </summary>
    public decimal PlatformFeePercent { get; set; } = 2.5m;

    /// <summary>In demo, farm withdrawals complete immediately (treasury simulation).</summary>
    public bool InstantDemoWithdrawals { get; set; } = true;

    public decimal MinTopUpEgp { get; set; } = 50m;
    public decimal MaxTopUpEgp { get; set; } = 500_000m;
    public decimal MinWithdrawEgp { get; set; } = 50m;
}
