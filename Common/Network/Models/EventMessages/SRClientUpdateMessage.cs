namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Models.EventMessages;

public class SRClientUpdateMessage
{
    public SRClientUpdateMessage(SRClientBase srClient, bool connected = true)
    {
        SrClient = srClient;
        Connected = connected;
    }

    public SRClientBase SrClient { get; }
    public bool Connected { get; }
}