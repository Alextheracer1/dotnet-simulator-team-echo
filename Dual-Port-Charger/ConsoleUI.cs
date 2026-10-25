using System;

namespace WS26_Dual_Port_Charger
{
    public class ConsoleUI
    {
        private readonly Device _device;
        private readonly SimulatorEngine _simulator;
        
        public ConsoleUI(Device device, SimulatorEngine simulator)
        {
            _device = device;
            _simulator = simulator;
        }

        public void Render()
        {
            Console.SetCursorPosition(0, 0);
        
            var mode = _simulator.AutoMode ? "AUTO" : "MANUELL";
            Console.WriteLine("=== E-Auto-Ladesäule Simulator ===".PadRight(80));
            Console.WriteLine($"Simulierte Zeit: {_simulator.SimulatedTime:yyyy-MM-dd HH:mm} | Modus: {mode}".PadRight(80));
            Console.WriteLine($"Sim-Tempo:       1 Sim-Minute = {_simulator.DelayMs} ms Echtzeit".PadRight(80));
            Console.WriteLine($"Gerätestatus:    {_device.Status}".PadRight(80));
            Console.WriteLine($"Netzanschluss:   {_device.Power / 1000,7:F1} kW Max-Limit".PadRight(80));
            Console.WriteLine($"Aktuelle Last:   {_device.ActivePower / 1000,7:F1} kW | Energie: {_device.Energy / 1000,7:F1} kWh".PadRight(80));
            Console.WriteLine(new string('-', 80));
            
            Console.WriteLine("Ladepunkte:");
            foreach (var cp in _device.ChargingPoints)
            {
                var activeKw = cp.ActivePower / 1000;
                var requestedKw = cp.GetRequestedPower() / 1000;
                
                var carInfo = cp.IsConnected ? cp.AssignedEV.Name : "Kein Fahrzeug";
                
                Console.WriteLine($"  LP {cp.Id}: {cp.Status,-12} | {carInfo}".PadRight(80));
                Console.WriteLine($"    Last: {activeKw,5:F1} kW (Fahrzeug fordert: {requestedKw,5:F1} kW)".PadRight(80));
            
                var barLength = 25;
                var filled = (int)(cp.SOC * barLength);
                var bar = new string('█', filled) + new string('░', barLength - filled);
                
                if (!cp.IsConnected) bar = new string('░', barLength);
                
                // Show both current SOC and Target SOC
                var socText = cp.IsConnected ? $"{cp.SOC:P0} (Ziel: {cp.TargetSOC:P0})" : "";
                Console.WriteLine($"    [ {bar} ] {socText}".PadRight(80));
                Console.WriteLine();
            }
        
            Console.WriteLine(new string('-', 80));
            Console.WriteLine("Tastenbelegung:".PadRight(80));
            Console.WriteLine("  [1] Auto an LP 1 verbinden/trennen [2] an LP 2 (nur Manuell)".PadRight(80));
            Console.WriteLine("  [+] Sim. beschleunigen             [-] Sim. verlangsamen".PadRight(80));
            Console.WriteLine("  [P] Pause / Fortsetzen".PadRight(80));
            Console.WriteLine("  [A] Auto-Modus                     [M] Manuell-Modus".PadRight(80));
            Console.WriteLine("  [F] Fehler auslösen                [R] Fehler zurücksetzen".PadRight(80));
            Console.WriteLine("  [↑] Leistung erhöhen               [↓] Leistung verringern".PadRight(80));
            Console.WriteLine("  [X] Beenden".PadRight(80));
            
            for(int i = 0; i < 3; i++) Console.WriteLine(new string(' ', 80));
        }
    }
}