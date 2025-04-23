using System;
using System.Linq;
using Ciribob.FS3D.SimpleRadio.Standalone.Client.Settings.RadioChannels;
using Ciribob.FS3D.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.FS3D.SimpleRadio.Standalone.Client.Singletons.Models;
using Ciribob.SRS.Common.Network.Client;
using Ciribob.SRS.Common.Network.Models;
using Ciribob.SRS.Common.Network.Singletons;
using NLog;

namespace Ciribob.FS3D.SimpleRadio.Standalone.Client.Utils;

public static class RadioHelper
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public static void ToggleGuard(int radioId)
    {
        var radio = GetRadio(radioId);

        if (radio != null)
            if (radio.Config.FrequencyControl == RadioConfig.FreqMode.OVERLAY ||
                radio.Config.GuardFrequencyControl == RadioConfig.FreqMode.OVERLAY)
                if (radio.Config.GuardFrequency > 0)
                {
                    if (radio.SecFreq > 0)
                        radio.SecFreq = 0;
                    else
                        radio.SecFreq = radio.Config.GuardFrequency;

                    EventBus.Instance.PublishOnBackgroundThreadAsync(new UnitUpdateMessage
                    {
                        FullUpdate = true,
                        UnitUpdate = ClientStateSingleton.Instance.PlayerUnitState.PlayerUnitStateBase
                    });
                }
    }

    public static void SetGuard(int radioId, bool enabled)
    {
        var radio = GetRadio(radioId);

        if (radio != null)
            if (radio.Config.FrequencyControl == RadioConfig.FreqMode.OVERLAY ||
                radio.Config.GuardFrequencyControl == RadioConfig.FreqMode.OVERLAY)
            {
                if (!enabled)
                    radio.SecFreq = 0;
                else
                    radio.SecFreq = radio.Config.GuardFrequency;
            }
    }

    public static bool UpdateRadioFrequency(double frequency, int radioId, bool delta = true, bool inMHz = true)
    {
        var inLimit = true;
        const double MHz = 1000000;

        if (inMHz) frequency = frequency * MHz;

        var radio = GetRadio(radioId);

        if (radio != null)
            if (radio.Modulation != Modulation.DISABLED
                && radio.Modulation != Modulation.INTERCOM
                && radio.Config.FrequencyControl == RadioConfig.FreqMode.OVERLAY)
            {
                if (delta)
                    radio.Freq = (int)Math.Round(radio.Freq + frequency);
                else
                    radio.Freq = (int)Math.Round(frequency);

                //make sure we're not over or under a limit
                if (radio.Freq > radio.Config.MaxFrequency)
                {
                    inLimit = false;
                    radio.Freq = radio.Config.MaxFrequency;
                }
                else if (radio.Freq < radio.Config.MinimumFrequency)
                {
                    inLimit = false;
                    radio.Freq = radio.Config.MinimumFrequency;
                }

                //set to no channel
                radio.CurrentChannel = radio.PresetChannels.First();

                //make radio data stale to force resysnc
                //ClientStateSingleton.Instance.LastSent = 0;
                EventBus.Instance.PublishOnBackgroundThreadAsync(new UnitUpdateMessage
                {
                    FullUpdate = true, UnitUpdate = ClientStateSingleton.Instance.PlayerUnitState.PlayerUnitStateBase
                });
            }

        return inLimit;
    }

    public static bool SelectRadio(int radioId)
    {
        var radio = GetRadio(radioId);

        if (radio != null)
            if (radio.Modulation != Modulation.DISABLED
                && ClientStateSingleton.Instance.PlayerUnitState.control ==
                PlayerUnitState.RadioSwitchControls.HOTAS)
            {
                ClientStateSingleton.Instance.PlayerUnitState.SelectedRadio = (short)radioId;
                return true;
            }

        return false;
    }

    public static Radio GetRadio(int radio)
    {
        var dcsPlayerRadioInfo = ClientStateSingleton.Instance.PlayerUnitState;

        if (dcsPlayerRadioInfo != null &&
            radio < dcsPlayerRadioInfo.Radios.Count && radio >= 0)
            return dcsPlayerRadioInfo.Radios[radio];

        return null;
    }


    public static void SelectNextRadio()
    {
        var dcsPlayerRadioInfo = ClientStateSingleton.Instance.PlayerUnitState;

        if (dcsPlayerRadioInfo != null &&
            dcsPlayerRadioInfo.control == PlayerUnitState.RadioSwitchControls.HOTAS)
        {
            if (dcsPlayerRadioInfo.SelectedRadio < 0
                || dcsPlayerRadioInfo.SelectedRadio > dcsPlayerRadioInfo.Radios.Count
                || dcsPlayerRadioInfo.SelectedRadio + 1 > dcsPlayerRadioInfo.Radios.Count)
            {
                SelectRadio(1);

                return;
            }

            int currentRadio = dcsPlayerRadioInfo.SelectedRadio;

            //find next radio
            for (var i = currentRadio + 1; i < dcsPlayerRadioInfo.Radios.Count; i++)
                if (SelectRadio(i))
                    return;

            //search up to current radio
            for (var i = 1; i < currentRadio; i++)
                if (SelectRadio(i))
                    return;
        }
    }

    public static void SelectPreviousRadio()
    {
        var dcsPlayerRadioInfo = ClientStateSingleton.Instance.PlayerUnitState;

        if (dcsPlayerRadioInfo != null &&
            dcsPlayerRadioInfo.control == PlayerUnitState.RadioSwitchControls.HOTAS)
        {
            if (dcsPlayerRadioInfo.SelectedRadio < 0
                || dcsPlayerRadioInfo.SelectedRadio > dcsPlayerRadioInfo.Radios.Count)
            {
                dcsPlayerRadioInfo.SelectedRadio = 1;
                return;
            }

            int currentRadio = dcsPlayerRadioInfo.SelectedRadio;

            //find previous radio
            for (var i = currentRadio - 1; i > 0; i--)
                if (SelectRadio(i))
                    return;

            //search down to current radio
            for (var i = dcsPlayerRadioInfo.Radios.Count; i > currentRadio; i--)
                if (SelectRadio(i))
                    return;
        }
    }

    public static void SelectRadioChannel(PresetChannel selectedPresetChannel, int radioId)
    {
        if (UpdateRadioFrequency((double)selectedPresetChannel.Value, radioId, false, false))
        {
            var radio = GetRadio(radioId);

            if (radio != null) radio.CurrentChannel = selectedPresetChannel;
        }
    }

    public static void RadioChannelUp(int radioId)
    {
        var currentRadio = GetRadio(radioId);

        if (currentRadio != null)
            if (currentRadio.Modulation != Modulation.DISABLED &&
                currentRadio.Modulation != Modulation.INTERCOM
                && ClientStateSingleton.Instance.PlayerUnitState.control ==
                PlayerUnitState.RadioSwitchControls.HOTAS)
            {
                var currentRadioPresetChannels = currentRadio.PresetChannels;
                var currentChannel = currentRadio.CurrentChannel;

                try
                {
                    var nexPresetChannel = currentRadioPresetChannels[currentChannel.Channel + 1];
                    currentRadio.SelectRadioChannel(nexPresetChannel);
                }
                catch
                {
                }
            }
    }

    public static void RadioChannelDown(int radioId)
    {
        var currentRadio = GetRadio(radioId);

        if (currentRadio != null)
            if (currentRadio.Modulation != Modulation.DISABLED &&
                currentRadio.Modulation != Modulation.INTERCOM
                && ClientStateSingleton.Instance.PlayerUnitState.control ==
                PlayerUnitState.RadioSwitchControls.HOTAS)
            {
                var currentRadioPresetChannels = currentRadio.PresetChannels;
                var currentChannel = currentRadio.CurrentChannel;

                if (currentChannel.Channel > 1)
                    try
                    {
                        var prevPresetChannel = currentRadioPresetChannels[currentChannel.Channel - 1];
                        currentRadio.SelectRadioChannel(prevPresetChannel);
                    }
                    catch
                    {
                    }
            }
    }

    public static void SetRadioVolume(float volume, int radioId)
    {
        if (volume > 1.0)
            volume = 1.0f;
        else if (volume < 0) volume = 0;

        var currentRadio = GetRadio(radioId);

        if (currentRadio != null
            && currentRadio.Modulation != Modulation.DISABLED
            && currentRadio.Config.VolumeControl == RadioConfig.VolumeMode.OVERLAY)
            currentRadio.Volume = volume;
    }

    public static void VolumeUp(int radioId)
    {
        var currentRadio = GetRadio(radioId);

        if (currentRadio != null
            && currentRadio.Modulation != Modulation.DISABLED
            && currentRadio.Config.VolumeControl == RadioConfig.VolumeMode.OVERLAY)
        {
            currentRadio.Volume = currentRadio.Volume + 0.05f;

            if (currentRadio.Volume > 1.0)
                currentRadio.Volume = 1.0f;
            else if (currentRadio.Volume < 0) currentRadio.Volume = 0;
        }
    }

    public static void VolumeDown(int radioId)
    {
        var currentRadio = GetRadio(radioId);

        if (currentRadio != null
            && currentRadio.Modulation != Modulation.DISABLED
            && currentRadio.Config.VolumeControl == RadioConfig.VolumeMode.OVERLAY)
        {
            currentRadio.Volume = currentRadio.Volume - 0.05f;

            if (currentRadio.Volume > 1.0f)
                currentRadio.Volume = 1.0f;
            else if (currentRadio.Volume < 0f) currentRadio.Volume = 0f;
        }
    }

    public static void ToggleRetransmit(int radioId)
    {
        var radio = GetRadio(radioId);

        if (radio != null)
            if (radio.Config.RetransmitControl == RadioConfig.RetransmitMode.OVERLAY)
                radio.Retransmit = !radio.Retransmit;

        //make radio data stale to force resysnc
        // ClientStateSingleton.Instance.LastSent = 0;
    }


    public static void ToggleHotMic(short radioId)
    {
        var radio = GetRadio(radioId);

        if (radio != null)
            if (radio.Modulation == Modulation.INTERCOM)
                ClientStateSingleton.Instance.PlayerUnitState.IntercomHotMic =
                    !ClientStateSingleton.Instance.PlayerUnitState.IntercomHotMic;
    }
}