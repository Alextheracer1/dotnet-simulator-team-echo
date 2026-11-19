using System;
using System.Threading.Tasks;

namespace TeamRabbitMQShared.Tests;

class Program
{
    static async Task<int> Main()
    {
        Console.WriteLine("=== Running TeamRabbitMQShared & Weather Integration Tests ===");
        var tests = new TelemetryAndMappingTests();
        var weatherTests = new WeatherIntegrationTests();

        try
        {
            Console.Write("[TEST] DeviceTelemetryMessage_SerializationAndDeserialization... ");
            tests.DeviceTelemetryMessage_SerializationAndDeserialization_MaintainsDataIntegrity();
            Console.WriteLine("PASSED ✅");

            Console.Write("[TEST] DeviceCommandMessage_FactoryMethods... ");
            tests.DeviceCommandMessage_FactoryMethods_ProduceExpectedProperties();
            Console.WriteLine("PASSED ✅");

            Console.Write("[TEST] RabbitMqConfiguration_RoutingKeyBuilders... ");
            tests.RabbitMqConfiguration_RoutingKeyBuilders_FormatTopicsCorrectly();
            Console.WriteLine("PASSED ✅");

            Console.Write("[TEST] ModbusTelemetryMapper_FromRegisters... ");
            tests.ModbusTelemetryMapper_FromRegisters_MapsStandardAndDeviceSpecificRegisters();
            Console.WriteLine("PASSED ✅");

            Console.Write("[TEST] ModbusTelemetryMapper_FromRegisters_MapsPvSpecificRegisters... ");
            tests.ModbusTelemetryMapper_FromRegisters_MapsPvSpecificRegisters();
            Console.WriteLine("PASSED ✅");

            Console.Write("[TEST] ModbusTelemetryMapper_FromRegisters_MapsWindSpecificRegisters... ");
            tests.ModbusTelemetryMapper_FromRegisters_MapsWindSpecificRegisters();
            Console.WriteLine("PASSED ✅");

            Console.Write("[TEST] WeatherSnapshotDto_SerializationAndDeserialization... ");
            weatherTests.WeatherSnapshotDto_SerializationAndDeserialization_MaintainsData();
            Console.WriteLine("PASSED ✅");

            Console.Write("[TEST] DeviceWeatherRegistration_SerializationAndDeserialization... ");
            weatherTests.DeviceWeatherRegistration_SerializationAndDeserialization_MaintainsData();
            Console.WriteLine("PASSED ✅");

            Console.Write("[TEST] WeatherCallbackReceiver_ReceivesAndMatchesSnapshotAsync... ");
            await weatherTests.WeatherCallbackReceiver_ReceivesAndMatchesSnapshotAsync();
            Console.WriteLine("PASSED ✅");

            Console.WriteLine("\nAll 9/9 Unit & Integration Tests Passed Successfully! 🎉");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAILED ❌\n{ex}");
            return 1;
        }
    }
}
