using Chrono.Services;

namespace Chrono.Tests;

public sealed class GoogleScopesTests
{
    [Fact]
    public void RequireCalendar_BothScopes_DoesNotThrow()
    {
        GoogleScopes.RequireCalendar([GoogleScopes.CalendarAppCreated, GoogleScopes.CalendarListReadonly, "openid", "email"]);
    }

    [Theory]
    [InlineData(GoogleScopes.CalendarAppCreated)]
    [InlineData(GoogleScopes.CalendarListReadonly)]
    public void RequireCalendar_OneScopeMissing_Throws(string granted)
    {
        Assert.Throws<SyncScopesMissingException>(() => GoogleScopes.RequireCalendar([granted, "openid"]));
    }

    [Fact]
    public void RequireCalendar_NoScopes_Throws()
    {
        Assert.Throws<SyncScopesMissingException>(() => GoogleScopes.RequireCalendar(null));
        Assert.Throws<SyncScopesMissingException>(() => GoogleScopes.RequireCalendar([]));
    }

    [Fact]
    public void RequireCalendar_ScopeFromIosResponse_SplitsBySpace()
    {
        var scope = $"{GoogleScopes.CalendarAppCreated} {GoogleScopes.CalendarListReadonly} openid";

        GoogleScopes.RequireCalendar(scope.Split(' '));
    }
}
