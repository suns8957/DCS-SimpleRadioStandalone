using System;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Models;

public class RadioReceivingState
{
    public long LastReceivedAt { get; set; }

    public bool IsSecondary { get; set; }
    public bool IsSimultaneous { get; set; }
    public int ReceivedOn { get; set; }

    public bool PlayedEndOfTransmission { get; set; }

    public string SentBy { get; set; }

    public bool IsReceiving => DateTime.Now.Ticks - LastReceivedAt < 3500000;
}