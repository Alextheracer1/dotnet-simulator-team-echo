using System;
using System.Threading.Tasks;

namespace SimulationsMaster.Tests;

public class Program
{
    public static async Task<int> Main()
    {
        Console.WriteLine("=================================================================");
        Console.WriteLine("       Simulations-Master xUnit Test Suite (Team Echo)           ");
        Console.WriteLine("=================================================================");

        var engineTests = new SimulationEngineTests();
        var serverTests = new TcpSimulationServerTests();

        int passed = 0;
        int failed = 0;

        void RunSyncTest(string testName, Action testAction)
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

        async Task RunAsyncTest(string testName, Func<Task> testAction)
        {
            Console.Write($"[TEST] {testName,-62} ... ");
            try
            {
                await testAction();
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

        // --- Kategorie 1: SimulationEngine (Virtuelle Zeit & Taktsteuerung) ---
        Console.WriteLine("\n[1] Virtuelle Zeit- & Taktsteuerung (SimulationEngine):");
        RunSyncTest("Constructor_InitializesWithDefaultOrCustomTime",
            () => engineTests.Constructor_InitializesWithDefaultOrCustomTime());
        RunSyncTest("Start_Pause_Resume_ManagesRunningState_Correctly",
            () => engineTests.Start_Pause_Resume_ManagesRunningState_Correctly());
        RunSyncTest("SetVirtualTime_DirectlyUpdatesCurrentVirtualTime",
            () => engineTests.SetVirtualTime_DirectlyUpdatesCurrentVirtualTime());
        RunSyncTest("ChangeTimeStep_UpdatesTimeStep_AndValidatesPositiveDuration",
            () => engineTests.ChangeTimeStep_UpdatesTimeStep_AndValidatesPositiveDuration());
        RunSyncTest("Restart_ThrowsNotSupportedException",
            () => engineTests.Restart_ThrowsNotSupportedException());
        RunSyncTest("AvailableTimeSteps_ReturnsNinePredefinedSteps",
            () => engineTests.AvailableTimeSteps_ReturnsNinePredefinedSteps());
        RunSyncTest("GetNearestTimeStep_SelectsClosestValidInterval (4s -> 1s)",
            () => engineTests.GetNearestTimeStep_SelectsClosestValidInterval(4, 1));
        RunSyncTest("GetNearestTimeStep_SelectsClosestValidInterval (8s -> 10s)",
            () => engineTests.GetNearestTimeStep_SelectsClosestValidInterval(8, 10));
        RunSyncTest("GetNearestTimeStep_SelectsClosestValidInterval (45s -> 60s)",
            () => engineTests.GetNearestTimeStep_SelectsClosestValidInterval(45, 60));
        RunSyncTest("GetNearestTimeStep_SelectsClosestValidInterval (1200s -> 900s)",
            () => engineTests.GetNearestTimeStep_SelectsClosestValidInterval(1200, 900));
        RunSyncTest("GetNearestTimeStep_SelectsClosestValidInterval (10800s -> 7200s)",
            () => engineTests.GetNearestTimeStep_SelectsClosestValidInterval(10800, 7200));
        RunSyncTest("CommitTimeStep_AdvancesVirtualTime_OnlyWhenExpectedTimeMatches",
            () => engineTests.CommitTimeStep_AdvancesVirtualTime_OnlyWhenExpectedTimeMatches());
        RunSyncTest("ReconcileFromStation_OnlyAdvancesClockForward",
            () => engineTests.ReconcileFromStation_OnlyAdvancesClockForward());
        RunSyncTest("GetStatusSnapshot_ReturnsConsistentSnapshot",
            () => engineTests.GetStatusSnapshot_ReturnsConsistentSnapshot());

        // --- Kategorie 2: TcpSimulationServer (Netzwerkprotokoll & Zeitschritt-Barriere) ---
        Console.WriteLine("\n[2] TCP Server, Protokoll & Zeitschritt-Barriere (TcpSimulationServer):");
        await RunAsyncTest("Server_BroadcastsEnvironment_UponClientConnection",
            () => serverTests.Server_BroadcastsEnvironment_UponClientConnection());
        await RunAsyncTest("Server_RespondsToEnvironmentRequest",
            () => serverTests.Server_RespondsToEnvironmentRequest());
        await RunAsyncTest("Server_HandlesClientRegistration_Correctly",
            () => serverTests.Server_HandlesClientRegistration_Correctly());
        await RunAsyncTest("Server_ReturnsError_WhenMessageMalformed",
            () => serverTests.Server_ReturnsError_WhenMessageMalformed());
        await RunAsyncTest("Server_AdvancesEngineVirtualTime_WhenClientAcknowledgesTimeStep",
            () => serverTests.Server_AdvancesEngineVirtualTime_WhenClientAcknowledgesTimeStep());

        // --- Gesamtergebnis ---
        Console.WriteLine("\n=================================================================");
        if (failed == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  Ergebnis: Alle {passed}/{passed} Simulation Master Tests erfolgreich bestanden! 🎉");
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
