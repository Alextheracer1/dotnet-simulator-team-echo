# Battery Energy Storage Simulator

A .NET 10 console application that simulates a battery energy storage system, connected device loads, and a repeating daily automatic schedule. It displays battery and energy-flow metrics and writes simulation samples to CSV.

## Prerequisites

- .NET 10
- A terminal that supports interactive keyboard input for live controls

## Build and run

From the repository root:

```powershell
dotnet build Battery.csproj
dotnet run --project Battery.csproj
```

The program starts in **Manual** mode with no devices connected. Press **A** to start the automatic schedule, or use the manual controls below. Press **Ctrl+C** to cancel the simulation.

When connected, the Simulation Master controls virtual time. If no master time step has ever been received, the local fallback advances virtual time by 30 minutes per real-time interval (one second by default). After a master connection, fallback continues with the last time step received from the master.

## Controls

| Key or command | Action |
| --- | --- |
| `A` | Enter Auto mode. The active automatic device profile replaces the current list. |
| `M` | Enter Manual mode with a zero power target. Current devices remain connected. |
| `D` / `discharge` | Pause and configure a device count (0–5), followed by each device's power demand in kW. Entries that exceed 20 kW cumulative demand are rejected. |
| `X` / `unplug all` | Disconnect all devices. |
| `C` / `charge [kW]` | Set a positive manual charging target. `C` is the `charge 2.5` shortcut. |
| `I` / `idle` | Set the manual target to zero and clear connected devices. |
| `P` | Pause or resume. During discharge setup, cancels and discards that setup; during device selection, cancels without disconnecting devices. |
| `+` / `-` | Increase or decrease the manual power target by 1 kW. |
| `F` / `R` | Inject a battery fault / reset the fault. |
| `Q` | Quit the simulation. |

During device setup, device names are assigned automatically (`Device 1`, etc.) and socket numbers are assigned from 1 to 4. For disconnection, enter a device name, a socket number (for example, `2` or `socket 2`), multiple comma-separated selections, or `all`.

## Automatic daily scenario

Auto mode follows virtual time. It targets 2.3 kW of socket charging whenever solar is unavailable and switches to a 2.0 kW solar target from 11:00–15:00. The target ramps linearly over 60 minutes at the solar window boundaries. Device demand is subtracted from the active target; when demand exceeds it, the Battery discharges to serve the remaining load. Actual power remains subject to SOC, battery ratings, and fault state.

| Simulated time | Charging target | Auto device behavior |
| --- | ---: | --- |
| 00:00–06:00 | +2.3 kW socket | Randomly select 0–2 profiles per virtual hour; the vehicle remains connected until 01:00. |
| 06:00–09:00 | +2.3 kW socket | Randomly select 3–5 profiles per virtual hour. |
| 09:00–11:00 | +2.3 kW socket | Randomly select 0–2 profiles per virtual hour. |
| 11:00–15:00 | +2.0 kW solar | Randomly select 0–2 profiles per virtual hour; target ramps at both boundaries. |
| 15:00–17:00 | +2.3 kW socket | Randomly select 0–2 profiles per virtual hour. |
| 17:00–20:00 | +2.3 kW socket | Randomly select 3–5 profiles per virtual hour. The 2.3 kW vehicle load connects first at 18:00. |
| 20:00–24:00 | +2.3 kW socket | Randomly select 0–2 profiles per virtual hour; the vehicle remains connected until 01:00. |

Auto mode selects from the existing device profiles at each virtual-hour boundary. At 18:00 it remembers connected devices, reconnects the 2.3 kW vehicle load first, then reconnects devices in order. New connections are rejected when they would exceed five devices or 20 kW cumulative demand. These same limits apply to manual device setup. An Auto-mode error has a 1% chance at each virtual-hour boundary and automatically resets after 30 virtual minutes.

## Model and displayed metrics

- Battery capacity and power are expressed in **kWh** and **kW**. Positive battery power charges the battery; negative power discharges it.
- Hardware limits are 10 kWh capacity, 3 kW maximum charge power, and 18 kW maximum discharge power. Lower configured limits are allowed; higher ones are rejected.
- Each simulation step applies the requested power for its simulated duration. State of charge is bounded between 0% and 100%; charge power tapers above 80% SOC.
- **Inflow** is the positive target only. **Outflow** is total connected-device demand as a positive value. **Total** is `Inflow − Outflow`.
- The dashboard's **Power** value is actual signed battery power. It may differ from the target or Total when battery limits, SOC, or faults constrain operation.
- `SimulationHistory` keeps timestamped SOC data in memory and a rolling window of the latest 96 readings. The CSV logger separately persists the full battery and meter sample data.

## Default and example parameters

`Program.cs` currently uses these settings:

| Parameter | Default example |
| --- | ---: |
| Capacity | 10 kWh |
| Maximum charge power | 3 kW |
| Maximum discharge power | 18 kW |
| Initial state of charge | 0% |
| Local fallback time step (before any master step is received) | 30 minutes |
| Local fallback real-time interval | 1 second |
| Start time | Today at 00:00 |
| Duration | 23:59:59 |
| Maximum connected devices | 5 |
| Maximum cumulative device demand | 20 kW |
| CSV output | `data/battery-run.csv` |

To try different settings, edit the `SimulationOptions` initializer in `Program.cs`. Capacity and power settings may be reduced but cannot exceed the hardware limits. `RealTimeStepInterval` controls wall-clock pacing for local fallback only; virtual time steps are supplied by the Simulation Master when available.

## Output

The simulator overwrites `data/battery-run.csv` when it starts, creating the output directory and writing a fresh header. Rows are written for the initial state and each simulation step; buffered rows are flushed every three virtual hours and when the run ends. Timestamps include seconds, and previous runs are not preserved.
