# GnuCashUtils

Tool for adding features to manage sqlite based gnucash files in an easier way

## Architecture

- MVVM using ReactiveUI
- Organize by features: FeatureA folder, with FeatureA.axaml and FeatureAViewModel.cs

## Tech Stack

- .NET 9 / Avalonia 11 desktop app
- ReactiveUI + ReactiveUI.SourceGenerators (`[Reactive]` attribute for reactive properties)
- MediatR for commands/queries (handlers live in the same file as the ViewModel)
- Dapper for SQL queries against the SQLite GnuCash database
- `IDbConnectionFactory` / `SqliteConnectionFactory` for DB access — always resolve via DI

## Adding a New Window

1. Create `FeatureName/FeatureNameWindow.axaml` (root element `<Window>`) and `FeatureNameWindow.axaml.cs` (extends `ReactiveWindow<FeatureNameWindowViewModel>`)
2. Create `FeatureName/FeatureNameWindowViewModel.cs` (extends `ViewModelBase`)
3. Register in `Program.cs`: `services.AddTransient<IViewFor<FeatureNameWindowViewModel>, FeatureNameWindow>()`
4. Open it from another ViewModel using `IViewLocator` (see `MainWindowViewModel.cs` for the pattern)

## File Picker

Use `StorageProvider.OpenFilePickerAsync` in code-behind (not ViewModel) — see `MainWindow.axaml.cs`.

## DataGrid with Dynamic Columns

When CSV/data columns are not known at compile time, create `DataGridTextColumn` objects in code-behind and add them to `DataGrid.Columns`. Use index binding: `new Binding("[0]")` etc. when ItemsSource is `IEnumerable<string[]>`. Watch a reactive `Headers` property and call a `RebuildColumns` method — see `CategorizationWindow.axaml.cs`.

## Tests

- Test project: `GnuCashUtils.Tests/`
- Framework: xunit + AwesomeAssertions
- File fixtures: place CSV/data files in `GnuCashUtils.Tests/Fixtures/`, mark them `CopyToOutputDirectory="PreserveNewest"` in the csproj, and use `Fixtures.File("name.csv")` to resolve the runtime path
- `Fixtures` class is globally imported via `<Using Include="GnuCashUtils.Tests"/>` in the test csproj — no per-file using needed

## Dapper

`Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true` is set globally — SQL `column_name` maps to C# `ColumnName` automatically.

## MediatR

Non-trivial IO (file or database) should be done in a MediatR command or query

Put the request/command and its handler in the same file

## DynamicData

DynamicData is a transitive dependency (via ReactiveUI). It comes with two `Count()` operators:
- `System.Reactive.Linq` / standard Rx.NET `Count()` — counts total emissions, not useful for change sets
- `DynamicData.Aggregation.Count()` — counts items currently passing through a change set pipeline

Always `using DynamicData.Aggregation;` when using `Count()` on a DynamicData pipeline, otherwise you get the Rx.NET version which doesn't do what you want.

## Testing

We use AwesomeAssertions (previously called FluentAssertions)

Generally mock calls to mediatr when testing view models
