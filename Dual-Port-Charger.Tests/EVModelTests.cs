using System;
using Dual_Port_Charger;
using Xunit;

namespace Dual_Port_Charger.Tests;

public class EVModelTests
{
    [Fact]
    public void Interpolate_CalculatesExactMidpoint_WhenAt50Percent()
    {
        // Arrange
        decimal x = 0.5m;
        decimal x0 = 0.0m;
        decimal x1 = 1.0m;
        decimal y0 = 100m;
        decimal y1 = 200m;

        // Act
        decimal result = EVModel.Interpolate(x, x0, x1, y0, y1);

        // Assert
        Assert.Equal(150m, result);
    }

    [Fact]
    public void Interpolate_ClampsToBoundaries_WhenOutsideRange()
    {
        // Wenn x kleiner als x0 ist, muss y0 zurückgegeben werden (Begrenzung nach unten)
        decimal lower = EVModel.Interpolate(-0.1m, 0.0m, 1.0m, 50m, 100m);
        Assert.Equal(50m, lower);

        // Wenn x größer als x1 ist, muss y1 zurückgegeben werden (Begrenzung nach oben)
        decimal upper = EVModel.Interpolate(1.5m, 0.0m, 1.0m, 50m, 100m);
        Assert.Equal(100m, upper);
    }

    [Fact]
    public void ModelS100D_ReturnsFullChargeLimit_WhenBatteryUnder20Percent()
    {
        // Arrange
        var model = EVModelCatalog.ModelS100D;
        decimal lowSoc = 0.15m; // 15 % SOC

        // Act
        decimal chargeLimit = model.GetChargeLimit(lowSoc);

        // Assert: Bis 20% lädt das Model S mit maximal 142 kW
        Assert.Equal(142000m, chargeLimit);
    }

    [Fact]
    public void ModelY2026_InterpolatesCorrectly_BetweenChargeLimitPoints()
    {
        // Arrange
        var model = EVModelCatalog.ModelY2026;
        // Bei SOC 45% (genau in der Mitte zwischen 30% und 60%):
        // Stützpunkte: 30% -> 250.000 W, 60% -> 110.000 W
        // Erwarteter Mittelwert: 250.000 - ((250.000 - 110.000) / 2) = 180.000 W
        decimal soc = 0.45m;

        // Act
        decimal chargeLimit = model.GetChargeLimit(soc);

        // Assert
        Assert.Equal(180000m, chargeLimit);
    }

    [Fact]
    public void PorscheTaycan_MaintainsHighPower_DueTo800VArchitecture()
    {
        // Arrange
        var taycan = EVModelCatalog.PorscheTaycan;

        // Act & Assert
        // Bis 45% SOC volle 270 kW Ladeleistung
        Assert.Equal(270000m, taycan.GetChargeLimit(0.10m));
        Assert.Equal(270000m, taycan.GetChargeLimit(0.40m));

        // Bei 90% SOC (Mitte zwischen 80% [100 kW] und 100% [30 kW]) -> 65 kW
        Assert.Equal(65000m, taycan.GetChargeLimit(0.90m));
    }

    [Fact]
    public void VWID4_CalculatesChargeLimit_AccuratelyAcrossSocCurve()
    {
        // Arrange
        var id4 = EVModelCatalog.VWID4;

        // Act & Assert:
        // 1. Unter 30 % SOC: Flache 135 kW
        Assert.Equal(135000m, id4.GetChargeLimit(0.15m));

        // 2. Bei 50 % SOC (Mitte zwischen 30% [135 kW] und 70% [65 kW]):
        // Mittelwert = 100 kW
        Assert.Equal(100000m, id4.GetChargeLimit(0.50m));

        // 3. Bei 85 % SOC (Mitte zwischen 70% [65 kW] und 100% [25 kW]):
        // Mittelwert = 45 kW
        Assert.Equal(45000m, id4.GetChargeLimit(0.85m));
    }

    [Fact]
    public void EVModelCatalog_GetRandomModel_ReturnsNonNullModel()
    {
        // Arrange
        var random = new Random(42);

        // Act
        var model = EVModelCatalog.GetRandomModel(random);

        // Assert
        Assert.NotNull(model);
        Assert.False(string.IsNullOrWhiteSpace(model.Name));
        Assert.True(model.BatteryCapacityWh > 0);
    }
}
