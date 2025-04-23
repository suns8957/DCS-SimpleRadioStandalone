using System.Threading;
using System.Threading.Tasks;
using Caliburn.Micro;
using Ciribob.FS3D.SimpleRadio.Standalone.Client.Singletons.Models;
using Ciribob.SRS.Common.Network.Models;
using Ciribob.SRS.Common.Network.Models.EventMessages;
using Ciribob.SRS.Common.Network.Singletons;
using PropertyChangedBase = Ciribob.SRS.Common.Helpers.PropertyChangedBase;

namespace Ciribob.FS3D.SimpleRadio.Standalone.Client.Singletons;

public class ClientStateSingleton : PropertyChangedBase, IHandle<TCPClientStatusMessage>
{
    private static volatile ClientStateSingleton _instance;
    private static readonly object _lock = new();

    private ClientStateSingleton()
    {
        // RadioReceivingState = new RadioReceivingState[11];
        PlayerUnitState = new PlayerUnitState();
        GUID = ShortGuid.NewGuid();
        RadioReceivingState = new RadioReceivingState[PlayerUnitState.NUMBER_OF_RADIOS];
        for (var i = 0; i < RadioReceivingState.Length; i++) RadioReceivingState[i] = new RadioReceivingState();

        RadioSendingState = new RadioSendingState();
        EventBus.Instance.SubscribeOnUIThread(this);
    }

    public ShortGuid GUID { get; }

    public PlayerUnitState PlayerUnitState { get; }

    public RadioSendingState RadioSendingState { get; set; }
    public RadioReceivingState[] RadioReceivingState { get; }

    public static ClientStateSingleton Instance
    {
        get
        {
            if (_instance == null)
                lock (_lock)
                {
                    if (_instance == null)
                        _instance = new ClientStateSingleton();
                }

            return _instance;
        }
    }

    public Task HandleAsync(TCPClientStatusMessage message, CancellationToken cancellationToken)
    {
        if (message.Connected)
        {
            //TODO
        }

        //TODO
        // PlayerUnitState.Reset();
        return Task.CompletedTask;
    }
}