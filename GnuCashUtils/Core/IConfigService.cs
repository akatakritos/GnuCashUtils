using System;
using System.IO;
using System.Reactive.Subjects;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace GnuCashUtils.Core;

public interface IConfigService
{
    IObservable<AppConfig> Config { get; }
    AppConfig CurrentConfig { get; }
}

public class ConfigService : IConfigService
{
    private readonly BehaviorSubject<AppConfig> _config;

    public IObservable<AppConfig> Config => _config;
    public AppConfig CurrentConfig => _config.Value;

    public ConfigService(string? configPath = null)
    {
        var config = Load(configPath ?? Path.Combine(AppContext.BaseDirectory, "config.yml"));
        _config = new BehaviorSubject<AppConfig>(config);
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
}
