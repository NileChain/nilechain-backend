namespace NileChain.Application.Auth;

/// <summary>
/// Pure uniqueness check for ApplicationUser.PhoneNumber (non-empty phones only).
/// </summary>
public static class PhoneUniqueness
{
    /// <summary>
    /// Returns true when <paramref name="phone"/> is already owned by a different user.
    /// Empty/null phones are never considered taken.
    /// </summary>
    public static bool IsTakenByOther(
        string? phone,
        Guid? currentUserId,
        IEnumerable<(Guid UserId, string? PhoneNumber)> existingUsers)
    {
        if (string.IsNullOrEmpty(phone))
            return false;

        foreach (var (userId, phoneNumber) in existingUsers)
        {
            if (phoneNumber != phone)
                continue;
            if (currentUserId.HasValue && userId == currentUserId.Value)
                continue;
            return true;
        }

        return false;
    }
}
