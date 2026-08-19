namespace NileChain.Application.Dtos.Admin;

/// <summary>
/// One signed contribution to a KYB kind's assist score. The code is stable so the admin UI
/// can label it in the reviewer's own language, and the deltas sum to the score itself —
/// the screen cannot disagree with the number it explains.
/// </summary>
public sealed class KybScoreFactorDto
{
    public string Code { get; set; } = string.Empty;
    public int Delta { get; set; }
}

public static class KybScoreFactorCodes
{
    public const string DocumentMissing = "DocumentMissing";
    public const string DocumentUploaded = "DocumentUploaded";
    public const string FileNameMatched = "FileNameMatched";
    public const string FileNameUnmatched = "FileNameUnmatched";
    public const string GuidanceAvailable = "GuidanceAvailable";
    public const string GuidanceUnavailable = "GuidanceUnavailable";

    /// <summary>English prose for the persisted audit trail; the UI translates the code instead.</summary>
    public static string Describe(KybScoreFactorDto factor) => factor.Code switch
    {
        DocumentMissing => "Required KYB document kind was not uploaded.",
        DocumentUploaded => "Document uploaded for this KYB kind.",
        FileNameMatched => "File name matches keywords expected for this kind.",
        FileNameUnmatched => "File name did not match keywords expected for this kind (heuristic).",
        GuidanceAvailable => "Knowledge-base guidance is available for this kind.",
        GuidanceUnavailable => "Knowledge-base guidance is unavailable (retrieval service returned nothing).",
        _ => factor.Code
    };
}
