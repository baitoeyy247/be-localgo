using LocalGo.Application.Common;
using NetTopologySuite.Geometries;

namespace LocalGo.Api.Mapping;

internal static class MappingHelpers
{
    public static string EnumToString<TEnum>(TEnum value) where TEnum : struct, Enum =>
        value.ToString();

    public static (double? Latitude, double? Longitude) GetCoordinates(Point? location)
    {
        if (GeoHelper.TryGetCoordinates(location, out var latitude, out var longitude))
        {
            return (latitude, longitude);
        }

        return (null, null);
    }
}
