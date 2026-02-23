using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using DynamicData;
using GnuCashUtils.Core;
using GnuCashUtils.Tagger;
using MediatR;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Unit = System.Reactive.Unit;

namespace GnuCashUtils.Reporting.TransactionReport;

public partial class TransactionReportViewModel : ViewModelBase, IReport
{
    public string Name { get; } = "Transaction Report";
    public ReactiveCommand<Unit, Unit> ExecuteCommand { get; }

    public ObservableCollection<Tag> Tags { get; } = [];
    [Reactive] public partial Tag? SelectedTag { get; set; }

    public TransactionReportViewModel(IMediator mediator)
    {
        var canExecute = this.WhenAnyValue(x => x.SelectedTag)
            .Select(t => t != null);
        ExecuteCommand = ReactiveCommand.CreateFromTask(OnExecute, canExecute);

        Observable.FromAsync(ct => mediator.Send(new FetchTags(), ct))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(tags => Tags.AddRange(tags.OrderBy(t => t.ToString())));
    }

    private Task OnExecute()
    {
        throw new System.NotImplementedException();
    }

    #region designer
    public TransactionReportViewModel()
    {
        ExecuteCommand = null!;

        if (!Design.IsDesignMode) return;
        Tags.AddRange([
            new Tag("food"),
            new Tag("travel"),
            new Tag("vacation"),
            new Tag("vacation", "disney-2024"),
            new Tag("vacation", "disney-2025"),
        ]);
        SelectedTag = Tags[2];
    }
    #endregion
}
