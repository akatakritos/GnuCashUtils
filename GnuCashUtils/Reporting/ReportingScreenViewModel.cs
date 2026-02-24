using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using DynamicData;
using GnuCashUtils.Core;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace GnuCashUtils.Reporting;

public partial class ReportingScreenViewModel: ViewModelBase
{
    public ObservableCollection<IReport> Reports { get; } = [];
    [Reactive] public partial IReport? SelectedReport { get; set; }
    public ReactiveCommand<Unit, Unit> ExecuteSelectedReportCommand { get; }
    
    public ReportingScreenViewModel(IEnumerable<IReport> reports)
    {
        Reports.AddRange(reports);

        var canExecute = this.WhenAnyValue(x => x.SelectedReport)
            .WhereNotNull()
            .Select(r => r.ExecuteCommand.CanExecute)
            .Switch();

        ExecuteSelectedReportCommand = ReactiveCommand.CreateFromObservable(() => SelectedReport!.ExecuteCommand.Execute(Unit.Default), canExecute);
    }

    #region designer
    public ReportingScreenViewModel()
    {
        Reports.Add(new TransactionReport.TransactionReportViewModel());
        ExecuteSelectedReportCommand = null!;
    }
    #endregion
}

public interface IReport
{
    /// <summary>
    /// Gets the unique name of the report.
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Gets a command that can execute the report
    /// </summary>
    ReactiveCommand<Unit, Unit> ExecuteCommand { get; }
}