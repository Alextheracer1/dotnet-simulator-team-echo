using System;
using EasyModbus;
using TeamShared; // Make sure to reference your shared class library project

namespace ModbusDiagnoseClient
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=====================================");
            Console.WriteLine("=== LB02 Modbus-Diagnose-Client   ===");
            Console.WriteLine("=====================================");
            
            // 1. Selectable Host, Port, and Target Device [cite: 4]
            Console.Write("Enter Target Host (default 127.0.0.1): ");
            string host = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(host)) host = "127.0.0.1";

            Console.Write("Enter Target Port (default 1502): ");
            if (!int.TryParse(Console.ReadLine(), out int port)) port = 1502;

            Console.Write("Enter Unit-ID / Target Device (default 1): ");
            if (!byte.TryParse(Console.ReadLine(), out byte unitId)) unitId = 1;

            ModbusClient modbusClient = new ModbusClient(host, port);
            modbusClient.UnitIdentifier = unitId;

            try
            {
                Console.WriteLine($" [INFO] Connecting to {host}:{port} (Unit: {unitId})...");
                
                // Synchronous connect is permitted here as it's an external library boundary [cite: 5]
                modbusClient.Connect(); 
                Console.WriteLine("[INFO] Connected successfully!");

                bool exit = false;
                while (!exit)
                {
                    Console.WriteLine("-------------------------------------");
                    Console.WriteLine("1. Read Common Registers (01-11)");
                    Console.WriteLine("2. Read Device-Specific Registers (13-15, e.g., PV Plant)");
                    Console.WriteLine("3. Write Power Limit Setpoint (10-11)");
                    Console.WriteLine("4. Reset Device Error (12)");
                    Console.WriteLine("5. Exit");
                    Console.WriteLine("-------------------------------------");
                    Console.Write("Select an option: ");
                    
                    string choice = Console.ReadLine();
                    Console.WriteLine();

                    try
                    {
                        switch (choice)
                        {
                            case "1": ReadCommonRegisters(modbusClient); break;
                            case "2": ReadDeviceSpecificRegisters(modbusClient); break;
                            case "3": WriteSetpoint(modbusClient); break;
                            case "4": ResetError(modbusClient); break;
                            case "5": exit = true; break;
                            default: Console.WriteLine("[WARNING] Invalid option selected."); break;
                        }
                    }
                    catch (EasyModbus.Exceptions.ModbusException mEx)
                    {
                        // Protocol errors shown understandably [cite: 4]
                        Console.WriteLine($"[MODBUS PROTOCOL ERROR] The device rejected the request: {mEx.Message}");
                    }
                    catch (Exception ex)
                    {
                        // Connection errors shown understandably [cite: 4]
                        Console.WriteLine($"[COMMUNICATION ERROR] Lost connection to simulator: {ex.Message}");
                    }
                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($" [CONNECTION ERROR] Failed to connect: {ex.Message}");
            }
            finally
            {
                if (modbusClient.Connected)
                {
                    modbusClient.Disconnect();
                    Console.WriteLine("[INFO] Disconnected cleanly.");
                }
            }
        }

        private static void ReadCommonRegisters(ModbusClient client)
        {
            Console.WriteLine(">>> Reading Common Register Head...");
            
            // In EasyModbus, requesting Register 1 means requesting protocol address 0.
            int startAddress = CommonRegisterMap.OperatingState - 1; 
            
            // Read 11 registers (Operating State through Setpoint_Low)
            int[] registers = client.ReadHoldingRegisters(startAddress, 11);

            // Raw values must be traceable [cite: 4]
            Console.WriteLine($"[RAW DATA]: [{string.Join(", ", registers)}]");

            short opState = (short)registers[0]; // Reg 1
            short errCode = (short)registers[1]; // Reg 2
            
            // 2 Registers -> Float
            float power = ModbusClient.ConvertRegistersToFloat(new int[] { registers[2], registers[3] }); 
            float energy = ModbusClient.ConvertRegistersToFloat(new int[] { registers[4], registers[5] }); 
            
            // 2 Registers -> Int32
            int timeLow = registers[6];
            int timeHigh = registers[7];
            int virtualTime = timeLow | (timeHigh << 16); 
            
            short onOff = (short)registers[8]; // Reg 9
            float setpoint = ModbusClient.ConvertRegistersToFloat(new int[] { registers[9], registers[10] }); // Reg 10-11

            // Interpreted values must be traceable [cite: 4]
            Console.WriteLine($"[DECODED VALUES]:");
            Console.WriteLine($" - Operating State (1):    {opState} ({(opState == 0 ? "Off" : opState == 1 ? "Booting" : opState == 2 ? "Running" : "Error")})");
            Console.WriteLine($" - Error Code (2):         {errCode}");
            Console.WriteLine($" - Active Power (3-4):     {power:F2} kW");
            Console.WriteLine($" - Cumul. Energy (5-6):    {energy:F2} kWh");
            Console.WriteLine($" - Virtual Time (7-8):     {virtualTime}");
            Console.WriteLine($" - On/Off State (9):       {onOff} ({(onOff == 1 ? "On" : "Off")})");
            Console.WriteLine($" - Current Setpoint (10-11): {setpoint:F2} kW");
        }

        private static void ReadDeviceSpecificRegisters(ModbusClient client)
        {
            Console.WriteLine(">>> Reading Device-Specific Registers (PV Plant Example)...");
            
            int startAddress = CommonRegisterMap.DeviceSpecificStart - 1; 
            int[] registers = client.ReadHoldingRegisters(startAddress, 3);
            
            Console.WriteLine($"[RAW DATA]: [{string.Join(", ", registers)}]");
            
            Console.WriteLine($"[DECODED VALUES]:");
            Console.WriteLine($" - Temperature (13): {registers[0]} °C");
            Console.WriteLine($" - Irradiation (14): {registers[1]} W/m²");
            Console.WriteLine($" - Grid Freq (15):   {registers[2] / 10.0:F1} Hz");
        }

        private static void WriteSetpoint(ModbusClient client)
        {
            Console.Write("Enter new Power Limit Setpoint (kW) [e.g. 25,5]: ");
            if (float.TryParse(Console.ReadLine(), out float setpoint))
            {
                // Value range errors must be shown understandably [cite: 4]
                if (setpoint < 0 || setpoint > 1000) 
                {
                    Console.WriteLine("[ERROR] Value range error: Setpoint must be between 0 kW and 1000 kW.");
                    return;
                }

                int[] regs = ModbusClient.ConvertFloatToRegisters(setpoint);
                int startAddress = CommonRegisterMap.Setpoint_High - 1; 
                
                Console.WriteLine($" [INFO] Writing float '{setpoint}' as raw registers [{regs[0]}, {regs[1]}] to addresses {startAddress} and {startAddress + 1}...");
                client.WriteMultipleRegisters(startAddress, regs);
                Console.WriteLine("[SUCCESS] Write command accepted by server.");
            }
            else
            {
                Console.WriteLine("[ERROR] Invalid format. Please enter a valid number.");
            }
        }

        private static void ResetError(ModbusClient client)
        {
            Console.WriteLine(">>> Sending Error Reset command...");
            int startAddress = CommonRegisterMap.ResetError - 1; 
            
            // Write 1 to clear errors
            client.WriteSingleRegister(startAddress, 1);
            Console.WriteLine("[SUCCESS] Error reset command sent.");
        }
    }
}
