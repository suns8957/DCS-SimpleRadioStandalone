using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Models;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Client;

public class UnitUpdateMessage
{
    private SRClientBase _unitUpdate;

    public SRClientBase UnitUpdate
    {
        get => _unitUpdate;
        set
        {
            if (value == null)
            {
                _unitUpdate = null;
            }
            else
            {
                var clone = value.DeepClone();
                _unitUpdate = clone;
            }
        }
    }

    public bool FullUpdate { get; set; }
}