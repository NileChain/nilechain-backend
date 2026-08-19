using System.Security.Claims;
using System.Text.Json;
using NileChain.API.Extensions;
using NileChain.Application.Dtos.Wallet;
using NileChain.Application.Interfaces;
using NileChain.Application.Services;
using NileChain.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NileChain.API.Controllers;

[Route("api/wallet")]
[ApiController]
[Authorize(Roles = $"{AppRoles.Factory},{AppRoles.Farm}")]
public class WalletController : ControllerBase
{
    private readonly IWalletService _wallets;

    public WalletController(IWalletService wallets) => _wallets = wallets;

    [HttpGet]
    public async Task<IActionResult> GetMine()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();
        var result = await _wallets.GetMineAsync(Guid.Parse(userId), AsFarm());
        return result.ToActionResult();
    }

    [HttpPost("top-up")]
    public async Task<IActionResult> StartTopUp([FromBody] StartWalletTopUpRequest body)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();
        var result = await _wallets.StartTopUpAsync(
            Guid.Parse(userId),
            AsFarm(),
            body.AmountEgp,
            body.IdempotencyKey,
            body.ReturnUrl);
        return result.ToActionResult();
    }

    [HttpPost("top-up/{topUpId:guid}/complete-simulator")]
    public async Task<IActionResult> CompleteSimulator(Guid topUpId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();
        var result = await _wallets.CompleteSimulatorTopUpAsync(Guid.Parse(userId), AsFarm(), topUpId);
        return result.ToActionResult();
    }

    /// <summary>
    /// Apply Paymob top-up after browser redirect (HMAC-verified query params).
    /// Needed locally when Paymob cannot POST the webhook to localhost.
    /// </summary>
    [HttpPost("top-up/confirm-paymob")]
    public async Task<IActionResult> ConfirmPaymob([FromBody] ConfirmPaymobTopUpRequest body)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var query = body.Query ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var result = await _wallets.ConfirmPaymobReturnAsync(
            Guid.Parse(userId),
            AsFarm(),
            query,
            body.Hmac);
        return result.ToActionResult();
    }

    [HttpPost("withdraw")]
    public async Task<IActionResult> Withdraw([FromBody] WalletWithdrawRequest body)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();
        var result = await _wallets.RequestWithdrawalAsync(
            Guid.Parse(userId),
            AsFarm(),
            body.AmountEgp,
            body.Method,
            body.DestinationSummary);
        return result.ToActionResult();
    }

    private bool AsFarm() => User.IsInRole(AppRoles.Farm);
}

[Route("api/webhooks/paymob")]
[ApiController]
[AllowAnonymous]
public class PaymobWebhookController : ControllerBase
{
    private readonly IWalletService _wallets;
    private readonly IMockEscrowPaymentService _escrow;
    private readonly IPaymobClient _paymob;
    private readonly ILogger<PaymobWebhookController> _logger;

    public PaymobWebhookController(
        IWalletService wallets,
        IMockEscrowPaymentService escrow,
        IPaymobClient paymob,
        ILogger<PaymobWebhookController> logger)
    {
        _wallets = wallets;
        _escrow = escrow;
        _paymob = paymob;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Receive()
    {
        using var doc = await JsonDocument.ParseAsync(Request.Body);
        var root = doc.RootElement;

        // Paymob may send { type, obj } or flat transaction object
        var obj = root.TryGetProperty("obj", out var nested) ? nested : root;
        var hmac = Request.Query["hmac"].FirstOrDefault()
                   ?? (root.TryGetProperty("hmac", out var h) ? h.GetString() : null);

        var flat = Flatten(obj);
        if (!_paymob.IsConfigured)
        {
            _logger.LogWarning("Paymob webhook rejected: Paymob is not configured");
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        if (string.IsNullOrWhiteSpace(hmac) || !_paymob.VerifyHmac(flat, hmac))
        {
            _logger.LogWarning("Paymob webhook HMAC missing or invalid");
            return Unauthorized();
        }

        var success = flat.TryGetValue("success", out var s) &&
                      string.Equals(s, "true", StringComparison.OrdinalIgnoreCase);
        var special = flat.GetValueOrDefault("merchant_order_id")
                      ?? flat.GetValueOrDefault("special_reference")
                      ?? ExtractSpecialReference(obj);
        var txnId = flat.GetValueOrDefault("id");
        var orderId = flat.GetValueOrDefault("order");

        if (string.IsNullOrWhiteSpace(special))
        {
            // Intention extras / order merchant order id
            special = TryReadPath(obj, "order", "merchant_order_id")
                      ?? TryReadPath(obj, "payment_key_claims", "extra", "nilechain_escrow")
                      ?? TryReadPath(obj, "payment_key_claims", "extra", "nilechain_topup");
        }

        if (string.IsNullOrWhiteSpace(special))
        {
            _logger.LogWarning("Paymob webhook missing special reference");
            return Ok(new { received = true, applied = false });
        }

        if (MockEscrowPaymentService.TryParseEscrowSpecial(special, out _))
        {
            var escrowResult = await _escrow.ApplyPaymobEscrowWebhookAsync(special, txnId, orderId, success);
            if (escrowResult.IsFailure)
                _logger.LogWarning("Paymob escrow apply failed: {Code}", escrowResult.Error?.Code);
            return Ok(new { received = true, applied = escrowResult.IsSuccess });
        }

        var result = await _wallets.ApplyPaymobTopUpSuccessAsync(special, txnId, orderId, success);
        if (result.IsFailure)
            _logger.LogWarning("Paymob top-up apply failed: {Code}", result.Error?.Code);

        return Ok(new { received = true, applied = result.IsSuccess });
    }

    private static Dictionary<string, string?> Flatten(JsonElement obj)
    {
        var d = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in obj.EnumerateObject())
        {
            if (p.NameEquals("source_data") && p.Value.ValueKind == JsonValueKind.Object)
            {
                foreach (var s in p.Value.EnumerateObject())
                    d[$"source_data_{s.Name}"] = Scalar(s.Value);
                continue;
            }

            if (p.NameEquals("order") && p.Value.ValueKind == JsonValueKind.Object)
            {
                d["order"] = p.Value.TryGetProperty("id", out var oid) ? Scalar(oid) : null;
                continue;
            }

            d[p.Name] = Scalar(p.Value);
        }
        return d;
    }

    private static string? Scalar(JsonElement e) =>
        e.ValueKind switch
        {
            JsonValueKind.String => e.GetString(),
            JsonValueKind.Number => e.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => null,
            _ => e.GetRawText()
        };

    private static string? ExtractSpecialReference(JsonElement obj)
    {
        if (obj.TryGetProperty("merchant_order_id", out var m))
            return Scalar(m);
        return null;
    }

    private static string? TryReadPath(JsonElement root, params string[] path)
    {
        var cur = root;
        foreach (var p in path)
        {
            if (cur.ValueKind != JsonValueKind.Object || !cur.TryGetProperty(p, out cur))
                return null;
        }
        return Scalar(cur);
    }
}
