using System;
using Dual_Port_Charger;
using Xunit;

namespace Dual_Port_Charger.Tests;

public class DeviceLoadBalancingTests
{
    [Fact]
    public void LoadBalancing_GivesSurplusToHungrierCar_WhenOneCarNeedsLess()
    {
        // Arrange
        // Standardmäßig: Port 1 = Model S (will 142 kW), Port 2 = Model Y (will 250 kW)
        // Gesamtleistung der Station: 300 kW
        var device = new Device();

        // Act
        // Zeitschritt 0 berechnen, damit nur das Load Balancing ausgeführt wird
        device.UpdateManualMode(TimeSpan.Zero);

        // Assert
        // Fair-Share-Berechnung:
        // 1. Port 1 will 142 kW (< 150 kW Fair-Share). Er bekommt volle 142 kW.
        // 2. Die verbleibenden 158 kW (300 kW - 142 kW) gehen komplett an Port 2!
        Assert.Equal(142000m, device.ChargingPoints[0].AvailablePower);
        Assert.Equal(158000m, device.ChargingPoints[1].AvailablePower);

        // Summe darf Stationslimit nicht überschreiten
        decimal totalAllocated = device.ChargingPoints[0].AvailablePower + device.ChargingPoints[1].AvailablePower;
        Assert.Equal(300000m, totalAllocated);
    }

    [Fact]
    public void LoadBalancing_Splits5050_WhenBothCarsRequestMoreThanHalf()
    {
        // Arrange: Beide Ladepunkte wollen mehr als 150 kW
        var device = new Device();
        device.ChargingPoints[0].AssignedEV = EVModelCatalog.PorscheTaycan; // Fordert 270 kW an
        device.ChargingPoints[0].SOC = 0.10m;
        device.ChargingPoints[1].AssignedEV = EVModelCatalog.ModelY2026;     // Fordert 250 kW an
        device.ChargingPoints[1].SOC = 0.10m;

        // Act
        device.UpdateManualMode(TimeSpan.Zero);

        // Assert: Beide fordern > 150 kW, also wird exakt 50/50 (150 kW pro Port) geteilt
        Assert.Equal(150000m, device.ChargingPoints[0].AvailablePower);
        Assert.Equal(150000m, device.ChargingPoints[1].AvailablePower);
    }

    [Fact]
    public void SetPowerLimit_RestrictsAvailablePowerForBothPorts()
    {
        // Arrange: Station wird extern (z.B. via Modbus / RabbitMQ) auf 100 kW gedrosselt
        var device = new Device();
        device.SetPowerLimit(100000m);

        // Act
        device.UpdateManualMode(TimeSpan.Zero);

        // Assert: Jeder Port bekommt bei hohem Bedarf maximal 50 kW
        Assert.Equal(50000m, device.ChargingPoints[0].AvailablePower);
        Assert.Equal(50000m, device.ChargingPoints[1].AvailablePower);
        Assert.Equal(100000m, device.Power);
    }

    [Fact]
    public void SetPowerLimit_ClampsNegativeValuesToZero()
    {
        // Arrange & Act: Negatives Limit darf nicht zu negativer Leistung führen
        var device = new Device();
        device.SetPowerLimit(-50000m);

        // Assert: Mindestens 0 W
        Assert.Equal(0m, device.Power);
    }

    [Fact]
    public void AdjustManualPower_IncreasesAndClampsPowerWithinValidBounds()
    {
        // Arrange: Port 1 hat 100 kW AvailablePower und will 142 kW (maxAllowed = 100 kW)
        var device = new Device();
        device.ChargingPoints[0].AvailablePower = 100000m;
        device.ChargingPoints[0].ActivePower = 50000m;
        device.ChargingPoints[1].IsConnected = false; // Port 2 ausblenden

        // Act 1: Manuell um 30 kW erhöhen
        device.AdjustManualPower(30000m);
        // Assert 1: Auf 80 kW gestiegen
        Assert.Equal(80000m, device.ChargingPoints[0].ActivePower);

        // Act 2: Manuell um weitere 50 kW erhöhen (würde 130 kW ergeben, max ist aber 100 kW)
        device.AdjustManualPower(50000m);
        // Assert 2: Gekappt bei 100 kW
        Assert.Equal(100000m, device.ChargingPoints[0].ActivePower);

        // Act 3: Manuell um 200 kW senken
        device.AdjustManualPower(-200000m);
        // Assert 3: Gekappt bei 0 W (keine negative Leistung)
        Assert.Equal(0m, device.ChargingPoints[0].ActivePower);
    }

    [Fact]
    public void AdjustManualPower_DoesNothing_WhenFaultIsActive()
    {
        // Arrange: Gestörte Station
        var device = new Device();
        device.TriggerFault();
        device.ChargingPoints[0].ActivePower = 0m;

        // Act: Versuchen, manuelle Ladeleistung zu erhöhen
        device.AdjustManualPower(50000m);

        // Assert: Bei Störung darf keine Leistung zugeschaltet werden
        Assert.Equal(0m, device.ChargingPoints[0].ActivePower);
    }

    [Fact]
    public void EnforceManualModeCars_ResetsBothPortsToTeslasWithFullTargetSoc()
    {
        // Arrange: Ports verändern (z.B. Taycan und VW ID.4 mit 70% Ziel)
        var device = new Device();
        device.ChargingPoints[0].AssignedEV = EVModelCatalog.PorscheTaycan;
        device.ChargingPoints[0].TargetSOC = 0.70m;
        device.ChargingPoints[1].AssignedEV = EVModelCatalog.VWID4;
        device.ChargingPoints[1].TargetSOC = 0.70m;

        // Act: Manuellen Modus erzwingen
        device.EnforceManualModeCars();

        // Assert: Müssen wieder Model S und Model Y mit 100% Ziel sein
        Assert.Equal("Model S 100D (2018)", device.ChargingPoints[0].AssignedEV.Name);
        Assert.Equal(1.0m, device.ChargingPoints[0].TargetSOC);
        Assert.Equal("Model Y LR (2026)", device.ChargingPoints[1].AssignedEV.Name);
        Assert.Equal(1.0m, device.ChargingPoints[1].TargetSOC);
    }

    [Fact]
    public void ToggleCar_TogglesOnlySelectedPort()
    {
        // Arrange: Beide Ports sind initial verbunden
        var device = new Device();
        Assert.True(device.ChargingPoints[0].IsConnected);
        Assert.True(device.ChargingPoints[1].IsConnected);

        // Act: Nur Port 1 umschalten
        device.ToggleCar(1);

        // Assert: Port 1 ist getrennt, Port 2 bleibt unberührt verbunden
        Assert.False(device.ChargingPoints[0].IsConnected);
        Assert.True(device.ChargingPoints[1].IsConnected);
    }

    [Fact]
    public void TriggerFault_PropagatesToAllChargingPoints()
    {
        // Arrange
        var device = new Device();

        // Act: Station auf Störung setzen
        device.TriggerFault();

        // Assert: Station und beide Ports müssen im Fehlerzustand sein
        Assert.True(device.Fault);
        Assert.Equal("Störung", device.Status);
        Assert.True(device.ChargingPoints[0].Fault);
        Assert.True(device.ChargingPoints[1].Fault);
    }

    [Fact]
    public void ResetFault_RestoresNormalOperationOnAllChargingPoints()
    {
        // Arrange: Erst Fehler auslösen
        var device = new Device();
        device.TriggerFault();

        // Act: Fehler quittieren
        device.ResetFault();

        // Assert: Station und beide Ports sind wieder gesund
        Assert.False(device.Fault);
        Assert.Equal("Funktioniert", device.Status);
        Assert.False(device.ChargingPoints[0].Fault);
        Assert.False(device.ChargingPoints[1].Fault);
    }

    [Fact]
    public void ActivePowerAndEnergy_SumAcrossAllPorts()
    {
        // Arrange
        var device = new Device();
        device.ChargingPoints[0].ActivePower = 60000m;
        device.ChargingPoints[0].Energy = 12000m;
        device.ChargingPoints[1].ActivePower = 40000m;
        device.ChargingPoints[1].Energy = 8000m;

        // Act & Assert: Stationssummen müssen exakt übereinstimmen
        Assert.Equal(100000m, device.ActivePower);
        Assert.Equal(20000m, device.Energy);
    }
}
