using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VehicleGo.IntegrationTests.Infrastructure;
using VehicleGo.Web.Auth;
using VehicleGo.Web.Startup;

namespace VehicleGo.IntegrationTests;

/// <summary>Start-up checks read <see cref="IConfiguration"/> (no database needed).</summary>
public sealed class StartupValidationTests
{
    private static Dictionary<string, string?> Valid() => new()
    {
        ["MASTER_KEY"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        ["ALLOWED_EMAILS"] = "alice@example.com,bob@example.com",
        ["GOOGLE_CLIENT_ID"] = "test-client-id",
        ["GOOGLE_CLIENT_SECRET"] = "test-client-secret-not-real",
    };

    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void 設定齊全時通過() => StartupValidation.Validate(Config(Valid()), NullLogger.Instance);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not base64!")]
    [InlineData("AAAA")]
    public void MASTER_KEY不合法時啟動失敗(string? masterKey)
    {
        var values = Valid();
        values["MASTER_KEY"] = masterKey;

        var error = Assert.Throws<InvalidOperationException>(() => StartupValidation.Validate(Config(values), NullLogger.Instance));
        Assert.Contains("MASTER_KEY", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("GOOGLE_CLIENT_ID")]
    [InlineData("GOOGLE_CLIENT_SECRET")]
    public void Google設定缺漏時啟動失敗(string key)
    {
        var values = Valid();
        values.Remove(key);

        var error = Assert.Throws<InvalidOperationException>(() => StartupValidation.Validate(Config(values), NullLogger.Instance));
        Assert.Contains(key, error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" , ")]
    public void ALLOWED_EMAILS是空的時只警告(string? allowed)
    {
        var values = Valid();
        values["ALLOWED_EMAILS"] = allowed;
        var logs = new CapturingLoggerProvider();

        StartupValidation.Validate(Config(values), logs.CreateLogger("test"));

        Assert.Contains(logs.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("ALLOWED_EMAILS", StringComparison.Ordinal));
    }

    [Fact]
    public void 名單去空白轉小寫_逗號分隔()
    {
        var provider = new EnvironmentAllowlistProvider(Config(new() { ["ALLOWED_EMAILS"] = " Alice@Example.com ,, bob@example.com" }));

        Assert.Equal(["alice@example.com", "bob@example.com"], provider.GetAllowedEmails());
    }
}

public sealed class ReturnUrlValidatorTests
{
    [Theory]
    [InlineData("/", "/")]
    [InlineData("/vehicles", "/vehicles")]
    [InlineData("/vehicles/123?tab=a#x", "/vehicles/123?tab=a#x")]
    [InlineData(null, "/")]
    [InlineData("", "/")]
    [InlineData("   ", "/")]
    [InlineData("vehicles", "/")]
    [InlineData("//evil.example.com", "/")]
    [InlineData("/\\evil.example.com", "/")]
    [InlineData("https://evil.example.com", "/")]
    [InlineData("http:/evil.example.com", "/")]
    [InlineData("javascript:alert(1)", "/")]
    [InlineData("/\tevil", "/")]
    [InlineData("\\\\evil.example.com", "/")]
    public void 只接受站內相對路徑(string? returnUrl, string expected) =>
        Assert.Equal(expected, ReturnUrlValidator.Sanitize(returnUrl));
}
