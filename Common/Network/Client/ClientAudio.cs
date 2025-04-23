using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Models;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Client;

public class ClientAudio
{
    public byte[] EncodedAudio { get; set; }
    public float[] PcmAudioFloat { get; set; }
    public string ClientGuid { get; set; }
    public long ReceiveTime { get; set; }
    public int ReceivedRadio { get; set; }
    public double Frequency { get; set; }
    public short Modulation { get; set; }
    public float Volume { get; set; }
    public uint UnitId { get; set; }
    public RadioReceivingState RadioReceivingState { get; set; }
    public ulong PacketNumber { get; set; }
    public string OriginalClientGuid { get; set; }
    public bool IsSecondary { get; set; }
    public string UnitType { get; set; } = "";
}