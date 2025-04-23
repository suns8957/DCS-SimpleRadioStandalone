using System.Net;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Models;

public class PendingPacket
{
    public IPEndPoint ReceivedFrom { get; set; }
    public byte[] RawBytes { get; set; }
}