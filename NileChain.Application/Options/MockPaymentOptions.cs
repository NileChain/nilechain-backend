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
    /// Paymob-on-milestone sandbox. Production default false until legal sign-off.
    /// When true, webhook (or simulator) is the only paid truth; offline mark-paid is 409.
    /// </summary>
    public bool GatewayEnabled { get; set; }

    /// <summary>When true, factory must POST confirm-release after QC (P1 default).</summary>
    public bool ReleaseRequiresFactoryConfirm { get; set; } = true;

    /// <summary>
    /// When true: milestone pay debits NileChain wallet; release credits farm wallet.
    /// Top-up uses Paymob (or local simulator if keys missing).
    /// </summary>
    public bool WalletEnabled { get; set; } = true;

    /// <summary>
    /// Platform fee percent charged to the factory on top of each milestone
    /// (e.g. 2.5 → factory pays milestone + 2.5%, farm still receives full milestone).
    /// QC-after-hold refunds the fee delta so the keep is on GMV released after QC.
    /// </summary>
    public decimal PlatformFeePercent { get; set; } = 2.5m;

    /// <summary>Who is billed the take-rate. Locked: Factory.</summary>
    public string FeePayer { get; set; } = "Factory";

    /// <summary>GMV base for the take-rate. Locked: amount released after QC.</summary>
    public string FeeBase { get; set; } = "ReleasedAfterQc";

    /// <summary>In demo, farm withdrawals complete immediately (treasury simulation).</summary>
    public bool InstantDemoWithdrawals { get; set; }

    public decimal MinTopUpEgp { get; set; } = 50m;
    public decimal MaxTopUpEgp { get; set; } = 500_000m;
    public decimal MinWithdrawEgp { get; set; } = 50m;
}
