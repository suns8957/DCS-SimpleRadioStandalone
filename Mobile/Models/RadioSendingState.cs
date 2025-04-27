using System.Text.Json.Serialization;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;

namespace Ciribob.DCS.SimpleRadio.Standalone.Mobile.Models;

public class RadioSendingState : PropertyChangedBaseClass
{
    [JsonIgnore] public long LastSentAt { get; set; }

    public bool IsSending { get; set; }

    public int SendingOn { get; set; }

    public int IsEncrypted { get; set; }
}