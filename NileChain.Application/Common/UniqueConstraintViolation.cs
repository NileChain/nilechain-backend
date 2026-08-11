using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace NileChain.Application.Common;

/// <summary>
/// Detects SQL unique / duplicate-key violations from EF <see cref="DbUpdateException"/>.
/// </summary>
public static class UniqueConstraintViolation
{
    public static bool IsViolation(Exception? ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            if (TryGetSqlErrorNumber(e, out var number) && number is 2601 or 2627)
                return true;

            var msg = e.Message;
            if (msg.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("UNIQUE KEY constraint", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("unique index", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("Cannot insert duplicate key", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static bool IsViolation(DbUpdateException ex) => IsViolation((Exception)ex);

    private static bool TryGetSqlErrorNumber(Exception ex, out int number)
    {
        number = 0;
        var type = ex.GetType();
        if (!string.Equals(type.Name, "SqlException", StringComparison.Ordinal))
            return false;

        var prop = type.GetProperty("Number", BindingFlags.Instance | BindingFlags.Public);
        if (prop?.GetValue(ex) is int n)
        {
            number = n;
            return true;
        }

        return false;
    }
}
