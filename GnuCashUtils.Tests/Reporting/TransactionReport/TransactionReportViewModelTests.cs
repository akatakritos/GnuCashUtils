using System.Reactive.Concurrency;
using System.Reactive.Threading.Tasks;
using AwesomeAssertions;
using GnuCashUtils.Core;
using GnuCashUtils.Reporting.TransactionReport;
using GnuCashUtils.Tagger;
using MediatR;
using NSubstitute;
using ReactiveUI;

namespace GnuCashUtils.Tests.Reporting.TransactionReport;

public class TransactionReportViewModelTests
{
    private readonly Fixture _fixture = new();

    class Fixture
    {
        public IMediator MockMediator = Substitute.For<IMediator>();
        public static readonly Tag SampleTag = new("vacation", "disney-2024");

        public Fixture()
        {
            MockMediator.Send(Arg.Any<FetchTags>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(new HashSet<Tag>()));
            MockMediator.Send(Arg.Any<ExecuteTransactionReport>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
        }

        public TransactionReportViewModel BuildSubject()
        {
            RxApp.MainThreadScheduler = ImmediateScheduler.Instance;
            var vm = new TransactionReportViewModel(MockMediator);
            return vm;
        }
    }

    [Fact]
    public void CanExecuteIsFalseWhenNoTagSelected()
    {
        var vm = _fixture.BuildSubject();
        bool canExecute = true;
        vm.ExecuteCommand.CanExecute.Subscribe(x => canExecute = x);

        vm.SelectedTag = null;

        canExecute.Should().BeFalse();
    }

    [Fact]
    public void CanExecuteIsTrueWhenTagSelected()
    {
        var vm = _fixture.BuildSubject();
        bool canExecute = false;
        vm.ExecuteCommand.CanExecute.Subscribe(x => canExecute = x);

        vm.SelectedTag = Fixture.SampleTag;

        canExecute.Should().BeTrue();
    }

    [Fact]
    public async Task OnExecuteSendsExecuteTransactionReportWithSelectedTag()
    {
        var vm = _fixture.BuildSubject();
        vm.SelectedTag = Fixture.SampleTag;

        await vm.ExecuteCommand.Execute().ToTask();

        await _fixture.MockMediator.Received(1).Send(
            Arg.Is<ExecuteTransactionReport>(r => r.Tag == Fixture.SampleTag),
            Arg.Any<CancellationToken>());
    }
}
