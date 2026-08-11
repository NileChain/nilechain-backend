using System.Text;

namespace NileChain.Application.Validation;

/// <summary>
/// Admin RAG document upload allowlist, size cap, filename sanitization, and binary rejection.
/// </summary>
public static class RagUploadValidation
{
    public const long MaxBytes = 5 * 1024 * 1024; // 5 MB

    public static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".pdf"
    };

    public sealed record Result(
        bool IsValid,
        string? ErrorCode,
        string? ErrorMessage,
        string? SafeStoredFileName,
        string? Extension);

    public static Result Validate(
        string? originalFileName,
        long length,
        Stream content)
    {
        if (length <= 0)
            return Fail("Rag.FileEmpty", "File is required.");

        if (length > MaxBytes)
            return Fail("Rag.FileTooLarge", $"File exceeds the maximum size of {MaxBytes / (1024 * 1024)} MB.");

        var ext = FileUploadValidation.NormalizeExtension(originalFileName);
        if (ext is null || !AllowedExtensions.Contains(ext))
            return Fail("Rag.TypeNotAllowed", "Allowed types: .txt, .md, .pdf.");

        var header = ReadHeader(content, 16);
        if (ext == ".pdf")
        {
            if (!header.AsSpan().StartsWith("%PDF"u8))
                return Fail("Rag.ContentMismatch", "PDF content does not match the declared type.");
        }
        else
        {
            // Reject binaries / NUL-heavy payloads for text/markdown.
            if (LooksBinary(header) || ContainsNul(content, maxProbe: 64 * 1024))
                return Fail("Rag.BinaryRejected", "Binary content is not allowed for text RAG uploads.");
        }

        var stored = $"{Guid.NewGuid():N}{ext}";
        return new Result(true, null, null, stored, ext);
    }

    public static Result Validate(
        string? originalFileName,
        long length,
        ReadOnlySpan<byte> headerBytes,
        bool contentContainsNul = false)
    {
        if (length <= 0)
            return Fail("Rag.FileEmpty", "File is required.");

        if (length > MaxBytes)
            return Fail("Rag.FileTooLarge", $"File exceeds the maximum size of {MaxBytes / (1024 * 1024)} MB.");

        var ext = FileUploadValidation.NormalizeExtension(originalFileName);
        if (ext is null || !AllowedExtensions.Contains(ext))
            return Fail("Rag.TypeNotAllowed", "Allowed types: .txt, .md, .pdf.");

        if (ext == ".pdf")
        {
            if (!headerBytes.StartsWith("%PDF"u8))
                return Fail("Rag.ContentMismatch", "PDF content does not match the declared type.");
        }
        else if (LooksBinary(headerBytes) || contentContainsNul)
        {
            return Fail("Rag.BinaryRejected", "Binary content is not allowed for text RAG uploads.");
        }

        var stored = $"{Guid.NewGuid():N}{ext}";
        return new Result(true, null, null, stored, ext);
    }

    /// <summary>Sanitize a display title derived from a user-supplied filename.</summary>
    public static string SanitizeDisplayName(string? originalFileName, string fallback = "document")
    {
        var name = Path.GetFileNameWithoutExtension(originalFileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(name))
            return fallback;

        var sb = new StringBuilder(name.Length);
        foreach (var ch in name.Trim())
        {
            if (char.IsLetterOrDigit(ch) || ch is '-' or '_' or ' ')
                sb.Append(ch);
        }

        var cleaned = sb.ToString().Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? fallback : cleaned;
    }

    private static bool LooksBinary(ReadOnlySpan<byte> header)
    {
        if (header.IsEmpty)
            return true;

        for (var i = 0; i < header.Length; i++)
        {
            if (header[i] == 0)
                return true;
        }

        return false;
    }

    private static bool LooksBinary(byte[] header) => LooksBinary(header.AsSpan());

    private static bool ContainsNul(Stream content, int maxProbe)
    {
        var buffer = new byte[Math.Min(4096, maxProbe)];
        var probed = 0;
        while (probed < maxProbe)
        {
            var toRead = Math.Min(buffer.Length, maxProbe - probed);
            var n = content.Read(buffer, 0, toRead);
            if (n == 0)
                break;

            for (var i = 0; i < n; i++)
            {
                if (buffer[i] == 0)
                {
                    if (content.CanSeek)
                        content.Position = 0;
                    return true;
                }
            }

            probed += n;
        }

        if (content.CanSeek)
            content.Position = 0;

        return false;
    }

    private static byte[] ReadHeader(Stream content, int count)
    {
        var buffer = new byte[count];
        var read = 0;
        while (read < count)
        {
            var n = content.Read(buffer, read, count - read);
            if (n == 0)
                break;
            read += n;
        }

        if (content.CanSeek)
            content.Position = 0;

        return read == count ? buffer : buffer.AsSpan(0, read).ToArray();
    }

    private static Result Fail(string code, string message) =>
        new(false, code, message, null, null);
}
