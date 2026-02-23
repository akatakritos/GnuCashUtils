using System;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace GnuCashUtils.Core;

public interface IConfigService
{
    IObservable<AppConfig> Config { get; }
    AppConfig CurrentConfig { get; }
}

public class ConfigService : IConfigService, IDisposable
{
    private readonly string _configPath;
    private readonly BehaviorSubject<AppConfig> _config;
    private readonly FileSystemWatcher? _watcher;

    public IObservable<AppConfig> Config => _config;
    public AppConfig CurrentConfig => _config.Value;

    public static string ResolveConfigDir()
    {
        var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var baseDir = xdgConfigHome is { Length: > 0 }
            ? xdgConfigHome
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        return Path.Combine(baseDir, "GnuCashUtils");
    }

    private static string DefaultConfigPath() => Path.Combine(ResolveConfigDir(), "config.yml");

    public ConfigService(string? configPath = null)
    {
        _configPath = configPath ?? DefaultConfigPath();
        _config = new BehaviorSubject<AppConfig>(Load(_configPath));

        // Resolve symlinks so the watcher sees changes to the real file, not just the link entry
        var resolvedPath = File.ResolveLinkTarget(_configPath, returnFinalTarget: true)?.FullName ?? _configPath;
        var dir = Path.GetDirectoryName(resolvedPath);
        var file = Path.GetFileName(resolvedPath);
        if (dir != null && Directory.Exists(dir))
        {
            _watcher = new FileSystemWatcher(dir, file)
            {
                NotifyFilter = NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };

            Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
                    h => _watcher.Changed += h,
                    h => _watcher.Changed -= h)
                .Throttle(TimeSpan.FromMilliseconds(300))
                .Subscribe(_ => _config.OnNext(Load(_configPath)));
        }
    }

    private static AppConfig Load(string path)
    {
        if (!File.Exists(path))
            return new AppConfig();

        var yaml = File.ReadAllText(path);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        return deserializer.Deserialize<AppConfig>(yaml);
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _config.Dispose();
    }
}
