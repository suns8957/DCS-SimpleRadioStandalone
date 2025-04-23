namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Models;

public class RadioConfig
{
    public enum EncryptionMode
    {
        NO_ENCRYPTION = 0,
        ENCRYPTION_JUST_OVERLAY = 1,
        ENCRYPTION_FULL = 2,
        ENCRYPTION_COCKPIT_TOGGLE_OVERLAY_CODE = 3

        // 0  is no controls
        // 1 is FC3 Gui Toggle + Gui Enc key setting
        // 2 is InCockpit toggle + Incockpit Enc setting
        // 3 is Incockpit toggle + Gui Enc Key setting
    }

    public enum FreqMode
    {
        COCKPIT = 0,
        OVERLAY = 1
    }

    public enum Modulation
    {
        AM = 0,
        FM = 1,
        INTERCOM = 2,
        DISABLED = 3,
        HAVEQUICK = 4,
        SATCOM = 5,
        MIDS = 6,
        SINCGARS = 7
    }

    public enum RetransmitMode
    {
        COCKPIT = 0,
        OVERLAY = 1,
        DISABLED = 2
    }

    public enum VolumeMode
    {
        COCKPIT = 0,
        OVERLAY = 1
    }
}