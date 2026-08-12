namespace NileChain.Domain.Common;

public static class EgyptGovernorateCentroids
{
    private static readonly Dictionary<string, (double Lat, double Lon)> ByName =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Alexandria"] = (31.2001, 29.9187),
            ["Aswan"] = (24.0889, 32.8998),
            ["Asyut"] = (27.1809, 31.1837),
            ["Assiut"] = (27.1809, 31.1837),
            ["Beheira"] = (30.8481, 30.3436),
            ["Beni Suef"] = (28.349, 30.87),
            ["Cairo"] = (30.0444, 31.2357),
            ["Dakahlia"] = (31.0409, 31.3785),
            ["Damietta"] = (31.4175, 31.8144),
            ["Faiyum"] = (29.3084, 30.8428),
            ["Fayoum"] = (29.3084, 30.8428),
            ["Gharbia"] = (30.8754, 31.0335),
            ["Giza"] = (30.0131, 31.2089),
            ["Ismailia"] = (30.5965, 32.2715),
            ["Kafr El Sheikh"] = (31.1107, 30.9388),
            ["Luxor"] = (25.6872, 32.6396),
            ["Matrouh"] = (31.3543, 27.2373),
            ["Minya"] = (28.1099, 30.7503),
            ["Monufia"] = (30.5972, 30.9876),
            ["New Valley"] = (25.4517, 30.5463),
            ["North Sinai"] = (30.2824, 33.6176),
            ["Port Said"] = (31.2653, 32.3019),
            ["Qalyubia"] = (30.329, 31.22),
            ["Qena"] = (26.1551, 32.716),
            ["Red Sea"] = (26.2541, 33.8116),
            ["Sharqia"] = (30.7327, 31.7195),
            ["Sohag"] = (26.559, 31.6956),
            ["South Sinai"] = (28.234, 33.622),
            ["Suez"] = (29.9668, 32.5498)
        };

    public static bool TryResolve(string? governorate, out double latitude, out double longitude)
    {
        latitude = 0;
        longitude = 0;
        if (string.IsNullOrWhiteSpace(governorate))
            return false;
        if (!ByName.TryGetValue(governorate.Trim(), out var point))
            return false;
        latitude = point.Lat;
        longitude = point.Lon;
        return true;
    }
}
