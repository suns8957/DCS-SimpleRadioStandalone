using System.Collections.Generic;

namespace Ciribob.FS3D.SimpleRadio.Standalone.Client.Network;

public class UDPExternalStateMessage
{
    public List<UDPExternalRadioState> Radios { get; set; }

    public uint UnitId { get; set; }

    public string Name { get; set; }

    public int SelectedRadio { get; set; }
}