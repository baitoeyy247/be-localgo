using LocalGo.Application.Common;

namespace LocalGo.Tests;

public sealed class GeoHelperTests
{
    [Fact]
    public void CreatePoint_sets_coordinates()
    {
        var point = GeoHelper.CreatePoint(13.7563, 100.5018);
        Assert.Equal(13.7563, point.Y, 4);
        Assert.Equal(100.5018, point.X, 4);
    }
}
