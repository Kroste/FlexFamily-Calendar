using FlexFamilyCalendar.Api.Mail;

namespace FlexFamilyCalendar.Api.Tests;

public class SmtpOptionsTests
{
    [Fact]
    public void Defaults_NotConfigured()
        => Assert.False(new SmtpOptions().IsConfigured);

    [Fact]
    public void OnlyHost_NotConfigured()
        => Assert.False(new SmtpOptions { Host = "smtp.example.com" }.IsConfigured);

    [Fact]
    public void OnlyFrom_NotConfigured()
        => Assert.False(new SmtpOptions { From = "no-reply@example.com" }.IsConfigured);

    [Fact]
    public void HostAndFrom_Configured()
        => Assert.True(new SmtpOptions
        {
            Host = "smtp.example.com",
            From = "no-reply@example.com"
        }.IsConfigured);

    [Fact]
    public void WhitespaceFields_NotConfigured()
        => Assert.False(new SmtpOptions { Host = "   ", From = "  " }.IsConfigured);
}
