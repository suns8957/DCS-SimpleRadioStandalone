using Ciribob.SRS.Common.Network.Models;

namespace Ciribob.FS3D.SimpleRadio.Standalone.Client.Singletons.Models;

public class RadioReceivingPriority
{
    public double Frequency;
    public float LineOfSightLoss;
    public short Modulation;
    public double ReceivingPowerLossPercent;
    public Radio ReceivingRadio;

    public RadioReceivingState ReceivingState;
}