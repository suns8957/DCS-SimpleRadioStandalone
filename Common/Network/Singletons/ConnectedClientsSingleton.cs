using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Models;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;

public sealed class ConnectedClientsSingleton : PropertyChangedBase
{
    private static volatile ConnectedClientsSingleton _instance;
    private static readonly object _lock = new();

    private ConnectedClientsSingleton()
    {
    }

    public ConcurrentDictionary<string, SRClientBase> Clients { get; } = new();

    public static ConnectedClientsSingleton Instance
    {
        get
        {
            if (_instance == null)
                lock (_lock)
                {
                    if (_instance == null)
                        _instance = new ConnectedClientsSingleton();
                }

            return _instance;
        }
    }

    public SRClientBase this[string key]
    {
        get => Clients[key];
        set
        {
            Clients[key] = value;
            NotifyAll();
        }
    }

    public ICollection<SRClientBase> Values => Clients.Values;


    public int Total => Clients.Count();

    public event PropertyChangedEventHandler PropertyChanged;

    public void NotifyAll()
    {
        NotifyPropertyChanged("Total");
    }

    public bool TryRemove(string key, out SRClientBase value)
    {
        var result = Clients.TryRemove(key, out value);
        if (result) NotifyPropertyChanged("Total");
        return result;
    }

    public void Clear()
    {
        Clients.Clear();
        NotifyPropertyChanged("Total");
    }

    public bool TryGetValue(string key, out SRClientBase value)
    {
        return Clients.TryGetValue(key, out value);
    }

    public bool ContainsKey(string key)
    {
        return Clients.ContainsKey(key);
    }
}