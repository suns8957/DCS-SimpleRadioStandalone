namespace Ciribob.FS3D.SimpleRadio.Standalone.Client.Network;

public class UDPInterfaceCommand
{
    internal static readonly string TX_END = "TX_END";
    internal static readonly string TX_START = "TX_START";
    internal static readonly string VOLUME = "Volume";

    public int RadioId { get; set; }
    public string Command { get; set; }
    public int Parameter { get; set; }
}