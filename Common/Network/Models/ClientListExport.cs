using System.Collections.Generic;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Models;

public struct ClientListExport
{
    public ICollection<SRClientBase> Clients { get; set; }

    public string ServerVersion { get; set; }
}