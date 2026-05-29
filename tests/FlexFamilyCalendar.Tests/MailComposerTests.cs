using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using Xunit;

namespace FlexFamilyCalendar.Tests;

public class MailComposerTests
{
    [Theory]
    [InlineData("a@b.de", true)]
    [InlineData("lars-kruegel@gmx.de", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("noat.de", false)]
    [InlineData("no@dot", false)]
    [InlineData("with space@b.de", false)]
    [InlineData("@b.de", false)]
    public void LooksLikeEmail_Validates(string email, bool expected)
        => Assert.Equal(expected, MailComposer.LooksLikeEmail(email));

    [Fact]
    public void IsConfigured_RequiresHostAndFrom()
    {
        Assert.False(MailComposer.IsConfigured(new AppSettings()));
        Assert.False(MailComposer.IsConfigured(new AppSettings { SmtpHost = "smtp.x" }));
        Assert.True(MailComposer.IsConfigured(new AppSettings { SmtpHost = "smtp.x", SmtpFrom = "a@b.de" }));
    }

    [Fact]
    public void RecipientsWithEmail_FiltersAndSortsByName()
    {
        var users = new[]
        {
            new User { DisplayName = "Zoe", Email = "zoe@x.de" },
            new User { DisplayName = "Kind", Email = "" },                 // keine Mail → raus
            new User { DisplayName = "Anna", Email = " anna@x.de " },      // wird getrimmt
            new User { Username = "bob", DisplayName = "", Email = "bob@x.de" },  // Fallback Username
        };

        var r = MailComposer.RecipientsWithEmail(users);

        Assert.Equal(3, r.Count);
        Assert.Equal(new[] { "Anna", "bob", "Zoe" }, r.Select(x => x.Name).ToArray());
        Assert.Equal("anna@x.de", r[0].Email);   // getrimmt
    }
}
