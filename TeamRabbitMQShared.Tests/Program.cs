using System;

namespace TeamRabbitMQShared.Tests;

class Program
{
    static int Main()
    {
        Console.WriteLine("=== Running TeamRabbitMQShared Unit Tests ===");
        var tests = new TelemetryAndMappingTests();

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

            Console.WriteLine("\nAll 4/4 Unit Tests Passed Successfully! 🎉");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAILED ❌\n{ex}");
            return 1;
        }
    }
}
