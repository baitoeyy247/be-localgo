using NetTopologySuite.Geometries;

namespace LocalGo.Application.Common;

public static class GeoHelper
{
    private const double EarthRadiusMeters = 6371000d;

    public static Point CreatePoint(double latitude, double longitude) =>
        new(longitude, latitude) { SRID = 4326 };

    public static bool TryGetCoordinates(Point? point, out double latitude, out double longitude)
    {
        latitude = 0;
        longitude = 0;
        if (point is null)
        {
            return false;
        }

        latitude = point.Y;
        longitude = point.X;
        return IsFinite(latitude) && IsFinite(longitude);
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    public static double DistanceMeters(Point a, Point b)
    {
        // PostGIS geometry distance in SRID 4326 is degrees.
        // Use Haversine so app logic consistently works in meters.
        var lat1 = DegreesToRadians(a.Y);
        var lat2 = DegreesToRadians(b.Y);
        var dLat = lat2 - lat1;
        var dLng = DegreesToRadians(b.X - a.X);

        var sinLat = Math.Sin(dLat / 2d);
        var sinLng = Math.Sin(dLng / 2d);
        var h = (sinLat * sinLat) + (Math.Cos(lat1) * Math.Cos(lat2) * sinLng * sinLng);
        var c = 2d * Math.Atan2(Math.Sqrt(h), Math.Sqrt(1d - h));
        return EarthRadiusMeters * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * (Math.PI / 180d);
}
