using Spendly.Web.Common;

namespace Spendly.Tests.TestSupport;

/// <summary>TimeProvider cho test: múi giờ Việt Nam, thời điểm hiện tại chỉnh được.</summary>
public class MutableTimeProvider : VietnamTimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 9, 29, 3, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => UtcNow;
}
