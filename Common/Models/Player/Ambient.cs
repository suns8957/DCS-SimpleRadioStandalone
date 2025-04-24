namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;

public class Ambient
{
    //  public float pitch = 1.0f;
    public string abType = "";
    public float vol = 1.0f;

    public override bool Equals(object obj)
    {
        if (obj == null || GetType() != obj.GetType())
            return false;

        var compare = (Ambient)obj;

        if (vol != compare.vol) return false;

        if (abType != compare.abType) return false;

        return true;
    }

    public Ambient Copy()
    {
        return new Ambient
        {
            vol = vol,
            abType = abType
        };
    }
}