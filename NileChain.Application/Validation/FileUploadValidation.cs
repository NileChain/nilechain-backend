namespace NileChain.Application.Validation;

/// <summary>
/// Farm document upload allowlist, size cap, and magic-byte checks (no I/O beyond the provided stream/bytes).
/// </summary>
public static class FileUploadValidation
{
    public const long MaxBytes = 10 * 1024 * 1024; // 10 MB

    public static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".jpg", ".jpeg", ".png", ".webp"
    };

    public static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/webp"
    };

    public sealed record Result(bool IsValid, string? ErrorCode, string? ErrorMessage);

    public static Result Validate(
        string? fileName,
        string? contentType,
        long length,
        Stream content)
    {
        if (length <= 0)
            return Fail("File.Empty", "File is required.");

        if (length > MaxBytes)
            return Fail("File.TooLarge", $"File exceeds the maximum size of {MaxBytes / (1024 * 1024)} MB.");

        var ext = NormalizeExtension(fileName);
        if (ext is null || !AllowedExtensions.Contains(ext))
            return Fail("File.TypeNotAllowed", "Allowed types: pdf, jpg, jpeg, png, webp.");

        if (!string.IsNullOrWhiteSpace(contentType)
            && !AllowedContentTypes.Contains(contentType.Trim())
            && !IsGenericOctetStream(contentType))
        {
            return Fail("File.TypeNotAllowed", "Allowed types: pdf, jpg, jpeg, png, webp.");
        }

        var header = ReadHeader(content, 16);
        if (!MatchesMagicBytes(ext, header))
            return Fail("File.ContentMismatch", "File content does not match the declared type.");

        return new Result(true, null, null);
    }

    public static Result Validate(
        string? fileName,
        string? contentType,
        long length,
        ReadOnlySpan<byte> headerBytes)
    {
        if (length <= 0)
            return Fail("File.Empty", "File is required.");

        if (length > MaxBytes)
            return Fail("File.TooLarge", $"File exceeds the maximum size of {MaxBytes / (1024 * 1024)} MB.");

        var ext = NormalizeExtension(fileName);
        if (ext is null || !AllowedExtensions.Contains(ext))
            return Fail("File.TypeNotAllowed", "Allowed types: pdf, jpg, jpeg, png, webp.");

        if (!string.IsNullOrWhiteSpace(contentType)
            && !AllowedContentTypes.Contains(contentType.Trim())
            && !IsGenericOctetStream(contentType))
        {
            return Fail("File.TypeNotAllowed", "Allowed types: pdf, jpg, jpeg, png, webp.");
        }

        if (!MatchesMagicBytes(ext, headerBytes))
            return Fail("File.ContentMismatch", "File content does not match the declared type.");

        return new Result(true, null, null);
    }

    public static string? NormalizeExtension(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        var ext = Path.GetExtension(fileName.Trim());
        return string.IsNullOrEmpty(ext) ? null : ext.ToLowerInvariant();
    }

    internal static bool MatchesMagicBytes(string extension, ReadOnlySpan<byte> header)
    {
        if (header.Length < 4)
            return false;

        return extension switch
        {
            ".pdf" => header.StartsWith("%PDF"u8),
            ".jpg" or ".jpeg" => header.Length >= 3
                && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".png" => header.Length >= 8
                && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
                && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A,
            ".webp" => header.Length >= 12
                && header.StartsWith("RIFF"u8)
                && header.Slice(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };
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

    private static bool IsGenericOctetStream(string? contentType) =>
        string.Equals(contentType?.Trim(), "application/octet-stream", StringComparison.OrdinalIgnoreCase);

    private static Result Fail(string code, string message) => new(false, code, message);
}
