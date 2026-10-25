using System;
using System.Collections.Generic;

namespace WS26_Dual_Port_Charger
{
    public class EVModel
    {
        public string Name { get; set; }
        public decimal BatteryCapacityWh { get; set; }
        
        // Function returning the maximum charge limit in Watts based on the current SOC (0.0 to 1.0)
        public Func<decimal, decimal> GetChargeLimit { get; set; }

        public static decimal Interpolate(decimal x, decimal x0, decimal x1, decimal y0, decimal y1)
        {
            if (x >= x1) return y1;
            if (x <= x0) return y0;
            return y0 + (x - x0) * (y1 - y0) / (x1 - x0);
        }
    }

    public static class EVModelCatalog
    {
        public static EVModel ModelS100D => new EVModel
        {
            Name = "Model S 100D (2018)",
            BatteryCapacityWh = 100000m,
            GetChargeLimit = (soc) =>
            {
                if (soc < 0.20m) return 142000m;
                if (soc < 0.50m) return EVModel.Interpolate(soc, 0.20m, 0.50m, 142000m, 90000m);
                if (soc < 0.80m) return EVModel.Interpolate(soc, 0.50m, 0.80m, 90000m, 40000m);
                return EVModel.Interpolate(soc, 0.80m, 1.00m, 40000m, 10000m);
            }
        };

        public static EVModel ModelY2026 => new EVModel
        {
            Name = "Model Y LR (2026)",
            BatteryCapacityWh = 78000m,
            GetChargeLimit = (soc) =>
            {
                if (soc < 0.30m) return 250000m;
                if (soc < 0.60m) return EVModel.Interpolate(soc, 0.30m, 0.60m, 250000m, 110000m);
                if (soc < 0.80m) return EVModel.Interpolate(soc, 0.60m, 0.80m, 110000m, 60000m);
                return EVModel.Interpolate(soc, 0.80m, 1.00m, 60000m, 20000m);
            }
        };

        public static EVModel PorscheTaycan => new EVModel
        {
            Name = "Porsche Taycan (800V)",
            BatteryCapacityWh = 93400m,
            GetChargeLimit = (soc) =>
            {
                // Extremely flat and high curve due to 800V architecture
                if (soc < 0.45m) return 270000m;
                if (soc < 0.80m) return EVModel.Interpolate(soc, 0.45m, 0.80m, 270000m, 100000m);
                return EVModel.Interpolate(soc, 0.80m, 1.00m, 100000m, 30000m);
            }
        };

        public static EVModel VWID4 => new EVModel
        {
            Name = "VW ID.4 Pro",
            BatteryCapacityWh = 77000m,
            GetChargeLimit = (soc) =>
            {
                // Conservative curve
                if (soc < 0.30m) return 135000m;
                if (soc < 0.70m) return EVModel.Interpolate(soc, 0.30m, 0.70m, 135000m, 65000m);
                return EVModel.Interpolate(soc, 0.70m, 1.00m, 65000m, 25000m);
            }
        };

        private static readonly List<EVModel> AllModels = new List<EVModel>
        {
            ModelS100D, ModelY2026, PorscheTaycan, VWID4
        };

        public static EVModel GetRandomModel(Random random)
        {
            return AllModels[random.Next(AllModels.Count)];
        }
    }
}