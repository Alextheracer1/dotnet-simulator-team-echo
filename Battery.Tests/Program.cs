using System;

namespace Battery.Tests;

public class Program
{
    public static int Main()
    {
        Console.WriteLine("=================================================================");
        Console.WriteLine("          Battery Simulator xUnit Test Suite (Team Echo)         ");
        Console.WriteLine("=================================================================");

        var modelTests = new BatteryModelTests();
        var scenarioTests = new AutoScenarioTests();
        var historyTests = new SimulationHistoryTests();

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

        // --- Kategorie 1: Batteriemodell & Lade-/Entladephysik ---
        Console.WriteLine("\n[1] Batteriemodell, Grenzwerte & Ladekurven (BatteryModel):");
        RunTest("Constructor_SetsInitialProperties_Correctly",
            () => modelTests.Constructor_SetsInitialProperties_Correctly());
        RunTest("Constructor_Throws_WhenCapacityInvalid (0.0)",
            () => modelTests.Constructor_Throws_WhenCapacityInvalid(0.0));
        RunTest("Constructor_Throws_WhenCapacityInvalid (-5.0)",
            () => modelTests.Constructor_Throws_WhenCapacityInvalid(-5.0));
        RunTest("Constructor_Throws_WhenCapacityInvalid (10.5)",
            () => modelTests.Constructor_Throws_WhenCapacityInvalid(10.5));
        RunTest("Constructor_Throws_WhenMaxChargePowerInvalid (0.0)",
            () => modelTests.Constructor_Throws_WhenMaxChargePowerInvalid(0.0));
        RunTest("Constructor_Throws_WhenMaxChargePowerInvalid (3.5)",
            () => modelTests.Constructor_Throws_WhenMaxChargePowerInvalid(3.5));
        RunTest("Constructor_Throws_WhenMaxDischargePowerInvalid (0.0)",
            () => modelTests.Constructor_Throws_WhenMaxDischargePowerInvalid(0.0));
        RunTest("Constructor_Throws_WhenMaxDischargePowerInvalid (19.0)",
            () => modelTests.Constructor_Throws_WhenMaxDischargePowerInvalid(19.0));
        RunTest("Constructor_Throws_WhenInitialSocOutOfRange (-0.1)",
            () => modelTests.Constructor_Throws_WhenInitialSocOutOfRange(-0.1));
        RunTest("Constructor_Throws_WhenInitialSocOutOfRange (100.1)",
            () => modelTests.Constructor_Throws_WhenInitialSocOutOfRange(100.1));
        RunTest("Advance_Throws_WhenElapsedIsZeroOrNegative",
            () => modelTests.Advance_Throws_WhenElapsedIsZeroOrNegative());
        RunTest("Advance_Throws_WhenPowerIsNotFinite",
            () => modelTests.Advance_Throws_WhenPowerIsNotFinite());
        RunTest("Advance_ChargesBattery_IncreasesSocAndTracksEnergy",
            () => modelTests.Advance_ChargesBattery_IncreasesSocAndTracksEnergy());
        RunTest("Advance_ClampsChargePower_ToHardwareMaximum",
            () => modelTests.Advance_ClampsChargePower_ToHardwareMaximum());
        RunTest("Advance_DischargesBattery_DecreasesSocAndTracksEnergy",
            () => modelTests.Advance_DischargesBattery_DecreasesSocAndTracksEnergy());
        RunTest("Advance_ClampsDischargePower_ToHardwareMaximum",
            () => modelTests.Advance_ClampsDischargePower_ToHardwareMaximum());
        RunTest("Advance_TapersChargePower_WhenSocAbove80Percent",
            () => modelTests.Advance_TapersChargePower_WhenSocAbove80Percent());
        RunTest("Advance_TopOffThreshold_CompletesFullChargeAndTransitionsToIdle",
            () => modelTests.Advance_TopOffThreshold_CompletesFullChargeAndTransitionsToIdle());
        RunTest("Advance_PreventsDischarge_BelowZeroCapacity",
            () => modelTests.Advance_PreventsDischarge_BelowZeroCapacity());
        RunTest("Advance_ZeroPower_TransitionsToIdle",
            () => modelTests.Advance_ZeroPower_TransitionsToIdle());
        RunTest("FaultInjection_HaltsOperationAndBlocksAdvance",
            () => modelTests.FaultInjection_HaltsOperationAndBlocksAdvance());
        RunTest("InjectFault_Throws_WhenMessageEmptyOrWhitespace",
            () => modelTests.InjectFault_Throws_WhenMessageEmptyOrWhitespace("   "));

        // --- Kategorie 2: Automatisches Szenario & Lastprofile ---
        Console.WriteLine("\n[2] Automatik-Szenario & Lastkurven (AutoScenario):");
        RunTest("GetTargetPowerKW_ReturnsSolarCharging_DuringSolarHours",
            () => scenarioTests.GetTargetPowerKW_ReturnsSolarCharging_DuringSolarHours());
        RunTest("GetTargetPowerKW_ReturnsSocketCharging_DuringOffPeakHours",
            () => scenarioTests.GetTargetPowerKW_ReturnsSocketCharging_DuringOffPeakHours());
        RunTest("GetTargetPowerKW_InterpolatesRamp_DuringTransitionHours",
            () => scenarioTests.GetTargetPowerKW_InterpolatesRamp_DuringTransitionHours());
        RunTest("IsVehicleConnected_ReturnsTrue_OnlyDuringEveningAndNight",
            () => scenarioTests.IsVehicleConnected_ReturnsTrue_OnlyDuringEveningAndNight());
        RunTest("SelectDeviceProfiles_Throws_WhenRandomIsNull",
            () => scenarioTests.SelectDeviceProfiles_Throws_WhenRandomIsNull());
        RunTest("SelectDeviceProfiles_SelectsBetween3And5Devices_InHighDemandPeriods",
            () => scenarioTests.SelectDeviceProfiles_SelectsBetween3And5Devices_InHighDemandPeriods());
        RunTest("SelectDeviceProfiles_SelectsBetween0And2Devices_InLowDemandPeriods",
            () => scenarioTests.SelectDeviceProfiles_SelectsBetween0And2Devices_InLowDemandPeriods());

        // --- Kategorie 3: Messwert-Historie & Aggregation ---
        Console.WriteLine("\n[3] Simulation-Historie & gleitende Durchschnitte (SimulationHistory):");
        RunTest("RecordStateOfCharge_Throws_WhenPercentNotFinite",
            () => historyTests.RecordStateOfCharge_Throws_WhenPercentNotFinite());
        RunTest("RecordStateOfCharge_TracksHistoryAndEnforcesRollingLimit",
            () => historyTests.RecordStateOfCharge_TracksHistoryAndEnforcesRollingLimit());
        RunTest("GetRollingSummary_ReturnsNull_WhenEmpty",
            () => historyTests.GetRollingSummary_ReturnsNull_WhenEmpty());
        RunTest("GetRollingSummary_CalculatesAverageMinMax_Accurately",
            () => historyTests.GetRollingSummary_CalculatesAverageMinMax_Accurately());
        RunTest("GetDailySummaries_AggregatesReadingsByDay",
            () => historyTests.GetDailySummaries_AggregatesReadingsByDay());

        // --- Gesamtergebnis ---
        Console.WriteLine("\n=================================================================");
        if (failed == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  Ergebnis: Alle {passed}/{passed} Battery Tests erfolgreich bestanden! 🎉");
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
