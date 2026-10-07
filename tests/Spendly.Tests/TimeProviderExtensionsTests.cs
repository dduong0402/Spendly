using FluentAssertions;
using Spendly.Tests.TestSupport;
using Spendly.Web.Common;
using Xunit;

namespace Spendly.Tests;

public class TimeProviderExtensionsTests
{
    [Fact]
    public void GetToday_AfterMidnightInVietnam_ReturnsNextDay()
    {
        // 18:00 UTC ngày 01/01 = 01:00 ngày 02/01 tại Việt Nam.
        var clock = new MutableTimeProvider { UtcNow = new DateTimeOffset(2026, 1, 1, 18, 0, 0, TimeSpan.Zero) };

        clock.GetToday().Should().Be(new DateOnly(2026, 1, 2));
    }

    [Fact]
    public void GetToday_BeforeMidnightInVietnam_ReturnsSameDay()
    {
        // 16:59 UTC ngày 01/01 = 23:59 ngày 01/01 tại Việt Nam.
        var clock = new MutableTimeProvider { UtcNow = new DateTimeOffset(2026, 1, 1, 16, 59, 0, TimeSpan.Zero) };

        clock.GetToday().Should().Be(new DateOnly(2026, 1, 1));
    }
}
