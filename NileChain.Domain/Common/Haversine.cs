namespace NileChain.Domain.Common;

/// <summary>
/// Pure great-circle distance (km). No external geo APIs.
/// </summary>
public static class Haversine
{
    private const double EarthRadiusKm = 6371.0;

    public static double DistanceKm(
        double latitude1,
        double longitude1,
        double latitude2,
        double longitude2)
    {
        static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

        var dLat = ToRadians(latitude2 - latitude1);
        var dLon = ToRadians(longitude2 - longitude1);
        var lat1 = ToRadians(latitude1);
        var lat2 = ToRadians(latitude2);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(lat1) * Math.Cos(lat2)
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Asin(Math.Min(1.0, Math.Sqrt(a)));
        return EarthRadiusKm * c;
    }

    public static double DistanceKm(
        decimal latitude1,
        decimal longitude1,
        decimal latitude2,
        decimal longitude2) =>
        DistanceKm(
            (double)latitude1,
            (double)longitude1,
            (double)latitude2,
            (double)longitude2);
}
