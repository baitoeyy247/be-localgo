using LocalGo.Api.Controllers;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace LocalGo.Tests;

public class R47DevGateTests
{
    private sealed class Env(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "t";
        public string ContentRootPath { get; set; } = "/";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Theory]
    [InlineData("Production", "true", false)]
    [InlineData("Production", null, false)]
    [InlineData("Staging", null, false)]
    [InlineData("Staging", "", false)]
    [InlineData("Staging", "false", false)]
    [InlineData("Staging", "true", true)]
    [InlineData("Staging", "TRUE", true)]
    [InlineData("Development", null, true)]
    [InlineData("Development", "false", false)]
    public void Gate_defaults_are_production_safe(string env, string? value, bool expected) =>
        Assert.Equal(expected, DevLoginGate.IsEnabled(value, new Env(env)));

    [Fact]
    public void Diagnostics_off_in_production()
    {
        Assert.False(DevLoginGate.DiagnosticsEnabled(new Env("Production")));
        Assert.True(DevLoginGate.DiagnosticsEnabled(new Env("Staging")));
    }
}
