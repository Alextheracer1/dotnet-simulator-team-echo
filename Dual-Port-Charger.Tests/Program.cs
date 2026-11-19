using System;

namespace Dual_Port_Charger.Tests;

class Program
{
    static int Main()
    {
        Console.WriteLine("=================================================================");
        Console.WriteLine("       Dual-Port-Charger xUnit Test Suite (Team Echo)            ");
        Console.WriteLine("=================================================================");

        var evTests = new EVModelTests();
        var cpTests = new ChargingPointTests();
        var deviceTests = new DeviceLoadBalancingTests();

        int passed = 0;
        int failed = 0;

        void RunTest(string testName, Action testAction)
        {
            Console.Write($"[TEST] {testName,-62} ... ");
            try
            {
                testAction();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("PASSED ✅");
                Console.ResetColor();
                passed++;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("FAILED ❌");
                Console.ResetColor();
                Console.WriteLine($"  Fehler: {ex.Message}");
                failed++;
            }
        }

        // --- Kategorie 1: Fahrzeug-Ladekurven & Interpolation ---
        Console.WriteLine("\n[1] Fahrzeugmodelle & Ladekurven (EVModel / EVModelCatalog):");
        RunTest("Interpolate_CalculatesExactMidpoint_WhenAt50Percent", 
            () => evTests.Interpolate_CalculatesExactMidpoint_WhenAt50Percent());
        RunTest("Interpolate_ClampsToBoundaries_WhenOutsideRange", 
            () => evTests.Interpolate_ClampsToBoundaries_WhenOutsideRange());
        RunTest("ModelS100D_ReturnsFullChargeLimit_WhenBatteryUnder20Percent", 
            () => evTests.ModelS100D_ReturnsFullChargeLimit_WhenBatteryUnder20Percent());
        RunTest("ModelY2026_InterpolatesCorrectly_BetweenChargeLimitPoints", 
            () => evTests.ModelY2026_InterpolatesCorrectly_BetweenChargeLimitPoints());
        RunTest("PorscheTaycan_MaintainsHighPower_DueTo800VArchitecture", 
            () => evTests.PorscheTaycan_MaintainsHighPower_DueTo800VArchitecture());
        RunTest("VWID4_CalculatesChargeLimit_AccuratelyAcrossSocCurve", 
            () => evTests.VWID4_CalculatesChargeLimit_AccuratelyAcrossSocCurve());
        RunTest("EVModelCatalog_GetRandomModel_ReturnsNonNullModel", 
            () => evTests.EVModelCatalog_GetRandomModel_ReturnsNonNullModel());

        // --- Kategorie 2: Ladephysik, SOC & Verbindungen ---
        Console.WriteLine("\n[2] Ladephysik & Port-Logik (ChargingPoint):");
        RunTest("CalculatePhysics_AddsAccurateEnergyAndIncreasesSoc", 
            () => cpTests.CalculatePhysics_AddsAccurateEnergyAndIncreasesSoc());
        RunTest("CalculatePhysics_NeverExceeds100PercentSoc", 
            () => cpTests.CalculatePhysics_NeverExceeds100PercentSoc());
        RunTest("GetRequestedPower_ReturnsZero_WhenNotConnected", 
            () => cpTests.GetRequestedPower_ReturnsZero_WhenNotConnected());
        RunTest("GetRequestedPower_ReturnsZero_WhenTargetSocReached", 
            () => cpTests.GetRequestedPower_ReturnsZero_WhenTargetSocReached());
        RunTest("Status_ReturnsLadevorgang_WhenChargingAndBelowTargetSoc", 
            () => cpTests.Status_ReturnsLadevorgang_WhenChargingAndBelowTargetSoc());
        RunTest("Status_ReturnsBeendet_WhenSocReachesTargetSoc", 
            () => cpTests.Status_ReturnsBeendet_WhenSocReachesTargetSoc());
        RunTest("TriggerFault_SetsFaultAndZeroesActivePower", 
            () => cpTests.TriggerFault_SetsFaultAndZeroesActivePower());
        RunTest("ResetFault_ClearsFaultState", 
            () => cpTests.ResetFault_ClearsFaultState());
        RunTest("ToggleConnection_ConnectsAndDisconnectsProperly", 
            () => cpTests.ToggleConnection_ConnectsAndDisconnectsProperly());

        // --- Kategorie 3: Dynamisches Lastmanagement (Load Balancing) & Station ---
        Console.WriteLine("\n[3] Dynamisches Lastmanagement & Station (Device):");
        RunTest("LoadBalancing_GivesSurplusToHungrierCar_WhenOneCarNeedsLess", 
            () => deviceTests.LoadBalancing_GivesSurplusToHungrierCar_WhenOneCarNeedsLess());
        RunTest("LoadBalancing_Splits5050_WhenBothCarsRequestMoreThanHalf", 
            () => deviceTests.LoadBalancing_Splits5050_WhenBothCarsRequestMoreThanHalf());
        RunTest("SetPowerLimit_RestrictsAvailablePowerForBothPorts", 
            () => deviceTests.SetPowerLimit_RestrictsAvailablePowerForBothPorts());
        RunTest("SetPowerLimit_ClampsNegativeValuesToZero", 
            () => deviceTests.SetPowerLimit_ClampsNegativeValuesToZero());
        RunTest("AdjustManualPower_IncreasesAndClampsPowerWithinValidBounds", 
            () => deviceTests.AdjustManualPower_IncreasesAndClampsPowerWithinValidBounds());
        RunTest("AdjustManualPower_DoesNothing_WhenFaultIsActive", 
            () => deviceTests.AdjustManualPower_DoesNothing_WhenFaultIsActive());
        RunTest("EnforceManualModeCars_ResetsBothPortsToTeslasWithFullTargetSoc", 
            () => deviceTests.EnforceManualModeCars_ResetsBothPortsToTeslasWithFullTargetSoc());
        RunTest("ToggleCar_TogglesOnlySelectedPort", 
            () => deviceTests.ToggleCar_TogglesOnlySelectedPort());
        RunTest("TriggerFault_PropagatesToAllChargingPoints", 
            () => deviceTests.TriggerFault_PropagatesToAllChargingPoints());
        RunTest("ResetFault_RestoresNormalOperationOnAllChargingPoints", 
            () => deviceTests.ResetFault_RestoresNormalOperationOnAllChargingPoints());
        RunTest("ActivePowerAndEnergy_SumAcrossAllPorts", 
            () => deviceTests.ActivePowerAndEnergy_SumAcrossAllPorts());

        // --- Gesamtergebnis ---
        Console.WriteLine("\n=================================================================");
        if (failed == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  Ergebnis: Alle {passed}/{passed} Tests erfolgreich bestanden! 🎉");
            Console.ResetColor();
            Console.WriteLine("=================================================================\n");
            return 0;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  Ergebnis: {failed} Test(s) fehlgeschlagen! ({passed} bestanden)");
            Console.ResetColor();
            Console.WriteLine("=================================================================\n");
            return 1;
        }
    }
}
