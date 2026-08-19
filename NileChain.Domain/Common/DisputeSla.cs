using NileChain.Domain.Entities;
using NileChain.Domain.Enums;

namespace NileChain.Domain.Common;

public static class DisputeSla
{
    public const int DefaultHours = 48;

    public static DateTime DueAt(DateTime createdAtUtc, int slaHours) =>
        createdAtUtc.AddHours(slaHours <= 0 ? DefaultHours : slaHours);

    public static bool IsOverdue(Dispute dispute, DateTime utcNow)
    {
        if (!DisputeTransitions.IsActive(dispute.Status))
            return false;

        var due = dispute.SlaDueAt ?? DueAt(dispute.CreatedAt, DefaultHours);
        return utcNow > due;
    }
}
