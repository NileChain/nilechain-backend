using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NileChain.Application.Interfaces;
using NileChain.Application.Options;

namespace NileChain.Infrastructure.Paymob;

public sealed class PaymobClient : IPaymobClient
{
    private readonly HttpClient _http;
    private readonly PaymobOptions _options;
    private readonly ILogger<PaymobClient> _logger;

    public PaymobClient(HttpClient http, IOptions<PaymobOptions> options, ILogger<PaymobClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
        if (!string.IsNullOrWhiteSpace(_options.BaseUrl))
            _http.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
    }

    public bool IsConfigured =>
        _options.Enabled
        && !string.IsNullOrWhiteSpace(_options.SecretKey)
        && !string.IsNullOrWhiteSpace(_options.PublicKey)
        && _options.CardIntegrationId > 0;

    public string BuildCheckoutUrl(string clientSecret) =>
        $"{_options.BaseUrl.TrimEnd('/')}/unifiedcheckout/?publicKey={Uri.EscapeDataString(_options.PublicKey)}&clientSecret={Uri.EscapeDataString(clientSecret)}";

    public async Task<PaymobIntentionResult> CreateIntentionAsync(
        PaymobIntentionRequest request,
        CancellationToken ct = default)
    {
        if (!IsConfigured)
            return new PaymobIntentionResult { Success = false, Error = "Paymob is not configured." };

        var cents = (int)Math.Round(request.AmountEgp * 100m, MidpointRounding.AwayFromZero);
        var body = new
        {
            amount = cents,
            currency = "EGP",
            payment_methods = new object[] { _options.CardIntegrationId },
            items = new[]
            {
                new
                {
                    name = string.IsNullOrWhiteSpace(request.ItemName)
                        ? "NileChain wallet top-up"
                        : request.ItemName,
                    amount = cents,
                    description = request.SpecialReference,
                    quantity = 1
                }
            },
            billing_data = new
            {
                first_name = request.FirstName,
                last_name = request.LastName,
                phone_number = request.Phone,
                email = request.Email,
                street = "N/A",
                building = "N/A",
                floor = "N/A",
                apartment = "N/A",
                city = "Cairo",
                country = "EGY",
                state = "Cairo"
            },
            special_reference = request.SpecialReference,
            notification_url = request.NotificationUrl,
            redirection_url = request.RedirectionUrl,
            extras = BuildExtras(request)
        };

        using var msg = new HttpRequestMessage(HttpMethod.Post, "v1/intention/");
        msg.Headers.Authorization = new AuthenticationHeaderValue("Token", _options.SecretKey);
        msg.Content = JsonContent.Create(body);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(msg, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Paymob intention HTTP failure");
            return new PaymobIntentionResult { Success = false, Error = ex.Message };
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Paymob intention failed {Status}: {Body}", (int)response.StatusCode, json);
            return new PaymobIntentionResult
            {
                Success = false,
                Error = $"Paymob {(int)response.StatusCode}: {Truncate(json, 400)}",
                RawJson = json
            };
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var clientSecret = root.TryGetProperty("client_secret", out var cs) ? cs.GetString() : null;
        var intentionId = root.TryGetProperty("id", out var idEl)
            ? idEl.ValueKind == JsonValueKind.Number ? idEl.GetRawText() : idEl.GetString()
            : null;
        var orderId = root.TryGetProperty("intention_order_id", out var oid)
            ? oid.ValueKind == JsonValueKind.Number ? oid.GetRawText() : oid.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(clientSecret))
            return new PaymobIntentionResult { Success = false, Error = "Paymob response missing client_secret.", RawJson = json };

        return new PaymobIntentionResult
        {
            Success = true,
            IntentionId = intentionId,
            ClientSecret = clientSecret,
            OrderId = orderId,
            CheckoutUrl = BuildCheckoutUrl(clientSecret),
            RawJson = json
        };
    }

    public bool VerifyHmac(IReadOnlyDictionary<string, string?> obj, string receivedHmac)
    {
        if (string.IsNullOrWhiteSpace(_options.HmacSecret) || string.IsNullOrWhiteSpace(receivedHmac))
            return false;

        var normalized = NormalizePaymobFields(obj);
        var received = receivedHmac.Trim().ToLowerInvariant();

        // Try both GET (order_id) and POST (order) value slots, and both
        // "always append" vs "skip missing" concat styles used across Paymob docs.
        foreach (var useOrderIdKey in new[] { true, false })
        {
            foreach (var skipMissing in new[] { false, true })
            {
                var payload = BuildHmacPayload(normalized, useOrderIdKey, skipMissing);
                if (MatchesHmac(payload, received))
                    return true;
            }
        }

        return false;
    }

    private bool MatchesHmac(string payload, string receivedHex)
    {
        var hash = HMACSHA512.HashData(
            Encoding.UTF8.GetBytes(_options.HmacSecret),
            Encoding.UTF8.GetBytes(payload));
        var computed = Convert.ToHexString(hash).ToLowerInvariant();
        if (computed.Length != receivedHex.Length)
            return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computed),
            Encoding.UTF8.GetBytes(receivedHex));
    }

    private static string BuildHmacPayload(
        IReadOnlyDictionary<string, string?> obj,
        bool preferOrderId,
        bool skipMissing)
    {
        // Fixed Paymob field order. Redirect uses order_id; webhook uses order.
        var orderVal = preferOrderId
            ? (Lookup(obj, "order_id") ?? Lookup(obj, "order"))
            : (Lookup(obj, "order") ?? Lookup(obj, "order_id"));

        var fields = new (string Key, string? Value)[]
        {
            ("amount_cents", Lookup(obj, "amount_cents")),
            ("created_at", Lookup(obj, "created_at")),
            ("currency", Lookup(obj, "currency")),
            ("error_occured", Lookup(obj, "error_occured")),
            ("has_parent_transaction", Lookup(obj, "has_parent_transaction")),
            ("id", Lookup(obj, "id")),
            ("integration_id", Lookup(obj, "integration_id")),
            ("is_3d_secure", Lookup(obj, "is_3d_secure")),
            ("is_auth", Lookup(obj, "is_auth")),
            ("is_capture", Lookup(obj, "is_capture")),
            ("is_refunded", Lookup(obj, "is_refunded")),
            ("is_standalone_payment", Lookup(obj, "is_standalone_payment")),
            ("is_voided", Lookup(obj, "is_voided")),
            ("order", orderVal),
            ("owner", Lookup(obj, "owner")),
            ("pending", Lookup(obj, "pending")),
            ("source_data_pan", LookupSourcePan(obj)),
            ("source_data_sub_type", LookupSourceSubType(obj)),
            ("source_data_type", LookupSourceType(obj)),
            ("success", Lookup(obj, "success")),
        };

        var sb = new StringBuilder();
        foreach (var (_, val) in fields)
        {
            if (skipMissing && string.IsNullOrEmpty(val))
                continue;
            sb.Append(val ?? string.Empty);
        }
        return sb.ToString();
    }

    private static Dictionary<string, string?> NormalizePaymobFields(IReadOnlyDictionary<string, string?> obj)
    {
        var d = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in obj)
        {
            if (string.IsNullOrWhiteSpace(kv.Key))
                continue;
            // Ignore our own return markers — not part of Paymob HMAC.
            if (string.Equals(kv.Key, "topUpId", StringComparison.OrdinalIgnoreCase)
                || string.Equals(kv.Key, "hmac", StringComparison.OrdinalIgnoreCase)
                || string.Equals(kv.Key, "nilechain_escrow", StringComparison.OrdinalIgnoreCase)
                || string.Equals(kv.Key, "nilechain_topup", StringComparison.OrdinalIgnoreCase))
                continue;
            d[kv.Key] = kv.Value?.Trim();
        }

        foreach (var boolKey in new[]
                 {
                     "error_occured", "has_parent_transaction", "is_3d_secure", "is_auth",
                     "is_capture", "is_refunded", "is_standalone_payment", "is_voided",
                     "pending", "success"
                 })
        {
            if (d.TryGetValue(boolKey, out var v) && !string.IsNullOrEmpty(v))
                d[boolKey] = v.ToLowerInvariant();
        }

        return d;
    }

    private static string? LookupSourcePan(IReadOnlyDictionary<string, string?> obj) =>
        Lookup(obj, "source_data_pan")
        ?? Lookup(obj, "source_data.pan")
        ?? Lookup(obj, "source_data[pan]");

    private static string? LookupSourceSubType(IReadOnlyDictionary<string, string?> obj) =>
        Lookup(obj, "source_data_sub_type")
        ?? Lookup(obj, "source_data.sub_type")
        ?? Lookup(obj, "source_data[sub_type]");

    private static string? LookupSourceType(IReadOnlyDictionary<string, string?> obj) =>
        Lookup(obj, "source_data_type")
        ?? Lookup(obj, "source_data.type")
        ?? Lookup(obj, "source_data[type]");

    private static string? Lookup(IReadOnlyDictionary<string, string?> obj, string key)
    {
        if (obj.TryGetValue(key, out var val))
            return val;
        foreach (var kv in obj)
        {
            if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
                return kv.Value;
        }
        return null;
    }

    private static object BuildExtras(PaymobIntentionRequest request) =>
        string.Equals(request.ExtraKind, "nilechain_escrow", StringComparison.OrdinalIgnoreCase)
            ? new { nilechain_escrow = request.SpecialReference }
            : new { nilechain_topup = request.SpecialReference };

    private static string Truncate(string s, int n) =>
        string.IsNullOrEmpty(s) ? string.Empty : (s.Length <= n ? s : s[..n] + "…");
}
