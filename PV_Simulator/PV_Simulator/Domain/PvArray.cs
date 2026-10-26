using System;

namespace PV_Simulator.Domain;

public sealed record PvArray
{
    public PvArray(
        string deviceId,
        double moduleNominalPowerKilowattsPeak,
        double tiltDegrees,
        double azimuthDegrees,
        double systemLossFraction = 0.14)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        if (moduleNominalPowerKilowattsPeak <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(moduleNominalPowerKilowattsPeak));
        }

        if (tiltDegrees is < 0 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(tiltDegrees));
        }

        if (azimuthDegrees is < 0 or >= 360)
        {
            throw new ArgumentOutOfRangeException(nameof(azimuthDegrees));
        }

        if (systemLossFraction is < 0 or >= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(systemLossFraction));
        }

        DeviceId = deviceId.Trim();
        ModuleNominalPowerKilowattsPeak = moduleNominalPowerKilowattsPeak;
        TiltDegrees = tiltDegrees;
        AzimuthDegrees = azimuthDegrees;
        SystemLossFraction = systemLossFraction;
    }

    public string DeviceId { get; }
    public double ModuleNominalPowerKilowattsPeak { get; }
    public double TiltDegrees { get; }
    public double AzimuthDegrees { get; }
    public double SystemLossFraction { get; }
}