using System;
using System.Collections.Generic;
using System.Linq;

namespace WS26_Dual_Port_Charger
{
    public class Device
    {
        private const decimal TotalStationPower = 300000m; 
        private readonly Random _random = new Random();

        private readonly List<ChargingPoint> _chargingPoints = new List<ChargingPoint>
        {
            new ChargingPoint { Id = 1, MaxPower = TotalStationPower, AssignedEV = EVModelCatalog.ModelS100D, IsConnected = true, SOC = 0.10m, TargetSOC = 0.80m },
            new ChargingPoint { Id = 2, MaxPower = TotalStationPower, AssignedEV = EVModelCatalog.ModelY2026, IsConnected = true, SOC = 0.15m, TargetSOC = 0.80m }
        };

        public List<ChargingPoint> ChargingPoints => _chargingPoints;
        public bool Fault { get; private set; }
        public string Status => Fault ? "Störung" : "Funktioniert";
        public decimal ActivePower => _chargingPoints.Sum(cp => cp.ActivePower);
        public decimal Energy => _chargingPoints.Sum(cp => cp.Energy);
        public decimal Power => TotalStationPower;

        public void UpdateAutoMode(TimeSpan timeStep)
        {
            if (Fault) return;
            
            // Randomly spawn and depart cars
            foreach (var cp in _chargingPoints)
            {
                if (!cp.IsConnected)
                {
                    // 10% chance to spawn a new car per simulated minute
                    if (_random.NextDouble() < 0.10)
                    {
                        cp.AssignedEV = EVModelCatalog.GetRandomModel(_random);
                        // Random Target SOC between 50% and 100%, rounded to nearest 5% for realism
                        cp.TargetSOC = Math.Round((decimal)(0.50 + _random.NextDouble() * 0.50) * 20m) / 20m; 
                        cp.SOC = (decimal)(0.05 + _random.NextDouble() * 0.15); // Arrives with 5% to 20%
                        cp.IsConnected = true;
                    }
                }
                else if (cp.SOC >= cp.TargetSOC)
                {
                    // 5% chance to depart per simulated minute once finished charging
                    if (_random.NextDouble() < 0.10)
                    {
                        cp.Disconnect();
                    }
                }
            }
            
            ApplyDynamicLoadBalancing();
            foreach (var cp in _chargingPoints) cp.UpdateAutoMode(timeStep);
        }

        public void UpdateManualMode(TimeSpan timeStep)
        {
            if (Fault) return;
            ApplyDynamicLoadBalancing();
            foreach (var cp in _chargingPoints) cp.UpdateManualMode(timeStep);
        }
        
        public void EnforceManualModeCars()
        {
            // Reset to the required Teslas for manual mode
            _chargingPoints[0].AssignedEV = EVModelCatalog.ModelS100D;
            _chargingPoints[0].TargetSOC = 1.0m;
            
            _chargingPoints[1].AssignedEV = EVModelCatalog.ModelY2026;
            _chargingPoints[1].TargetSOC = 1.0m;
        }

        private void ApplyDynamicLoadBalancing()
        {
            var activePorts = _chargingPoints.Where(cp => cp.IsConnected && !cp.Fault && cp.SOC < cp.TargetSOC)
                                             .OrderBy(cp => cp.GetRequestedPower())
                                             .ToList();
            
            var availableStationPower = TotalStationPower;
            
            foreach (var cp in _chargingPoints) cp.AvailablePower = 0;

            for (int i = 0; i < activePorts.Count; i++)
            {
                var port = activePorts[i];
                var fairShare = availableStationPower / (activePorts.Count - i);
                var requested = port.GetRequestedPower();
                
                var granted = Math.Min(requested, fairShare);
                port.AvailablePower = granted;
                availableStationPower -= granted;
            }
        }

        public void AdjustManualPower(decimal amount)
        {
            if (Fault) return;
            foreach (var cp in _chargingPoints)
            {
                if (cp.IsConnected)
                {
                    var maxAllowed = Math.Min(cp.AvailablePower, cp.GetRequestedPower());
                    cp.ActivePower = Math.Clamp(cp.ActivePower + amount, 0, maxAllowed);
                }
            }
        }

        public void ToggleCar(int portId)
        {
            var cp = _chargingPoints.FirstOrDefault(p => p.Id == portId);
            cp?.ToggleConnection();
        }

        public void TriggerFault()
        {
            Fault = true;
            foreach (var cp in _chargingPoints) cp.TriggerFault();
        }

        public void ResetFault()
        {
            Fault = false;
            foreach (var cp in _chargingPoints) cp.ResetFault();
        }
    }

    public class ChargingPoint
    {
        public int Id { get; set; }
        public EVModel AssignedEV { get; set; }
        public decimal MaxPower { get; set; }
        public decimal AvailablePower { get; set; } 
        public decimal SOC { get; set; }
        public decimal TargetSOC { get; set; } = 1.0m;
        public decimal ActivePower { get; set; }
        public decimal Energy { get; set; }
        public bool Fault { get; set; }
        public bool IsConnected { get; set; } = false;
        
        public string Status => Fault ? "Störung" : (!IsConnected ? "Frei" : (SOC >= TargetSOC ? "Beendet" : "Ladevorgang"));

        public decimal GetRequestedPower()
        {
            if (!IsConnected || SOC >= TargetSOC) return 0;
            return AssignedEV.GetChargeLimit(SOC);
        }

        public void Disconnect()
        {
            IsConnected = false;
            ActivePower = 0;
            AvailablePower = 0;
            SOC = 0;
        }

        public void ToggleConnection()
        {
            if (IsConnected)
            {
                Disconnect();
            }
            else
            {
                IsConnected = true;
                SOC = 0.05m; 
                TargetSOC = 1.0m; // Manual connections always default to 100% target
                ActivePower = GetRequestedPower(); // Start charging instantly when manually connected
            }
        }

        public void UpdateAutoMode(TimeSpan timeStep)
        {
            if (Fault || !IsConnected || SOC >= TargetSOC) 
            {
                ActivePower = 0;
                return;
            }
            
            ActivePower = Math.Min(AvailablePower, GetRequestedPower());
            CalculatePhysics(timeStep);
        }

        public void UpdateManualMode(TimeSpan timeStep)
        {
            if (Fault || !IsConnected || SOC >= TargetSOC)
            {
                ActivePower = 0;
                return;
            }
            
            var maxAllowed = Math.Min(AvailablePower, GetRequestedPower());
            if (ActivePower > maxAllowed) ActivePower = maxAllowed;
            
            CalculatePhysics(timeStep);
        }

        private void CalculatePhysics(TimeSpan timeStep)
        {
            var energyAdded = ActivePower * (decimal)timeStep.TotalHours;
            Energy += energyAdded;
            SOC = Math.Min(1m, SOC + (energyAdded / AssignedEV.BatteryCapacityWh));
        }

        public void TriggerFault()
        {
            Fault = true;
            ActivePower = 0;
        }

        public void ResetFault()
        {
            Fault = false;
            ActivePower = 0;
        }
    }
}