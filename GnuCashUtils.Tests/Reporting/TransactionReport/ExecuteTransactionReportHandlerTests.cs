using AwesomeAssertions;
using DynamicData;
using GnuCashUtils.Core;
using GnuCashUtils.Reporting.TransactionReport;
using GnuCashUtils.Tagger;
using NSubstitute;

namespace GnuCashUtils.Tests.Reporting.TransactionReport;

public class ExecuteTransactionReportHandlerTests
{
    private readonly Fixture _fixture = new();

    class Fixture
    {
        public static readonly Account GroceriesAccount = new() { Guid = "acc-groceries", Name = "Groceries", FullName = "Expenses:Groceries" };
        public static readonly Account TravelAccount = new() { Guid = "acc-travel", Name = "Travel", FullName = "Expenses:Travel" };

        public List<ExecuteTransactionReportHandler.Dto> Rows =
        [
            new() { TransactionGuid = "t1", PostDate = "2024-01-10 00:00:00", AccountGuid = "acc-groceries", Description = "Whole Foods", ValueNum = 5000, ValueDenom = 100 },
            new() { TransactionGuid = "t2", PostDate = "2024-01-15 00:00:00", AccountGuid = "acc-groceries", Description = "Trader Joes", ValueNum = 3000, ValueDenom = 100 },
            new() { TransactionGuid = "t3", PostDate = "2024-01-12 00:00:00", AccountGuid = "acc-travel", Description = "Airline", ValueNum = 20000, ValueDenom = 100 },
        ];

        public IAccountStore MockAccountStore = Substitute.For<IAccountStore>();

        public Fixture()
        {
            var cache = new SourceCache<Account, string>(a => a.Guid);
            cache.AddOrUpdate([GroceriesAccount, TravelAccount]);
            MockAccountStore.Accounts.Returns(cache);
        }

        public TestableHandler BuildSubject() => new(MockAccountStore, Rows);
    }

    /// <summary>
    /// Subclass that overrides GetTransactions to return canned data, bypassing the real DB.
    /// </summary>
    class TestableHandler : ExecuteTransactionReportHandler
    {
        private readonly List<ExecuteTransactionReportHandler.Dto> _rows;

        public TestableHandler(IAccountStore accountStore, List<ExecuteTransactionReportHandler.Dto> rows)
            : base(null!, accountStore)
        {
            _rows = rows;
        }

        protected override List<ExecuteTransactionReportHandler.Dto> GetTransactions(ExecuteTransactionReport request) => _rows;
    }

    [Fact]
    public void GroupsByAccount()
    {
        var handler = _fixture.BuildSubject();
        var data = handler.BuildReportData(new ExecuteTransactionReport());

        data.AccountGroups.Should().HaveCount(2);
    }

    [Fact]
    public void GroupsOrderedByAccountFullName()
    {
        var handler = _fixture.BuildSubject();
        var data = handler.BuildReportData(new ExecuteTransactionReport());

        data.AccountGroups[0].Account.FullName.Should().Be("Expenses:Groceries");
        data.AccountGroups[1].Account.FullName.Should().Be("Expenses:Travel");
    }

    [Fact]
    public void TransactionsGroupedUnderCorrectAccount()
    {
        var handler = _fixture.BuildSubject();
        var data = handler.BuildReportData(new ExecuteTransactionReport());

        var groceries = data.AccountGroups.Single(g => g.Account.Guid == "acc-groceries");
        groceries.Transactions.Should().HaveCount(2);
        groceries.Transactions.Select(t => t.Description).Should().BeEquivalentTo(["Whole Foods", "Trader Joes"]);
    }

    [Fact]
    public void SubtotalIsCorrect()
    {
        var handler = _fixture.BuildSubject();
        var data = handler.BuildReportData(new ExecuteTransactionReport());

        var groceries = data.AccountGroups.Single(g => g.Account.Guid == "acc-groceries");
        groceries.Total.Should().Be(80m); // 50 + 30
    }

    [Fact]
    public void GrandTotalIsCorrect()
    {
        var handler = _fixture.BuildSubject();
        var data = handler.BuildReportData(new ExecuteTransactionReport());

        data.GrandTotal.Should().Be(280m); // 50 + 30 + 200
    }

    [Fact]
    public void RunningBalanceAccumulatesWithinGroup()
    {
        var handler = _fixture.BuildSubject();
        var data = handler.BuildReportData(new ExecuteTransactionReport());

        var groceries = data.AccountGroups.Single(g => g.Account.Guid == "acc-groceries");
        groceries.Transactions[0].RunningBalance.Should().Be(50m);
        groceries.Transactions[1].RunningBalance.Should().Be(80m);
    }

    [Fact]
    public void RunningBalanceRestartsPerGroup()
    {
        var handler = _fixture.BuildSubject();
        var data = handler.BuildReportData(new ExecuteTransactionReport());

        var travel = data.AccountGroups.Single(g => g.Account.Guid == "acc-travel");
        travel.Transactions[0].RunningBalance.Should().Be(200m);
    }

    [Fact]
    public void FormattedDateIsCorrect()
    {
        var handler = _fixture.BuildSubject();
        var data = handler.BuildReportData(new ExecuteTransactionReport());

        var groceries = data.AccountGroups.Single(g => g.Account.Guid == "acc-groceries");
        groceries.Transactions[0].FormattedDate.Should().Be("2024-01-10");
    }

    [Fact]
    public void AmountsAreComputedFromNumDenom()
    {
        var handler = _fixture.BuildSubject();
        var data = handler.BuildReportData(new ExecuteTransactionReport());

        var groceries = data.AccountGroups.Single(g => g.Account.Guid == "acc-groceries");
        groceries.Transactions[0].Amount.Should().Be(50m);
        groceries.Transactions[1].Amount.Should().Be(30m);
    }
}
