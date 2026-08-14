using System.Globalization;
using System.Text.RegularExpressions;

namespace NileChain.Domain.Common;

/// <summary>
/// Contract effective term dates: start = last bilateral signature; end = delivery deadline.
/// Also fills Arabic/English placeholders left by LLM drafts.
/// </summary>
public static partial class ContractTermDates
{
    private static readonly CultureInfo ArEg = CultureInfo.GetCultureInfo("ar-EG");

    /// <summary>
    /// After both parties sign: set <see cref="Entities.Contract.StartsAt"/> /
    /// <see cref="Entities.Contract.EndsAt"/> once, and materialize placeholders in generated text.
    /// </summary>
    public static void ApplyOnFullSign(Entities.Contract contract, DateTime? deliveryDate)
    {
        ArgumentNullException.ThrowIfNull(contract);
        if (!contract.IsFullySigned)
            return;

        contract.StartsAt ??= NormalizeDate(contract.SignedAt ?? DateTime.UtcNow);
        if (contract.EndsAt is null && deliveryDate.HasValue)
            contract.EndsAt = NormalizeDate(deliveryDate.Value);

        contract.GeneratedText = FillPlaceholders(
            contract.GeneratedText,
            contract.StartsAt,
            contract.EndsAt);
    }

    /// <summary>
    /// Lazy repair for already-signed contracts that still show bracket placeholders.
    /// </summary>
    public static void EnsureApplied(Entities.Contract contract, DateTime? deliveryDate)
    {
        ArgumentNullException.ThrowIfNull(contract);
        if (!contract.IsFullySigned)
            return;

        var missingDates = contract.StartsAt is null || contract.EndsAt is null;
        var textHasPlaceholder =
            !string.IsNullOrWhiteSpace(contract.GeneratedText)
            && PlaceholderRegex().IsMatch(contract.GeneratedText);

        if (!missingDates && !textHasPlaceholder)
            return;

        ApplyOnFullSign(contract, deliveryDate ?? contract.EndsAt);
    }

    public static void ApplyAmendment(
        Entities.Contract contract,
        DateTime? startsAt,
        DateTime? endsAt)
    {
        ArgumentNullException.ThrowIfNull(contract);
        if (startsAt.HasValue)
            contract.StartsAt = NormalizeDate(startsAt.Value);
        if (endsAt.HasValue)
            contract.EndsAt = NormalizeDate(endsAt.Value);

        contract.GeneratedText = FillPlaceholders(
            contract.GeneratedText,
            contract.StartsAt,
            contract.EndsAt);

        ClearPendingAmendment(contract);
    }

    public static void ProposeAmendment(
        Entities.Contract contract,
        Guid proposedByUserId,
        DateTime? pendingStartsAt,
        DateTime? pendingEndsAt)
    {
        ArgumentNullException.ThrowIfNull(contract);
        if (pendingStartsAt is null && pendingEndsAt is null)
            throw new ArgumentException("At least one pending date is required.");

        contract.PendingStartsAt = pendingStartsAt.HasValue
            ? NormalizeDate(pendingStartsAt.Value)
            : null;
        contract.PendingEndsAt = pendingEndsAt.HasValue
            ? NormalizeDate(pendingEndsAt.Value)
            : null;
        contract.DateAmendmentProposedByUserId = proposedByUserId;
        contract.DateAmendmentProposedAt = DateTime.UtcNow;
    }

    public static void ClearPendingAmendment(Entities.Contract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        contract.PendingStartsAt = null;
        contract.PendingEndsAt = null;
        contract.DateAmendmentProposedByUserId = null;
        contract.DateAmendmentProposedAt = null;
    }

    public static string? FillPlaceholders(
        string? text,
        DateTime? startsAt,
        DateTime? endsAt)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var startLabel = FormatDate(startsAt);
        var endLabel = FormatDate(endsAt);
        var result = text;

        result = BracketStartRegex().Replace(result, startLabel);
        result = BracketEndRegex().Replace(result, endLabel);

        // "تاريخ بدء العقد: …" lines that still hold brackets/underscores/TBD
        result = StartLineRegex().Replace(result, m =>
            $"{m.Groups[1].Value}{startLabel}");
        result = EndLineRegex().Replace(result, m =>
            $"{m.Groups[1].Value}{endLabel}");

        return result;
    }

    public static DateTime NormalizeDate(DateTime value) =>
        DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);

    public static string FormatDate(DateTime? value)
    {
        if (value is null)
            return "—";
        return value.Value.ToString("dd MMMM yyyy", ArEg);
    }

    [GeneratedRegex(@"\[\s*تاريخ\s*بدء\s*العقد\s*\]|\[\s*Start\s*Date\s*\]|\[\s*Contract\s*Start\s*Date\s*\]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BracketStartRegex();

    [GeneratedRegex(@"\[\s*تاريخ\s*انتهاء\s*العقد\s*\]|\[\s*End\s*Date\s*\]|\[\s*Contract\s*End\s*Date\s*\]|\[\s*Delivery\s*Date\s*\]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BracketEndRegex();

    [GeneratedRegex(
        @"(تاريخ\s*بدء\s*العقد\s*[:：]\s*)(?:\[.*?\]|_{2,}|\.{2,}|—|-|TBD|N\/A|يُحدَّد لاحقاً|يحدد لاحقا|قيد التحديد)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StartLineRegex();

    [GeneratedRegex(
        @"(تاريخ\s*انتهاء\s*العقد\s*[:：]\s*)(?:\[.*?\]|_{2,}|\.{2,}|—|-|TBD|N\/A|يُحدَّد لاحقاً|يحدد لاحقا|قيد التحديد)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EndLineRegex();

    [GeneratedRegex(
        @"\[\s*تاريخ\s*(?:بدء|انتهاء)\s*العقد\s*\]|\[\s*(?:Start|End|Delivery)\s*Date\s*\]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderRegex();
}
