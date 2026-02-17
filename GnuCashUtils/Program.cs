using Avalonia;
using Avalonia.ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CsvHelper;
using GnuCashUtils.BulkEdit;
using GnuCashUtils.Categorization;
using GnuCashUtils.Core;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Splat.Microsoft.Extensions.DependencyInjection;
using Splat;
using BulkEditWindow = GnuCashUtils.BulkEdit.BulkEditWindow;

namespace GnuCashUtils;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "accounts")
        {
            RunAccounts();
            return;
        }

        if (args.Length > 0 && args[0] == "analyze")
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: GnuCashUtils analyze <path_to_csv>");
                Environment.Exit(1);
            }
            RunAnalyze(args[1]);
            return;
        }

        var app = BuildAvaloniaApp();
        RegisterUiServices();
        app.StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();

    private static void RegisterCoreServices(IServiceCollection services)
    {
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
        services.AddSingleton<IDbConnectionFactory, SqliteConnectionFactory>();
        services.AddSingleton<IConfigService, ConfigService>();
    }

    private static void RegisterUiServices()
    {
        var services = new ServiceCollection();
        RegisterCoreServices(services);
        services.AddTransient<IViewFor<BulkEditWindowViewModel>, BulkEditWindow>();
        services.AddTransient<IViewFor<CategorizationWindowViewModel>, CategorizationWindow>();
        services.UseMicrosoftDependencyResolver();
    }

    private static void RunAccounts()
    {
        var services = new ServiceCollection();
        RegisterCoreServices(services);
        var provider = services.BuildServiceProvider();

        var configService = provider.GetRequiredService<IConfigService>();
        var config = configService.CurrentConfig;

        var dbFactory = provider.GetRequiredService<IDbConnectionFactory>();
        if (!string.IsNullOrWhiteSpace(config.Database))
            dbFactory.SetDatabase(config.Database);

        var mediator = provider.GetRequiredService<IMediator>();
        var accounts = mediator.Send(new FetchAccountsRequest()).GetAwaiter().GetResult();

        foreach (var account in accounts)
            Console.WriteLine(account.FullName);
    }

    private static void RunAnalyze(string csvPath)
    {
        var services = new ServiceCollection();
        RegisterCoreServices(services);
        var provider = services.BuildServiceProvider();

        var configService = provider.GetRequiredService<IConfigService>();
        var config = configService.CurrentConfig;

        var fileName = Path.GetFileName(csvPath);
        var bankConfig = config.Banks.FirstOrDefault(bank => Regex.IsMatch(fileName, bank.Match));

        if (bankConfig is null)
        {
            Console.Error.WriteLine($"No bank configuration matched '{fileName}'. Check config.yml.");
            Environment.Exit(1);
            return;
        }

        var mediator = provider.GetRequiredService<IMediator>();
        var rows = mediator.Send(new ParseCsvRequest(csvPath, bankConfig)).GetAwaiter().GetResult();

        var matcher = new MerchantMatcher(config.Merchants);
        var emptyAccounts = new ObservableCollection<Account>();

        var unmatched = rows
            .Where(row => matcher.Match(new CategorizationRowViewModel(row.Date, row.Description, row.Amount)) is null)
            .ToList();

        using var csvWriter = new CsvWriter(Console.Out, CultureInfo.InvariantCulture);
        csvWriter.WriteRecords(unmatched);
    }
}
