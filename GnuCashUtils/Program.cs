using Avalonia;
using Avalonia.ReactiveUI;
using System;
using GnuCashUtils.BulkEdit;
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
        var app = BuildAvaloniaApp();
        RegisterServices();
        app.StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();

    private static void RegisterServices()
    {
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
        
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
        services.AddTransient<IViewFor<BulkEditWindowViewModel>, BulkEditWindow>();
            services.AddSingleton<IDbConnectionFactory, SqliteConnectionFactory>();
        services.UseMicrosoftDependencyResolver();
    }
}