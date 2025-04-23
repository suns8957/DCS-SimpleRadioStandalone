using Ciribob.SRS.Common.Network.Models;

namespace Ciribob.FS3D.SimpleRadio.Standalone.Client.Network;

public class UDPExternalRadioState
{
    public int Id { get; set; }
    public bool Tx { get; set; }
    public bool Rx { get; set; }
    public double Freq { set; get; }

    public double SecFreq { set; get; }

    public int Chn { get; set; }
    public string ChnName { get; set; }

    public float Vol { get; set; }
    public Modulation Mode { get; internal set; }
}