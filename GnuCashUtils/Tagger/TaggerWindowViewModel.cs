using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using DynamicData;
using GnuCashUtils.Core;
using MediatR;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Unit = System.Reactive.Unit;

namespace GnuCashUtils.Tagger;

public partial class TaggerWindowViewModel: ViewModelBase
{
    private readonly IMediator _mediator;
    public ObservableCollection<TaggedTransaction> Transactions { get; } = [];
    public ObservableCollection<Tag> Tags { get; } = [];
    
    [Reactive] public partial string SearchText { get; set; } 
    [Reactive] public partial DateOnly? StartDate { get; set; }
    [Reactive] public partial DateOnly? EndDate { get; set; }
    
    public ObservableCollection<Tag> SelectedTags { get; } = [];
    public ReactiveCommand<IEnumerable<TaggedTransaction>, Unit> ApplyCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    
    public TaggerWindowViewModel(IMediator mediator, IScheduler? scheduler = null)
    {
        _searchText = "";
        _mediator = mediator;
        this.WhenAnyValue(x => x.SearchText, x => x.StartDate, x => x.EndDate)
            .Throttle(TimeSpan.FromMilliseconds(250), scheduler ?? RxApp.TaskpoolScheduler)
            .Select(tuple => new SearchTransactions(tuple.Item1, tuple.Item2, tuple.Item3))
            .DistinctUntilChanged()
            .Select(req => Observable.FromAsync(ct => mediator.Send(req, ct)))
            .Switch()
            .Subscribe(transactions =>
            {
                Transactions.Clear();
                Transactions.AddRange(transactions);
            });


        ApplyCommand = ReactiveCommand.Create<IEnumerable<TaggedTransaction>, Unit>(ApplyCommandImpl);
        SaveCommand = ReactiveCommand.CreateFromTask(SaveCommandImpl);
    }

    private Unit ApplyCommandImpl(IEnumerable<TaggedTransaction> transactions)
    {
        foreach (var transaction in transactions)
        {
            transaction.Tags.Clear();
            transaction.Tags.AddRange(SelectedTags);
        }
        return Unit.Default;
    }
    
    private async Task SaveCommandImpl()
    {
        await _mediator.Send(new ApplyTags(Transactions.Where(t => t.IsDirty)));
    }
}

public partial class TaggedTransaction: ViewModelBase
{
    public string TransactionGuid { get; init; } = "";
    public string Memo { get; set; } = "";
    public DateOnly Date { get; set; }
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public ObservableCollection<Tag> Tags { get; } = [];
    [Reactive] public bool IsDirty { get; set; }

    public TaggedTransaction()
    {
        Tags.CollectionChanged += (_, _) => IsDirty = true;
    }
}


public record Tag(string Name, string? Value)
{
    public override string ToString() => string.IsNullOrEmpty(Value) ? $"Tag {Name}" : $"Tag {Name}={Value}";
}

public record SearchTransactions(string SearchText, DateOnly? StartDate, DateOnly? EndDate): IRequest<List<TaggedTransaction>>;

public class SearchTransactionsHandler(IDbConnectionFactory db)
    : IRequestHandler<SearchTransactions, List<TaggedTransaction>>
{
    public Task<List<TaggedTransaction>> Handle(SearchTransactions request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}

public record FetchTags(): IRequest<List<Tag>>;

public class FetchTagsHandler(IDbConnectionFactory db) : IRequestHandler<FetchTags, List<Tag>>
{
    public Task<List<Tag>> Handle(FetchTags request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}

public record ApplyTags(IEnumerable<TaggedTransaction> Transactions): IRequest;
public class ApplyTagsHandler (IDbConnectionFactory db): IRequestHandler<ApplyTags>
{
    public Task Handle(ApplyTags request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}