using System;
using System.Collections.Generic;
using EasyModbus;
using Spectre.Console;
using TeamConsoleShared;
using TeamConsoleShared.Diagnostics;
using TeamShared;

namespace ModbusDiagnoseClient;

class Program
{
    static void Main(string[] args)
    {
        SimulationTheme.InitializeConsole();

        AnsiConsole.Write(
            new Panel(new Markup("[bold darkorange]🔍 Modbus-Diagnose-Client (LB02)[/]\n[grey62]Interaktives Diagnose- und Testwerkzeug für Feldgeräte-Register[/]"))
                .Border(BoxBorder.Rounded)
                .BorderStyle(new Style(foreground: SimulationTheme.DiagnoseAccent))
                .Expand());

        AnsiConsole.Markup("[bold grey85]Ziel-Host [grey][[127.0.0.1]][/]: [/]");
        string? host = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(host)) host = "127.0.0.1";

        AnsiConsole.Markup("[bold grey85]Ziel-Port [grey][[1502]][/]: [/]");
        var portInput = Console.ReadLine();
        if (!int.TryParse(portInput, out int port)) port = 1502;

        AnsiConsole.Markup("[bold grey85]Unit-ID [grey][[1]][/]: [/]");
        var unitInput = Console.ReadLine();
        if (!byte.TryParse(unitInput, out byte unitId)) unitId = 1;

        ModbusClient modbusClient = new ModbusClient(host, port)
        {
            UnitIdentifier = unitId
        };

        try
        {
            AnsiConsole.MarkupLine($"\n[grey62]Verbinde mit[/] [bold cyan]{host}:{port}[/] (Unit: [bold cyan]{unitId}[/])...");
            modbusClient.Connect();
            AnsiConsole.MarkupLine("[bold green3]✓ Erfolgreich mit Modbus-Server verbunden![/]\n");

            bool exit = false;
            while (!exit)
            {
                var menuTable = new Table()
                    .Border(TableBorder.Rounded)
                    .BorderStyle(new Style(foreground: SimulationTheme.DiagnoseAccent))
                    .Expand();

                menuTable.Title("[bold darkorange]Verfügbare Diagnose-Aktionen[/]");
                menuTable.AddColumn(new TableColumn("[bold grey85]Taste[/]").Centered());
                menuTable.AddColumn(new TableColumn("[bold grey85]Funktion[/]"));

                menuTable.AddRow("[bold cyan]1[/]", "Standard-Registerkopf lesen (Reg 01-11: Betriebszustand, Leistung, Zeit, etc.)");
                menuTable.AddRow("[bold cyan]2[/]", "Gerätespezifische Register lesen (Reg 13-15: z.B. PV/Batterie/Ladesäule)");
                menuTable.AddRow("[bold cyan]3[/]", "Leistungssollwert schreiben (Reg 10-11: Float32 Setpoint in kW)");
                menuTable.AddRow("[bold cyan]4[/]", "Gerätefehler zurücksetzen (Reg 12: Reset Error = 1)");
                menuTable.AddRow("[bold red]5[/]", "Verbindung trennen & Beenden");

                AnsiConsole.Write(menuTable);
                AnsiConsole.Markup("[bold darkorange]Auswahl [1-5]: [/]");

                string? choice = Console.ReadLine()?.Trim();
                Console.WriteLine();

                try
                {
                    switch (choice)
                    {
                        case "1":
                            ReadCommonRegisters(modbusClient);
                            break;
                        case "2":
                            ReadDeviceSpecificRegisters(modbusClient);
                            break;
                        case "3":
                            WriteSetpoint(modbusClient);
                            break;
                        case "4":
                            ResetError(modbusClient);
                            break;
                        case "5":
                            exit = true;
                            break;
                        default:
                            AnsiConsole.MarkupLine("[bold yellow]Ungültige Auswahl. Bitte 1-5 wählen.[/]");
                            break;
                    }
                }
                catch (EasyModbus.Exceptions.ModbusException mEx)
                {
                    AnsiConsole.MarkupLine($"[bold red]MODBUS PROTOCOL ERROR:[/] Das Gerät hat die Anfrage abgewiesen: {Markup.Escape(mEx.Message)}");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[bold red]KOMMUNIKATIONSFEHLER:[/] Verbindung zum Simulator verloren: {Markup.Escape(ex.Message)}");
                }
                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[bold red]VERBINDUNGSFEHLER:[/] Konnte nicht verbinden: {Markup.Escape(ex.Message)}");
        }
        finally
        {
            if (modbusClient.Connected)
            {
                modbusClient.Disconnect();
                AnsiConsole.MarkupLine("[grey62]Verbindung sauber getrennt.[/]");
            }
        }
    }

    private static void ReadCommonRegisters(ModbusClient client)
    {
        AnsiConsole.MarkupLine("[bold cyan]>>> Lese gemeinsamen Registerkopf (01-11)...[/]");
        int startAddress = CommonRegisterMap.OperatingState - 1;
        int[] registers = client.ReadHoldingRegisters(startAddress, 11);

        short opState = (short)registers[0];
        short errCode = (short)registers[1];
        float power = ModbusClient.ConvertRegistersToFloat(new[] { registers[2], registers[3] });
        float energy = ModbusClient.ConvertRegistersToFloat(new[] { registers[4], registers[5] });

        int timeLow = registers[6];
        int timeHigh = registers[7];
        int virtualTime = timeLow | (timeHigh << 16);

        short onOff = (short)registers[8];
        float setpoint = ModbusClient.ConvertRegistersToFloat(new[] { registers[9], registers[10] });

        var entries = new List<RegisterTable.RegisterEntry>
        {
            new(1, "Operating State", "Int16", registers[0].ToString(), opState switch { 0 => "Off (0)", 1 => "Booting (1)", 2 => "Running (2)", 3 => "Error (3)", _ => opState.ToString() }),
            new(2, "Error Code", "Int16", registers[1].ToString(), errCode == 0 ? "Kein Fehler (0)" : $"Fehler-Code {errCode}"),
            new(3, "Active Power (High)", "Float32 Word 1", registers[2].ToString(), $"{power:F2} kW"),
            new(4, "Active Power (Low)", "Float32 Word 2", registers[3].ToString(), "-"),
            new(5, "Cumulative Energy (High)", "Float32 Word 1", registers[4].ToString(), $"{energy:F2} kWh"),
            new(6, "Cumulative Energy (Low)", "Float32 Word 2", registers[5].ToString(), "-"),
            new(7, "Virtual Time (Low)", "Int32 Word 1", registers[6].ToString(), $"{virtualTime} (Unix)"),
            new(8, "Virtual Time (High)", "Int32 Word 2", registers[7].ToString(), "-"),
            new(9, "On/Off State", "Int16", registers[8].ToString(), onOff == 1 ? "Ein (1)" : "Aus (0)"),
            new(10, "Setpoint (High)", "Float32 Word 1", registers[9].ToString(), $"{setpoint:F2} kW"),
            new(11, "Setpoint (Low)", "Float32 Word 2", registers[10].ToString(), "-")
        };

        AnsiConsole.Write(RegisterTable.Create("Modbus Registerkopf (LB02 Standard 01-11)", entries));
    }

    private static void ReadDeviceSpecificRegisters(ModbusClient client)
    {
        AnsiConsole.MarkupLine("[bold cyan]>>> Lese gerätespezifische Register (13-15)...[/]");
        int startAddress = CommonRegisterMap.DeviceSpecificStart - 1;
        int[] registers = client.ReadHoldingRegisters(startAddress, 3);

        var entries = new List<RegisterTable.RegisterEntry>
        {
            new(13, "Temperatur / Datum 1", "Int16", registers[0].ToString(), $"{registers[0]} °C"),
            new(14, "Einstrahlung / Datum 2", "Int16", registers[1].ToString(), $"{registers[1]} W/m²"),
            new(15, "Netzfrequenz / Datum 3", "Int16", registers[2].ToString(), $"{registers[2] / 10.0:F1} Hz")
        };

        AnsiConsole.Write(RegisterTable.Create("Gerätespezifische Register (13-15)", entries));
    }

    private static void WriteSetpoint(ModbusClient client)
    {
        AnsiConsole.Markup("[bold darkorange]Neuer Leistungssollwert in kW [grey][[z.B. 25,5]][/]: [/]");
        var input = Console.ReadLine();
        if (float.TryParse(input, out float setpoint))
        {
            if (setpoint < 0 || setpoint > 1000)
            {
                AnsiConsole.MarkupLine("[bold red]Wertebereichsfehler: Sollwert muss zwischen 0 und 1000 kW liegen.[/]");
                return;
            }

            int[] regs = ModbusClient.ConvertFloatToRegisters(setpoint);
            int startAddress = CommonRegisterMap.Setpoint_High - 1;

            AnsiConsole.MarkupLine($"[grey62]Schreibe Float '{setpoint:F2}' als Register [[{regs[0]}, {regs[1]}]] an Adresse {startAddress}...[/]");
            client.WriteMultipleRegisters(startAddress, regs);
            AnsiConsole.MarkupLine("[bold green3]✓ Sollwert erfolgreich an Server übermittelt.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[bold red]Ungültiges Zahlenformat.[/]");
        }
    }

    private static void ResetError(ModbusClient client)
    {
        AnsiConsole.MarkupLine("[bold cyan]>>> Sende Fehler-Reset-Befehl (Register 12 = 1)...[/]");
        int startAddress = CommonRegisterMap.ResetError - 1;
        client.WriteSingleRegister(startAddress, 1);
        AnsiConsole.MarkupLine("[bold green3]✓ Fehler-Reset-Befehl erfolgreich gesendet.[/]");
    }
}
