using Ciribob.SRS.Common.Network.Models;

namespace Ciribob.FS3D.SimpleRadio.Standalone.Client.Singletons.Models;

public class Transponder : TransponderBase
{
    /**
     * *  -- IFF_STATUS:  OFF = 0,  NORMAL = 1 , or IDENT = 2 (IDENT means Blink on LotATC) 
     * -- M1:-1 = off, any other number on 
     * -- M3: -1 = OFF, any other number on 
     * -- M4: 1 = ON or 0 = OFF
     * -- EXPANSION: only enabled if IFF Expansion is enabled
     * -- CONTROL: 1 - OVERLAY / SRS, 0 - COCKPIT / Realistic, 2 = DISABLED / NOT FITTED AT ALL
     * -- IFF STATUS{"control":1,"expansion":false,"mode1":51,"mode3":7700,"mode4":1,"status":2}
     */
    public enum IFFControlMode
    {
        COCKPIT = 0,
        OVERLAY = 1,
        DISABLED = 2
    }

    public IFFStatus status = IFFStatus.OFF;

    public IFFControlMode Control { get; } = IFFControlMode.DISABLED;

    public bool Expansion { get; } = false;

    //TODO implement equals
}