using Ciribob.DCS.SimpleRadio.Standalone.Common.Models;

namespace Ciribob.DCS.SimpleRadio.Standalone.Mobile.Models;

public class RadioReceivingPriority
{
    public double Frequency;
    public float LineOfSightLoss;
    public short Modulation;
    public double ReceivingPowerLossPercent;
    public Radio ReceivingRadio;

    public RadioReceivingState ReceivingState;
}