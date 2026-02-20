using Avalonia;
using System;
using Avalonia.ReactiveUI;
using GnuCashUtils.BulkEdit;
using GnuCashUtils.Categorization;
using GnuCashUtils.Core;
using GnuCashUtils.Core.Behaviors;
using GnuCashUtils.Tagger;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Serilog;
using Splat.Microsoft.Extensions.DependencyInjection;
using Splat;
using Splat.Serilog;
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
        
        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Information)
            // .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command",
            //     Serilog.Events.LogEventLevel.Warning)
            .WriteTo.Console(
                outputTemplate:
                "[{Timestamp:yyyy-MM-dd HH:mm:ss.ff} {SourceContext,-48} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

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
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<Program>();
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
        });
        services.AddSingleton<IDbConnectionFactory, SqliteConnectionFactory>();
        services.AddSingleton<IConfigService, ConfigService>();
    }

    private static void RegisterUiServices()
    {
        var services = new ServiceCollection();
        RegisterCoreServices(services);
        services.AddTransient<IViewFor<BulkEditWindowViewModel>, BulkEditWindow>();
        
        services.AddTransient<IViewFor<CategorizationWindowViewModel>, CategorizationWindow>();
        services.AddTransient<CategorizationWindowViewModel>();
        
        services.AddTransient<IViewFor<TaggerWindowViewModel>, TaggerWindow>();
        services.AddTransient<TaggerWindowViewModel>();
        
        services.AddSingleton<IAccountStore, AccountStore>();
        services.AddTransient<IClassifierBuilder, ClassifierBuilder>();
        services.UseMicrosoftDependencyResolver();
        Locator.CurrentMutable.UseSerilogFullLogger();
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

}
