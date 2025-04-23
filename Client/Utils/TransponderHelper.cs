using Ciribob.FS3D.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.FS3D.SimpleRadio.Standalone.Client.Singletons.Models;
using Ciribob.SRS.Common.Network.Models;

namespace Ciribob.FS3D.SimpleRadio.Standalone.Client.Utils;

public static class TransponderHelper
{
    public static Transponder GetTransponder(bool onlyIfOverlayControls = false)
    {
        var dcsPlayerRadioInfo = ClientStateSingleton.Instance.PlayerUnitState;

        var transponder = (Transponder)dcsPlayerRadioInfo.Transponder;

        if (dcsPlayerRadioInfo != null &&
            transponder != null &&
            transponder.Control != Transponder.IFFControlMode.DISABLED)
        {
            if (onlyIfOverlayControls)
            {
                if (transponder.Control == Transponder.IFFControlMode.OVERLAY)
                    return transponder;
            }
            else
            {
                return transponder;
            }
        }

        return null;
    }

    public static bool ToggleIdent()
    {
        //TODO fix
        // ClientStateSingleton.Instance.LastSent = 0;
        var trans = GetTransponder(true);

        if (trans != null && trans.status != TransponderBase.IFFStatus.OFF)
        {
            if (trans.status == TransponderBase.IFFStatus.NORMAL)
            {
                trans.status = TransponderBase.IFFStatus.IDENT;
                return true;
            }

            trans.status = TransponderBase.IFFStatus.NORMAL;
            return true;
        }

        return false;
    }

    // public static bool Mode4Toggle()
    // {
    //     ClientStateSingleton.Instance.LastSent = 0;
    //     var trans = GetTransponder(true);
    //
    //     if (trans != null) trans.mode4 = !trans.mode4;
    //
    //     return false;
    // }
    //
    // public static bool SetMode3(int mode3)
    // {
    //     ClientStateSingleton.Instance.LastSent = 0;
    //     var trans = GetTransponder(true);
    //
    //     if (trans != null)
    //     {
    //         if (mode3 < 0)
    //         {
    //             trans.mode3 = -1;
    //         }
    //         else
    //         {
    //             var numberStr = Math.Abs(mode3).ToString().ToCharArray();
    //
    //             for (var i = 0; i < numberStr.Length; i++)
    //                 if (int.Parse(numberStr[i].ToString()) > 7)
    //                     numberStr[i] = '7';
    //
    //             trans.mode3 = int.Parse(new string(numberStr));
    //         }
    //
    //         return true;
    //     }
    //
    //     return false;
    // }
    //
    // public static bool SetMode1(int mode1)
    // {
    //     ClientStateSingleton.Instance.LastSent = 0;
    //     var trans = GetTransponder(true);
    //
    //     if (trans != null)
    //     {
    //         if (mode1 < 0)
    //         {
    //             trans.mode1 = -1;
    //         }
    //         else
    //         {
    //             //first digit 0-7 inc
    //             //second 0-3 inc
    //
    //             var first = mode1 / 10;
    //
    //             if (first > 7) first = 7;
    //
    //             if (first < 0) first = 0;
    //
    //             var second = mode1 % 10;
    //
    //             if (second > 3) second = 3;
    //
    //             trans.mode1 = first * 10 + second;
    //         }
    //
    //         return true;
    //     }
    //
    //     return false;
    // }
    //
    // public static bool TogglePower()
    // {
    //     ClientStateSingleton.Instance.LastSent = 0;
    //     var trans = GetTransponder(true);
    //
    //     if (trans != null)
    //     {
    //         if (trans.status == Transponder.IFFStatus.OFF)
    //             trans.status = Transponder.IFFStatus.NORMAL;
    //         else
    //             trans.status = Transponder.IFFStatus.OFF;
    //
    //         return true;
    //     }
    //
    //     return false;
    // }
    //
    // public static bool SetPower(bool on)
    // {
    //     ClientStateSingleton.Instance.LastSent = 0;
    //     var trans = GetTransponder(true);
    //
    //     if (trans != null)
    //     {
    //         if (on)
    //             trans.status = Transponder.IFFStatus.NORMAL;
    //         else
    //             trans.status = Transponder.IFFStatus.OFF;
    //
    //         return true;
    //     }
    //
    //     return false;
    // }
    //
    // public static bool SetMode4(bool on)
    // {
    //     ClientStateSingleton.Instance.LastSent = 0;
    //     var trans = GetTransponder(true);
    //
    //     if (trans != null)
    //     {
    //         trans.mode4 = on;
    //         return true;
    //     }
    //
    //     return false;
    // }
    //
    // public static bool SetIdent(bool on)
    // {
    //     ClientStateSingleton.Instance.LastSent = 0;
    //     var trans = GetTransponder(true);
    //
    //     if (trans != null && trans.status != Transponder.IFFStatus.OFF)
    //     {
    //         if (on)
    //             trans.status = Transponder.IFFStatus.IDENT;
    //         else
    //             trans.status = Transponder.IFFStatus.NORMAL;
    //
    //         return true;
    //     }
    //
    //     return false;
    // }
}