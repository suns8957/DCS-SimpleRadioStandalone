using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Models.EventMessages;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;

public class SyncedServerSettings
{
    private static SyncedServerSettings instance;
    private static readonly object _lock = new();
    private static readonly Dictionary<string, string> defaults = DefaultServerSettings.Defaults;

    private readonly ConcurrentDictionary<string, string> _settings;
    private readonly Logger Logger = LogManager.GetCurrentClassLogger();


    public SyncedServerSettings()
    {
        _settings = new ConcurrentDictionary<string, string>();
    }

    public List<double> GlobalFrequencies { get; set; } = new();

    public static SyncedServerSettings Instance
    {
        get
        {
            lock (_lock)
            {
                if (instance == null) instance = new SyncedServerSettings();
            }

            return instance;
        }
    }

    public string ServerVersion { get; set; }

    public string GetSetting(ServerSettingsKeys key)
    {
        var setting = key.ToString();

        return _settings.GetOrAdd(setting, defaults.ContainsKey(setting) ? defaults[setting] : "");
    }

    public bool GetSettingAsBool(ServerSettingsKeys key)
    {
        return Convert.ToBoolean(GetSetting(key));
    }

    public void Decode(Dictionary<string, string> encoded)
    {
        foreach (var kvp in encoded) _settings.AddOrUpdate(kvp.Key, kvp.Value, (key, oldVal) => kvp.Value);

        EventBus.Instance.PublishOnBackgroundThreadAsync(new ServerSettingsUpdatedMessage(_settings));
    }
}