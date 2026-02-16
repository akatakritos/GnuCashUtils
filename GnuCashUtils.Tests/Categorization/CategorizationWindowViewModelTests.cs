using AwesomeAssertions;
using GnuCashUtils.Categorization;

namespace GnuCashUtils.Tests.Categorization;

public class CategorizationWindowViewModelTests
{
    [Fact]
    public void ItLoadsCsv()
    {
        var vm = new CategorizationWindowViewModel();
        vm.LoadCsv(Fixtures.File("sample.csv"));

        vm.Headers.Should().Equal(["Date", "Description", "Amount"]);
        vm.Rows.Should().HaveCount(3);
        vm.Rows[0].Should().Equal(["2024-01-15", "Grocery Store", "-45.00"]);
    }

    [Fact]
    public void ItSkipsPreamble()
    {
        // preamble.csv has several free-text lines before the CSV header,
        // including one with a comma in it ("Bank of Example, N.A.")
        var vm = new CategorizationWindowViewModel();
        vm.LoadCsv(Fixtures.File("preamble.csv"));

        vm.Headers.Should().Equal(["Date", "Description", "Amount", "Balance"]);
        vm.Rows.Should().HaveCount(2);
        vm.Rows[0].Should().Equal(["2024-01-15", "Grocery Store", "-45.00", "955.00"]);
    }

    [Fact]
    public void ItHandlesQuotedCommas()
    {
        var vm = new CategorizationWindowViewModel();
        vm.LoadCsv(Fixtures.File("quoted.csv"));

        vm.Headers.Should().Equal(["Date", "Description", "Amount"]);
        vm.Rows.Should().HaveCount(3);
        vm.Rows[0][1].Should().Be("Coffee, Snacks");
        vm.Rows[1][1].Should().Be("Dinner, Drinks");
    }

    [Fact]
    public void ItHandlesEscapedQuotes()
    {
        var vm = new CategorizationWindowViewModel();
        vm.LoadCsv(Fixtures.File("quoted.csv"));

        // "Transfer ""savings""" should parse to: Transfer "savings"
        vm.Rows[2][1].Should().Be("Transfer \"savings\"");
    }

    [Fact]
    public void ItSkipsTrailingBlankRows()
    {
        // sample.csv ends with a blank line; it should not appear as a row
        var vm = new CategorizationWindowViewModel();
        vm.LoadCsv(Fixtures.File("sample.csv"));

        vm.Rows.Should().HaveCount(3);
    }

    [Fact]
    public void ItSetsCsvFilePath()
    {
        var vm = new CategorizationWindowViewModel();
        var path = Fixtures.File("sample.csv");
        vm.LoadCsv(path);

        vm.CsvFilePath.Should().Be(path);
    }
}
