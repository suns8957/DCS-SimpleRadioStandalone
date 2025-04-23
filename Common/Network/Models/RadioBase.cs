using System;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Models;

public class RadioBase
{
    public bool enc; // encryption enabled
    public byte encKey;
    public double freq = 1;
    public Modulation modulation = Modulation.DISABLED;

    //should the radio restransmit?
    public bool retransmit = false;
    public double secFreq = 1;


    /**
     * Used to determine if we should send an update to the server or not
     * We only need to do that if something that would stop us Receiving happens which
     * is frequencies and modulation
     */
    public override bool Equals(object obj)
    {
        if (obj == null || GetType() != obj.GetType())
            return false;

        var compare = (RadioBase)obj;

        if (!FreqCloseEnough(freq, compare.freq)) return false;
        if (modulation != compare.modulation) return false;
        if (enc != compare.enc) return false;
        if (encKey != compare.encKey) return false;
        if (retransmit != compare.retransmit) return false;
        if (!FreqCloseEnough(secFreq, compare.secFreq)) return false;

        return true;
    }

    internal RadioBase Copy()
    {
        //probably can use memberswise clone
        return new RadioBase
        {
            enc = enc,
            modulation = modulation,
            secFreq = secFreq,
            encKey = encKey,
            freq = freq
        };
    }

    //comparing doubles is risky - check that we're close enough to hear (within 100hz)

    public static bool FreqCloseEnough(double freq1, double freq2)
    {
        var diff = Math.Abs(freq1 - freq2);

        return diff < 500;
    }

    public RadioBase DeepClone()
    {
        return new RadioBase
        {
            enc = enc,
            modulation = modulation,
            secFreq = secFreq,
            encKey = encKey,
            freq = freq
        };
    }
}