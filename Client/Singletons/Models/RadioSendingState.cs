using Ciribob.SRS.Common.Helpers;
using Newtonsoft.Json;

namespace Ciribob.FS3D.SimpleRadio.Standalone.Client.Singletons.Models;

public class RadioSendingState : PropertyChangedBase
{
    [JsonIgnore] public long LastSentAt { get; set; }

    public bool IsSending { get; set; }

    public int SendingOn { get; set; }

    public int IsEncrypted { get; set; }
}