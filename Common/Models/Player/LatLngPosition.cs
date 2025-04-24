using System;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;

public class LatLngPosition
{
    public double alt;
    public double lat;
    public double lng;

    protected bool Equals(LatLngPosition other)
    {
        return lat.Equals(other.lat) && lng.Equals(other.lng) && alt.Equals(other.alt);
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((LatLngPosition)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(lat, lng, alt);
    }

    public bool IsValid()
    {
        return lat != 0 && lng != 0;
    }

    public override string ToString()
    {
        return $"Pos:[{lat},{lng},{alt}]";
    }

    public LatLngPosition DeepClone()
    {
        return new LatLngPosition
        {
            lat = lat,
            lng = lng,
            alt = alt
        };
    }
}