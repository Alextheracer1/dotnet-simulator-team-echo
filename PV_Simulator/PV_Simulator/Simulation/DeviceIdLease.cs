using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace PV_Simulator.Simulation;

public sealed class DeviceIdLease : IDisposable
{
    private readonly FileStream _lockStream;

    public DeviceIdLease(string deviceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);

        var normalizedDeviceId = deviceId.Trim().ToUpperInvariant();
        var deviceIdHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedDeviceId)));
        var appDataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(appDataDirectory))
        {
            appDataDirectory = Path.GetTempPath();
        }

        var lockDirectory = Path.Combine(appDataDirectory, "PV_Simulator", "active-device-ids");
        Directory.CreateDirectory(lockDirectory);
        var lockPath = Path.Combine(lockDirectory, $"{deviceIdHash}.lock");

        try
        {
            _lockStream = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException exception)
        {
            throw new InvalidOperationException(
                $"Die Geräte-ID '{deviceId}' wird bereits von einer laufenden Simulation verwendet.",
                exception);
        }
    }

    public void Dispose() => _lockStream.Dispose();
}
