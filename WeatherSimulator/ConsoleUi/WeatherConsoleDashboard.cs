using System;
using System.Collections.Generic;
using System.Linq;
using Spectre.Console;
using TeamConsoleShared;
using TeamConsoleShared.Components;
using TeamConsoleShared.Layout;
using WeatherSimulator.Models;
using WeatherSimulator.Services;

namespace WeatherSimulator.ConsoleUi;

public sealed class WeatherConsoleDashboard
{
    private readonly int _port;
    private readonly string _masterHost;
    private readonly int _masterPort;
    private readonly string _csvPath;
    private readonly WeatherDataset _dataset;
    private readonly LocationWeatherSeries? _primarySeries;
    private string? _lastLayoutKey;

    public WeatherConsoleDashboard(
        int port,
        string masterHost,
        int masterPort,
        string csvPath,
        WeatherDataset dataset)
    {
        _port = port;
        _masterHost = masterHost;
        _masterPort = masterPort;
        _csvPath = csvPath;
        _dataset = dataset;
        _primarySeries = dataset.SeriesByLocation.Count > 0 ? dataset.SeriesByLocation[0] : null;
    }

    public void Render(
        IWeatherService weatherService,
        WeatherSimulationClock clock,
        IWeatherClientRegistry clientRegistry,
        IWeatherActivityLogger activityLogger)
    {
        var isMasterConnected = clock.IsMasterConnected;
        var simTime = clock.VirtualTime ?? DateTime.UtcNow;
        var timeStep = clock.TimeStep;
        var masterStatus = isMasterConnected ? $"Online ({_masterHost}:{_masterPort})" : "Offline";
        var clientDetails = clientRegistry.GetDetails();

        // Referenz-Wetterdaten an der primären Messstation (Salzburg)
        WeatherResponse? refWeather = null;
        if (_primarySeries is not null)
        {
            try
            {
                refWeather = weatherService.GetWeather(_primarySeries.Latitude, _primarySeries.Longitude, simTime);
            }
            catch
            {
                // Fallback bei Berechnungsfehlern
            }
        }

        if (Console.IsOutputRedirected || Console.IsErrorRedirected)
        {
            var tempStr = refWeather is not null ? $"{refWeather.TemperatureC:F1}°C" : "--";
            var cloudStr = refWeather is not null ? $"{refWeather.CloudCoverPercent:F0}%" : "--";
            Console.WriteLine($"[WEATHER] {simTime:yyyy-MM-dd HH:mm:ss} | Master: {masterStatus} | Clients: {clientDetails.Count} | Salzburg: {tempStr}, Wolken: {cloudStr} | GET: {activityLogger.TotalGetRequests} | Push: {activityLogger.TotalPushes}");
            return;
        }

        var isCompact = Console.WindowWidth < 95;
        var timePattern = isCompact ? "HH:mm:ss" : "yyyy-MM-dd HH:mm:ss";

        // Layout-Status ermitteln: Nur bei Änderungen (z.B. Fenstergröße, Alert-Status, Detailtabelle) Console.Clear ausführen,
        // um Flackern bei den normalen 1-Sekunden-Ticks vollständig zu verhindern.
        var hasAlert = !isMasterConnected;
        var showDetailsTable = Console.WindowHeight >= 32 && clientDetails.Count > 0;
        var clientCountKey = Math.Min(3, clientDetails.Count);
        var layoutKey = $"{Console.WindowWidth}x{Console.WindowHeight}_Alert:{hasAlert}_Details:{showDetailsTable}_{clientCountKey}";

        var forceClear = _lastLayoutKey != layoutKey;
        if (forceClear)
        {
            _lastLayoutKey = layoutKey;
        }

        var dashboard = new SimulationDashboard()
            .WithHeader(h =>
            {
                h.Title = "🌤️ Wetter-Simulator (Digital Twin Provider)";
                h.Subtitle = "Lokaler Wetter- & Sonnendaten-Dienst (Salzburg)";
                h.ModbusPort = _port;
                h.SimulatedTime = simTime;
                h.TimeFormat = timePattern;
                h.SpeedDescription = isMasterConnected ? $"{timeStep.TotalSeconds:0.##}s / Tick" : "Warte auf Master...";
                h.ModeBadgeMarkup = StatusBadge.Info($"HTTP :{_port}");
                h.StatusBadgeMarkup = isMasterConnected
                    ? StatusBadge.Custom("MASTER ONLINE", "black", "chartreuse3")
                    : StatusBadge.Custom("MASTER OFFLINE", "black", "darkorange");
                h.AccentColor = SimulationTheme.WeatherAccent;
            });

        if (!isMasterConnected)
        {
            dashboard.WithAlert(
                $"MASTER OFFLINE: Warte auf Simulation-Master ({_masterHost}:{_masterPort})",
                "bold black on yellow");
        }

        // Spalte 1: Wetter & Sonnenstand (kompakt, zeilenstabil ohne Umbruch)
        var weatherMetrics = new List<MetricItem>();
        if (refWeather is not null && _primarySeries is not null)
        {
            var isDay = refWeather.Elevation > 0;
            var sunShort = $"{refWeather.Elevation:F0}° El / {refWeather.Azimuth:F0}° Az ({(isDay ? "Tag ☀️" : "Nacht 🌙")})";
            var locationText = isCompact ? "Salzburg" : $"Salzburg ({_primarySeries.Latitude:F1}°, {_primarySeries.Longitude:F1}°)";

            weatherMetrics.Add(MetricItem.Text("Standort", locationText, "bold white", "📍"));
            weatherMetrics.Add(MetricItem.Number("Temperatur", refWeather.TemperatureC, "°C", refWeather.TemperatureC > 0 ? "bold gold1" : "bold deepskyblue1", "0.0", "🌡️"));
            weatherMetrics.Add(MetricItem.MarkupValue("Bewölkung", GaugeBar.Create(refWeather.CloudCoverPercent / 100.0, 8, "grey74", "grey27", suffix: $"{refWeather.CloudCoverPercent:F0}%"), icon: "☁️"));
            weatherMetrics.Add(MetricItem.Number("Wind", refWeather.WindSpeedMps, "m/s", "bold deepskyblue1", "0.0", "💨"));
            weatherMetrics.Add(MetricItem.Number("Niederschlag", refWeather.PrecipitationMm, "mm", refWeather.PrecipitationMm > 0 ? "bold dodgerblue1" : "grey74", "0.0", "🌧️"));
            weatherMetrics.Add(MetricItem.Text("Sonnenstand", sunShort, isDay ? "bold yellow" : "dim grey", "☀️"));
        }
        else
        {
            weatherMetrics.Add(MetricItem.Text("Status", "Keine Daten geladen", "bold red", "⚠️"));
        }

        // Spalte 2: Dienst & Zeitsynchronisation
        var clientSummary = clientDetails.Count == 0
            ? "0 (Warte auf Geräte)"
            : $"{clientDetails.Count} aktiv ({string.Join(", ", clientDetails.Take(2).Select(c => c.Registration.DeviceId))})";

        var latestLog = activityLogger.RecentLogs.Count > 0
            ? activityLogger.RecentLogs[^1]
            : "[grey]Bereit[/]";

        var serviceMetrics = new List<MetricItem>
        {
            MetricItem.Text(isCompact ? "Virt. Zeit" : "Virtuelle Zeit", simTime.ToString(timePattern), "bold gold1", "⏱️"),
            MetricItem.Text("Master-Sync", isMasterConnected ? $"Online ({_masterPort})" : "Offline (Retry)", isMasterConnected ? "bold chartreuse3" : "bold darkorange", "🔗"),
            MetricItem.Number("CSV-Basis", _dataset.TotalRecords, "Punkte", "bold cyan", "#,##0", "📊"),
            MetricItem.Text("Feldgeräte", clientSummary, clientDetails.Count > 0 ? "bold green3" : "grey74", "🔌"),
            MetricItem.Text("Traffic", $"{activityLogger.TotalGetRequests} GETs / {activityLogger.TotalPushes} Pushes", "bold white", "📥"),
            MetricItem.MarkupValue("Letztes Event", latestLog, icon: "📋")
        };

        dashboard.AddTwoColumns(
            ("Wetter & Sonnenstand (Salzburg)", MetricGrid.Create(weatherMetrics, 1)),
            ("Dienst & Zeitsynchronisation", MetricGrid.Create(serviceMetrics, 1))
        );

        // Optionale Detail-Tabelle nur wenn das Terminal hoch genug ist (>= 32 Zeilen), um Scrollen zu verhindern
        if (!Console.IsOutputRedirected && Console.WindowHeight >= 32 && clientDetails.Count > 0)
        {
            var clientTable = new Table()
                .Border(TableBorder.Rounded)
                .BorderStyle(new Style(foreground: SimulationTheme.WeatherAccent))
                .Expand();

            clientTable.AddColumn(new TableColumn("[bold grey85]Feldgerät[/]").LeftAligned());
            clientTable.AddColumn(new TableColumn("[bold grey85]Typ[/]").Centered());
            clientTable.AddColumn(new TableColumn("[bold grey85]Callback-URL[/]").LeftAligned());
            clientTable.AddColumn(new TableColumn("[bold grey85]Status[/]").Centered());

            foreach (var client in clientDetails.OrderBy(c => c.Registration.DeviceId).Take(3))
            {
                var reg = client.Registration;
                var typeIcon = reg.DeviceType.ToLowerInvariant() switch
                {
                    "pv" => "☀️ PV",
                    "wind" => "💨 Wind",
                    _ => reg.DeviceType
                };

                string pushStatusMarkup;
                if (!client.LastPushAt.HasValue)
                {
                    pushStatusMarkup = "[yellow]Warte auf Push[/]";
                }
                else
                {
                    var ageSeconds = (DateTime.Now - client.LastPushAt.Value).TotalSeconds;
                    var ageText = ageSeconds < 60 ? $"{ageSeconds:F0}s" : $"{(int)(ageSeconds / 60)}m";
                    pushStatusMarkup = client.LastPushSuccess == true
                        ? $"[bold chartreuse3]OK[/] [grey]({ageText} her)[/]"
                        : $"[bold red]Fehler[/] [grey]({ageText} her)[/]";
                }

                clientTable.AddRow(
                    new Markup($"[bold white]{Markup.Escape(reg.DeviceId)}[/]"),
                    new Markup($"[cyan]{Markup.Escape(typeIcon)}[/]"),
                    new Markup($"[grey]{Markup.Escape(reg.CallbackUrl)}[/]"),
                    new Markup(pushStatusMarkup)
                );
            }

            dashboard.AddSection("Registrierte Feldgeräte (Push-Abonnenten)", clientTable);
        }

        dashboard.WithKeyBindings(
            ("Q / X", "Beenden"),
            ("R", "Aktualisieren")
        );

        dashboard.Render(forceClear);
    }
}
