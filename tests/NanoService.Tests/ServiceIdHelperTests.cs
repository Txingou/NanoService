using NanoService;
using Xunit;

namespace NanoService.Tests;

public sealed class ServiceIdHelperTests
{
    [Fact]
    public void Compute_SameType_ReturnsStableValue()
    {
        var first = ServiceIdHelper.Compute(typeof(ServiceIdHelper));
        var second = ServiceIdHelper.Compute(typeof(ServiceIdHelper));

        Assert.Equal(first, second);
        Assert.NotEqual(0u, first);
    }

    [Fact]
    public void Compute_DifferentTypes_ReturnsDifferentValues()
    {
        var first = ServiceIdHelper.Compute(typeof(ServiceIdHelperTests));
        var second = ServiceIdHelper.Compute(typeof(object));

        Assert.NotEqual(first, second);
    }
}
