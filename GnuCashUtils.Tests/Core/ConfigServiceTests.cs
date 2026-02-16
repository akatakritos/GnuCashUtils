using System;
using System.IO;
using AwesomeAssertions;
using GnuCashUtils.Core;

namespace GnuCashUtils.Tests.Core;

public class ConfigServiceTests
{
    [Fact]
    public void ItParsesDatabase()
    {
        using var svc = new ConfigService(Fixtures.File("config-full.yml"));

        svc.CurrentConfig.Database.Should().Be("/data/test.gnucash");
    }

    [Fact]
    public void ItParsesBankCount()
    {
        using var svc = new ConfigService(Fixtures.File("config-full.yml"));

        svc.CurrentConfig.Banks.Should().HaveCount(2);
    }

    [Fact]
    public void ItParsesBankFields()
    {
        using var svc = new ConfigService(Fixtures.File("config-full.yml"));

        var discover = svc.CurrentConfig.Banks[0];
        discover.Name.Should().Be("Discover");
        discover.Match.Should().Be("Discover-Statement-\\d+\\.csv$");
        discover.Skip.Should().Be(1);
        discover.Headers.Should().Be("{date:mm/DD/yyyy},_,{description},{amount},_");

        var boa = svc.CurrentConfig.Banks[1];
        boa.Name.Should().Be("Bank of America");
        boa.Skip.Should().Be(7);
    }

    [Fact]
    public void ItThrowsWhenFileNotFound()
    {
        var act = () => new ConfigService("/nonexistent/path/config.yml");

        act.Should().Throw<IOException>();
    }

    [Fact]
    public void ItParsesConfigWithNoBanks()
    {
        using var svc = new ConfigService(Fixtures.File("config-no-banks.yml"));

        svc.CurrentConfig.Database.Should().Be("/data/test.gnucash");
        svc.CurrentConfig.Banks.Should().BeEmpty();
    }

    [Fact]
    public void ConfigObservableEmitsCurrentValueOnSubscribe()
    {
        using var svc = new ConfigService(Fixtures.File("config-full.yml"));

        AppConfig? received = null;
        svc.Config.Subscribe(c => received = c);

        received.Should().NotBeNull();
        received!.Database.Should().Be("/data/test.gnucash");
    }

    [Fact]
    public void CurrentConfigAndObservableAreConsistent()
    {
        using var svc = new ConfigService(Fixtures.File("config-full.yml"));

        AppConfig? observed = null;
        svc.Config.Subscribe(c => observed = c);

        svc.CurrentConfig.Should().BeSameAs(observed);
    }
}
