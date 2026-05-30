using FlexFamilyCalendar.Services.Update;
using Xunit;

namespace FlexFamilyCalendar.Tests;

public class VersionCompareTests
{
    [Theory]
    [InlineData("1.0.0", "1.0.0", 0)]
    [InlineData("v1.0.0", "1.0.0", 0)]
    [InlineData("1.0.1", "1.0.0", 1)]
    [InlineData("1.1.0", "1.0.9", 1)]
    [InlineData("2.0.0", "1.99.99", 1)]
    [InlineData("1.0.0-alpha", "1.0.0", -1)]   // pre-release < release
    [InlineData("1.0.0", "1.0.0-rc.1", 1)]
    [InlineData("1.0.0-rc.2", "1.0.0-rc.1", 1)]
    [InlineData("1.2.3-test", "1.2.3-test", 0)]
    [InlineData("", "0.0.0", 0)]
    [InlineData("garbage", "0.0.0", 0)]
    public void Compare_BasicCases(string a, string b, int expected)
        => Assert.Equal(expected, Math.Sign(VersionCompare.Compare(a, b)));

    [Theory]
    [InlineData("v0.1.0", "v0.0.9", true)]
    [InlineData("v0.1.0", "v0.1.0", false)]
    [InlineData("v0.0.9", "v0.1.0", false)]
    public void IsNewer(string candidate, string baseline, bool expected)
        => Assert.Equal(expected, VersionCompare.IsNewer(candidate, baseline));
}

public class UpdateServiceParseTests
{
    // Realer Auszug aus dem GH-Release-Schema, gekürzt.
    private const string SampleRelease = """
    {
      "tag_name": "v0.2.0",
      "html_url": "https://github.com/owner/repo/releases/tag/v0.2.0",
      "body": "## Was ist neu\n- Cooles Feature\n",
      "assets": [
        { "name": "FlexFamilyCalendar-v0.2.0-linux-x64.tar.gz", "browser_download_url": "https://example.com/linux", "size": 42000000 },
        { "name": "FlexFamilyCalendar-v0.2.0-x86_64.AppImage",   "browser_download_url": "https://example.com/appimage", "size": 43000000 },
        { "name": "FlexFamilyCalendar-v0.2.0-win-x64.zip",       "browser_download_url": "https://example.com/win", "size": 69000000 }
      ]
    }
    """;

    [Fact]
    public void Parse_PicksLinuxTar()
    {
        var info = UpdateService.Parse(SampleRelease, "v0.1.0", UpdatePlatform.LinuxTar);
        Assert.NotNull(info);
        Assert.Equal("v0.2.0", info!.LatestVersion);
        Assert.NotNull(info.Asset);
        Assert.EndsWith("linux-x64.tar.gz", info.Asset!.FileName);
        Assert.Equal(UpdatePlatform.LinuxTar, info.Asset.Platform);
    }

    [Fact]
    public void Parse_PicksAppImage()
    {
        var info = UpdateService.Parse(SampleRelease, "v0.1.0", UpdatePlatform.LinuxAppImage);
        Assert.NotNull(info);
        Assert.EndsWith("x86_64.AppImage", info!.Asset!.FileName);
    }

    [Fact]
    public void Parse_PicksWindowsZip()
    {
        var info = UpdateService.Parse(SampleRelease, "v0.1.0", UpdatePlatform.WindowsZip);
        Assert.NotNull(info);
        Assert.EndsWith("win-x64.zip", info!.Asset!.FileName);
    }

    [Fact]
    public void Parse_ReturnsNullWhenSameVersion()
        => Assert.Null(UpdateService.Parse(SampleRelease, "v0.2.0", UpdatePlatform.LinuxTar));

    [Fact]
    public void Parse_ReturnsNullWhenCurrentIsNewer()
        => Assert.Null(UpdateService.Parse(SampleRelease, "v0.3.0", UpdatePlatform.LinuxTar));

    [Fact]
    public void Parse_UnsupportedPlatform_HasInfoButNoAsset()
    {
        var info = UpdateService.Parse(SampleRelease, "v0.1.0", UpdatePlatform.Unsupported);
        Assert.NotNull(info);
        Assert.Null(info!.Asset);   // kein passendes Asset → User bekommt nur Release-Seite verlinkt
    }

    [Fact]
    public void Parse_NoMatchingAsset_HasInfoButNoAsset()
    {
        const string noAssets = """{ "tag_name": "v0.2.0", "html_url": "u", "body": "x", "assets": [] }""";
        var info = UpdateService.Parse(noAssets, "v0.1.0", UpdatePlatform.LinuxTar);
        Assert.NotNull(info);
        Assert.Null(info!.Asset);
    }
}
