using System;

namespace PV_Simulator.Domain;

public sealed record Location
{
    public Location(string name, double latitude, double longitude, double elevationMeters = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (latitude is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude));
        }

        if (longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude));
        }

        Name = name;
        Latitude = latitude;
        Longitude = longitude;
        ElevationMeters = elevationMeters;
    }

    public string Name { get; }
    public double Latitude { get; }
    public double Longitude { get; }
    public double ElevationMeters { get; }
}