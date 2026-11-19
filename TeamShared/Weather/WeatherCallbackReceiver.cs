using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TeamShared.Weather;

/// <summary>
/// Leichtgewichtiger HTTP-Empfänger für Wetter-Push-Snapshots in Feldgeräten (PV, Windrad).
/// Bindet dynamisch ab dem Wunsch-Port (z. B. 6000 für PV, 6100 für Wind) und registriert sich beim WeatherSimulator.
/// </summary>
public sealed class WeatherCallbackReceiver : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpListener? _httpListener;
    private readonly TcpListener? _tcpListener;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<DateTime, WeatherSnapshotDto> _snapshots = new();
    private readonly SemaphoreSlim _signal = new(0);

    private Task? _listenerLoopTask;
    private bool _isDisposed;

    public int BoundPort { get; }
    public string CallbackUrl { get; }
    public bool IsRegistered { get; private set; }
    public WeatherSnapshotDto? LatestSnapshot { get; private set; }

    private WeatherCallbackReceiver(int port, HttpListener? httpListener, TcpListener? tcpListener)
    {
        BoundPort = port;
        _httpListener = httpListener;
        _tcpListener = tcpListener;
        CallbackUrl = $"http://127.0.0.1:{port}/weather/";
    }

    /// <summary>
    /// Startet den Callback-Empfänger ab dem angegebenen Basis-Port (sucht aufsteigend nach einem freien Port).
    /// </summary>
    public static async Task<WeatherCallbackReceiver> StartAsync(int startPort, string deviceType, CancellationToken cancellationToken = default)
    {
        for (int port = startPort; port < startPort + 50; port++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Versuch 1: Standard HttpListener
            try
            {
                var httpListener = new HttpListener();
                httpListener.Prefixes.Add($"http://127.0.0.1:{port}/weather/");
                httpListener.Prefixes.Add($"http://localhost:{port}/weather/");
                httpListener.Start();

                var receiver = new WeatherCallbackReceiver(port, httpListener, null);
                receiver.StartLoop();
                return receiver;
            }
            catch (HttpListenerException)
            {
                // Port belegt oder fehlende URL-ACL Rechte auf Windows -> versuche Fallback auf TcpListener
            }
            catch (SocketException)
            {
                // Port belegt
                continue;
            }

            // Versuch 2: Leichtgewichtiger Managed TcpListener (ohne URL-ACL Berechtigungsprobleme)
            try
            {
                var tcpListener = new TcpListener(IPAddress.Any, port);
                tcpListener.Start();

                var receiver = new WeatherCallbackReceiver(port, null, tcpListener);
                receiver.StartLoop();
                return receiver;
            }
            catch (SocketException)
            {
                // Port belegt, nächsten Port probieren
            }
        }

        throw new IOException($"Kein freier Port für {deviceType}-Wetter-Callback zwischen {startPort} und {startPort + 50} gefunden.");
    }

    private void StartLoop()
    {
        _listenerLoopTask = _httpListener != null ? RunHttpListenerLoopAsync(_cts.Token) : RunTcpListenerLoopAsync(_cts.Token);
    }

    private async Task RunHttpListenerLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _httpListener != null && _httpListener.IsListening)
        {
            try
            {
                var context = await _httpListener.GetContextAsync().ConfigureAwait(false);
                _ = HandleHttpContextAsync(context, token);
            }
            catch (Exception) when (token.IsCancellationRequested || !_httpListener.IsListening)
            {
                break;
            }
            catch
            {
                // Fehler beim Entgegennehmen einzelner Requests
            }
        }
    }

    private async Task HandleHttpContextAsync(HttpListenerContext context, CancellationToken token)
    {
        try
        {
            if (context.Request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                var body = await reader.ReadToEndAsync(token).ConfigureAwait(false);
                var snapshot = JsonSerializer.Deserialize<WeatherSnapshotDto>(body, JsonOptions);
                if (snapshot != null)
                {
                    RecordSnapshot(snapshot);
                }

                context.Response.StatusCode = (int)HttpStatusCode.OK;
                var bytes = Encoding.UTF8.GetBytes("{\"status\":\"received\"}");
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes, token).ConfigureAwait(false);
            }
            else
            {
                context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
            }
        }
        catch
        {
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        }
        finally
        {
            try { context.Response.Close(); } catch { }
        }
    }

    private async Task RunTcpListenerLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _tcpListener != null)
        {
            try
            {
                var client = await _tcpListener.AcceptTcpClientAsync(token).ConfigureAwait(false);
                _ = HandleTcpClientAsync(client, token);
            }
            catch (Exception) when (token.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // Fehler beim Verbindungsaufbau
            }
        }
    }

    private async Task HandleTcpClientAsync(TcpClient client, CancellationToken token)
    {
        using (client)
        await using (var stream = client.GetStream())
        {
            try
            {
                using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                var headerLines = new StringBuilder();
                string? line;
                int contentLength = 0;

                while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync(token).ConfigureAwait(false)))
                {
                    headerLines.AppendLine(line);
                    if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = line.Split(':');
                        if (parts.Length > 1 && int.TryParse(parts[1].Trim(), out var len))
                            contentLength = len;
                    }
                }

                if (contentLength > 0)
                {
                    var buffer = new char[contentLength];
                    var readTotal = 0;
                    while (readTotal < contentLength)
                    {
                        var read = await reader.ReadAsync(buffer, readTotal, contentLength - readTotal).ConfigureAwait(false);
                        if (read == 0) break;
                        readTotal += read;
                    }

                    var body = new string(buffer, 0, readTotal);
                    var snapshot = JsonSerializer.Deserialize<WeatherSnapshotDto>(body, JsonOptions);
                    if (snapshot != null)
                    {
                        RecordSnapshot(snapshot);
                    }
                }

                var responseBytes = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: 21\r\nConnection: close\r\n\r\n{\"status\":\"received\"}");
                await stream.WriteAsync(responseBytes, token).ConfigureAwait(false);
            }
            catch
            {
                // Fehler bei Socket-Verarbeitung
            }
        }
    }

    public void RecordSnapshot(WeatherSnapshotDto snapshot)
    {
        var utcKey = snapshot.Timestamp.UtcDateTime;
        _snapshots[utcKey] = snapshot;
        LatestSnapshot = snapshot;

        // Wartende Threads benachrichtigen
        if (_signal.CurrentCount == 0)
        {
            _signal.Release();
        }

        // Alte Einträge (> 10 Minuten) aufräumen
        var threshold = DateTime.UtcNow.AddMinutes(-10);
        foreach (var key in _snapshots.Keys.Where(k => k < threshold))
        {
            _snapshots.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Wartet auf den Wetter-Snapshot für die exakte Master-Zielzeit (mit konfigurierbarem Timeout).
    /// Gibt null zurück, wenn innerhalb des Timeouts kein passender Snapshot eingetroffen ist (z. B. für Offline-Fallback).
    /// </summary>
    public async Task<WeatherSnapshotDto?> WaitForSnapshotAsync(DateTime targetVirtualTime, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var targetUtc = DateTime.SpecifyKind(targetVirtualTime, DateTimeKind.Utc);
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Suche Snapshot mit Zeitstempel innerhalb von +/- 1.5 Sekunden um die Zielzeit
            var match = _snapshots.Values.FirstOrDefault(s => Math.Abs((s.Timestamp.UtcDateTime - targetUtc).TotalSeconds) <= 1.5);
            if (match != null)
                return match;

            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
                break;

            try
            {
                await _signal.WaitAsync(remaining, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        // Fallback-Prüfung: Gab es während des Wartens einen passenden Eintrag?
        return _snapshots.Values.FirstOrDefault(s => Math.Abs((s.Timestamp.UtcDateTime - targetUtc).TotalSeconds) <= 1.5);
    }

    /// <summary>
    /// Registriert dieses Feldgerät beim WeatherSimulator über HTTP POST /api/weather/register.
    /// </summary>
    public async Task<bool> RegisterWithWeatherSimulatorAsync(
        string weatherSimulatorUrl,
        string deviceId,
        string deviceType,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        var registration = new DeviceWeatherRegistration
        {
            DeviceId = deviceId,
            DeviceType = deviceType,
            CallbackUrl = CallbackUrl,
            Latitude = latitude,
            Longitude = longitude
        };

        var endpoint = $"{weatherSimulatorUrl.TrimEnd('/')}/api/weather/register";
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var response = await client.PostAsJsonAsync(endpoint, registration, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                IsRegistered = true;
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Startet eine asynchrone Hintergrund-Schleife, die periodisch versucht, das Gerät beim WeatherSimulator zu registrieren,
    /// falls der WeatherSimulator beim Start des Simulators noch nicht erreichbar war.
    /// </summary>
    public void StartAutoRegistration(string weatherSimulatorUrl, string deviceId, string deviceType, double latitude, double longitude)
    {
        _ = Task.Run(async () =>
        {
            while (!_isDisposed && !IsRegistered)
            {
                try
                {
                    var ok = await RegisterWithWeatherSimulatorAsync(weatherSimulatorUrl, deviceId, deviceType, latitude, longitude, _cts.Token).ConfigureAwait(false);
                    if (ok)
                        break;
                }
                catch
                {
                    // Ignorieren und erneut versuchen
                }

                try
                {
                    await Task.Delay(2000, _cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _cts.Cancel();

        try
        {
            _httpListener?.Stop();
            _httpListener?.Close();
        }
        catch { }

        try
        {
            _tcpListener?.Stop();
        }
        catch { }

        if (_listenerLoopTask != null)
        {
            try
            {
                await _listenerLoopTask.ConfigureAwait(false);
            }
            catch { }
        }

        _signal.Dispose();
        _cts.Dispose();
    }
}
