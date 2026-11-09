using System;
using Dual_Port_Charger;
using Xunit;

namespace Dual_Port_Charger.Tests;

public class ChargingPointTests
{
    [Fact]
    public void CalculatePhysics_AddsAccurateEnergyAndIncreasesSoc()
    {
        // Arrange: Port mit Model S (100 kWh Batterie) bei 10 % Ladestand
        var port = new ChargingPoint
        {
            Id = 1,
            IsConnected = true,
            AssignedEV = EVModelCatalog.ModelS100D,
            SOC = 0.10m,
            TargetSOC = 0.80m,
            AvailablePower = 50000m // 50 kW freigegeben
        };

        // Act: 1 Stunde lang laden im Auto-Modus
        // 50 kW * 1h = 50.000 Wh Energiezuwachs
        // 50.000 Wh / 100.000 Wh Batteriekapazität = +50 % SOC
        port.UpdateAutoMode(TimeSpan.FromHours(1));

        // Assert
        Assert.Equal(50000m, port.ActivePower);
        Assert.Equal(50000m, port.Energy);
        Assert.Equal(0.60m, port.SOC); // Von 10% auf 60% gestiegen
    }

    [Fact]
    public void CalculatePhysics_NeverExceeds100PercentSoc()
    {
        // Arrange: Port fast voll (90 % SOC), aber wir laden 50 kWh nach
        var port = new ChargingPoint
        {
            Id = 1,
            IsConnected = true,
            AssignedEV = EVModelCatalog.ModelS100D,
            SOC = 0.90m,
            TargetSOC = 1.00m,
            AvailablePower = 50000m
        };

        // Act: 1 Stunde mit 50 kW laden (wäre rechnerisch +50% -> 140%)
        port.UpdateAutoMode(TimeSpan.FromHours(1));

        // Assert: SOC darf niemals über 1.00 (100%) steigen
        Assert.Equal(1.00m, port.SOC);
    }

    [Fact]
    public void GetRequestedPower_ReturnsZero_WhenNotConnected()
    {
        // Arrange: Auto ist nicht eingesteckt
        var port = new ChargingPoint
        {
            Id = 1,
            IsConnected = false,
            AssignedEV = EVModelCatalog.ModelS100D,
            SOC = 0.10m
        };

        // Act & Assert: Nicht verbundenes Fahrzeug darf 0 W anfordern
        Assert.Equal(0m, port.GetRequestedPower());
    }

    [Fact]
    public void GetRequestedPower_ReturnsZero_WhenTargetSocReached()
    {
        // Arrange: Fahrzeug hat Ziel-SOC von 80% bereits erreicht
        var port = new ChargingPoint
        {
            Id = 1,
            IsConnected = true,
            AssignedEV = EVModelCatalog.ModelS100D,
            SOC = 0.80m,
            TargetSOC = 0.80m
        };

        // Act & Assert: Fertig geladenes Auto darf keine Leistung mehr anfordern
        Assert.Equal(0m, port.GetRequestedPower());
    }

    [Fact]
    public void Status_ReturnsLadevorgang_WhenChargingAndBelowTargetSoc()
    {
        // Arrange: Auto ist verbunden und noch unter TargetSOC
        var port = new ChargingPoint
        {
            Id = 1,
            IsConnected = true,
            SOC = 0.20m,
            TargetSOC = 0.80m,
            Fault = false
        };

        // Act & Assert
        Assert.Equal("Ladevorgang", port.Status);
    }

    [Fact]
    public void Status_ReturnsBeendet_WhenSocReachesTargetSoc()
    {
        // Arrange: Auto ist verbunden und hat Ziel-SOC erreicht
        var port = new ChargingPoint
        {
            Id = 1,
            IsConnected = true,
            SOC = 0.80m,
            TargetSOC = 0.80m,
            Fault = false
        };

        // Act & Assert
        Assert.Equal("Beendet", port.Status);
    }

    [Fact]
    public void TriggerFault_SetsFaultAndZeroesActivePower()
    {
        // Arrange: Ladepunkt ist mitten im Ladevorgang
        var port = new ChargingPoint
        {
            Id = 1,
            IsConnected = true,
            AssignedEV = EVModelCatalog.ModelS100D,
            SOC = 0.20m,
            ActivePower = 100000m
        };

        // Act: Störung auslösen
        port.TriggerFault();

        // Assert: Fehler gesetzt, Wirkleistung sofort 0 W, Status ist "Störung"
        Assert.True(port.Fault);
        Assert.Equal(0m, port.ActivePower);
        Assert.Equal("Störung", port.Status);
    }

    [Fact]
    public void ResetFault_ClearsFaultState()
    {
        // Arrange: Gestörter Ladepunkt
        var port = new ChargingPoint
        {
            Id = 1,
            Fault = true,
            ActivePower = 0m
        };

        // Act: Störung quittieren
        port.ResetFault();

        // Assert
        Assert.False(port.Fault);
        Assert.Equal("Frei", port.Status);
    }

    [Fact]
    public void ToggleConnection_ConnectsAndDisconnectsProperly()
    {
        // Arrange: Neuer Ladepunkt (nicht verbunden)
        var port = new ChargingPoint
        {
            Id = 1,
            IsConnected = false,
            AssignedEV = EVModelCatalog.ModelY2026
        };

        // Act 1: Auto anstecken (Toggle)
        port.ToggleConnection();

        // Assert 1: Angesteckt mit Initial-Werten
        Assert.True(port.IsConnected);
        Assert.Equal(0.05m, port.SOC);
        Assert.Equal(1.0m, port.TargetSOC);
        Assert.True(port.ActivePower > 0);

        // Act 2: Auto wieder abstecken (Toggle)
        port.ToggleConnection();

        // Assert 2: Zurückgesetzt auf frei / 0 W
        Assert.False(port.IsConnected);
        Assert.Equal(0m, port.ActivePower);
        Assert.Equal(0m, port.SOC);
    }
}
