using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using AwesomeAssertions;
using GnuCashUtils.Reporting;
using NSubstitute;
using ReactiveUI;

namespace GnuCashUtils.Tests.Reporting;

public class ReportingScreenViewModelTests
{
    private readonly Fixture _fixture = new();

    class Fixture
    {
        public List<IReport> Reports = [MakeReport("Report A"), MakeReport("Report B")];

        public static IReport MakeReport(string name, bool canExecute = true)
        {
            var report = Substitute.For<IReport>();
            report.Name.Returns(name);
            report.ExecuteCommand.Returns(ReactiveCommand.Create(() => { }, Observable.Return(canExecute)));
            return report;
        }

        public static IReport MakeTrackableReport(string name, out bool executed)
        {
            var wasExecuted = false;
            var report = Substitute.For<IReport>();
            report.Name.Returns(name);
            report.ExecuteCommand.Returns(ReactiveCommand.Create(() => { wasExecuted = true; }));
            executed = wasExecuted;
            // Box the flag so callers can read the final value after execution
            return report;
        }

        public ReportingScreenViewModel BuildSubject()
        {
            return new ReportingScreenViewModel(Reports);
        }
    }

    [Fact]
    public void ItPopulatesReportsFromConstructorArgument()
    {
        var vm = _fixture.BuildSubject();

        vm.Reports.Should().HaveCount(2);
        vm.Reports.Select(r => r.Name).Should().BeEquivalentTo(["Report A", "Report B"]);
    }

    [Fact]
    public void ExecuteCommandCannotExecuteWhenNoReportIsSelected()
    {
        var vm = _fixture.BuildSubject();
        bool canExecute = true;
        vm.ExecuteSelectedReportCommand.CanExecute.Subscribe(x => canExecute = x);

        canExecute.Should().BeFalse();
    }

    [Fact]
    public void ExecuteCommandCanExecuteAfterReportIsSelected()
    {
        var vm = _fixture.BuildSubject();
        bool canExecute = false;
        vm.ExecuteSelectedReportCommand.CanExecute.Subscribe(x => canExecute = x);

        vm.SelectedReport = _fixture.Reports[0];

        canExecute.Should().BeTrue();
    }

    [Fact]
    public void ExecuteCommandCannotExecuteWhenSelectedReportCommandCannotExecute()
    {
        var disabledReport = Fixture.MakeReport("Disabled", canExecute: false);
        var vm = new ReportingScreenViewModel([disabledReport]);
        bool canExecute = true;
        vm.ExecuteSelectedReportCommand.CanExecute.Subscribe(x => canExecute = x);

        vm.SelectedReport = disabledReport;

        canExecute.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteCommandDelegatesToSelectedReportExecuteCommand()
    {
        var vm = _fixture.BuildSubject();
        bool executed = false;
        var innerCommand = ReactiveCommand.Create(() => { executed = true; });
        var report = Substitute.For<IReport>();
        report.Name.Returns("Trackable");
        report.ExecuteCommand.Returns(innerCommand);

        vm.SelectedReport = report;
        await vm.ExecuteSelectedReportCommand.Execute().ToTask();

        executed.Should().BeTrue();
    }

    [Fact]
    public void ExecuteCommandCanExecuteUpdatesWhenSelectedReportChanges()
    {
        var disabledReport = Fixture.MakeReport("Disabled", canExecute: false);
        var enabledReport = Fixture.MakeReport("Enabled", canExecute: true);
        var vm = new ReportingScreenViewModel([disabledReport, enabledReport]);
        bool canExecute = true;
        vm.ExecuteSelectedReportCommand.CanExecute.Subscribe(x => canExecute = x);

        vm.SelectedReport = disabledReport;
        canExecute.Should().BeFalse();

        vm.SelectedReport = enabledReport;
        canExecute.Should().BeTrue();
    }

    [Fact]
    public void ItPopulatesReportsInOrderProvided()
    {
        var reports = new List<IReport>
        {
            Fixture.MakeReport("Zebra"),
            Fixture.MakeReport("Alpha"),
            Fixture.MakeReport("Mango"),
        };
        var vm = new ReportingScreenViewModel(reports);

        vm.Reports.Select(r => r.Name).Should().ContainInOrder("Zebra", "Alpha", "Mango");
    }
}
