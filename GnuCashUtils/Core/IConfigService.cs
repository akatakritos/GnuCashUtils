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

    public ConfigService(string? configPath = null)
    {
        _configPath = configPath ?? Path.Combine(AppContext.BaseDirectory, "config.yml");
        _config = new BehaviorSubject<AppConfig>(Load(_configPath));

        var dir = Path.GetDirectoryName(_configPath);
        var file = Path.GetFileName(_configPath);
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
