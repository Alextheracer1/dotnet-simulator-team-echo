# Simulation Master Client Protocol

This guide describes the TCP interface that a simulator uses to synchronize its virtual clock with the Simulation Master.

## Connection and framing

- Connect to the machine running the master over TCP. The default port is `1510`; the master can override it with `SIMULATION_MASTER_PORT`.
- Keep the connection open while participating in the simulation. Reconnect if the connection closes.
- Messages are UTF-8 JSON, one JSON object per line, terminated by `\n` (the server writes `\r\n` on Windows). Read complete lines; do not assume one socket read equals one message.
- JSON property names use camel case, for example `timeStep` and `virtualTime`.
- `DateTime` values use ISO 8601 JSON representation. Send and retain UTC times where possible (for example, `2026-01-01T00:00:00Z`).
- `TimeSpan` values use the .NET constant (`c`) format in JSON strings, for example `"00:15:00"` for 15 minutes.

The connection is plain TCP without authentication or encryption. Restrict access to a trusted network.

## Startup and registration

The master sends an `Environment` message immediately after accepting a connection. A client may then request a fresh snapshot and register:

1. Read the initial `Environment` line.
2. Send `EnvironmentRequest` and read its `Environment` response. This request is optional if the initial snapshot is sufficient, but it is useful when the connection handshake takes time.
3. Send `Register` with a stable `stationId` and the simulator's last known `virtualTime`.
4. Read the `Environment` response to registration, then process subsequent master messages on the same connection.

Example client messages (each object is sent on a single line):

```json
{"type":"EnvironmentRequest"}
{"type":"Register","stationId":"pv-simulator-01","virtualTime":"2026-01-01T00:00:00Z","timeStep":"00:00:01","isRunning":false}
```

A station that has no saved virtual time can use the time from the environment snapshot. When recovering from a master restart, report the station's latest known time in `Register`. While the master is paused, it reconciles forward to a later station time; it never moves backward. Registration time is not used to reconcile the master while it is running.

Only registered connections participate in timestep ACK barriers.

## Message types

| Type | Direction | Purpose |
| --- | --- | --- |
| `EnvironmentRequest` | Client → Master | Request a current environment snapshot. |
| `Environment` | Master → Client | Snapshot of committed virtual time, current sequence, running state, and configured timestep. |
| `Register` | Client → Master | Register the station and provide its last known virtual time. |
| `TimeStep` | Master → Client | Ask the simulator to advance from `previousVirtualTime` to `virtualTime`. |
| `State` | Client → Master | Optional station state/telemetry report. The current master accepts this message but does not use it to update its clock. |
| `Acknowledgement` | Client → Master | Confirm that the station completed the specified `TimeStep`. |
| `Error` | Master → Client | Error response, for example when a JSON line cannot be parsed. |

The common message fields are:

| JSON field | Type | Meaning |
| --- | --- | --- |
| `type` | string | Message type listed above. |
| `stationId` | string | Stable simulator/device identifier; used on registration. |
| `sequence` | integer | Master sequence number. Echo it in the ACK for a `TimeStep`. |
| `virtualTime` | ISO 8601 date-time | Current time in an environment, target time in a timestep, or completed time in an ACK. |
| `previousVirtualTime` | ISO 8601 date-time | Start time for a timestep. |
| `timeStep` | .NET `TimeSpan` string | Absolute simulated duration for one tick, not a multiplier. |
| `isRunning` | boolean | Whether the master is running the simulation. |
| `isOn` | boolean | Optional device state. |
| `status` | string | Optional status text. |
| `activePowerKW` | number | Optional active power in kW. |
| `cumulativeEnergyKWh` | number | Optional cumulative energy in kWh. |
| `stateOfChargePercent` | number | Optional state of charge in percent. |
| `setpointKW` | number | Optional power setpoint in kW. |

Fields not relevant to a message can be omitted. Ignore fields a simulator does not use.

## Running and paused behavior

When the master is paused, it periodically broadcasts `Environment` snapshots. These are informational; do not advance the simulator and do not ACK them.

When running, the master broadcasts a `TimeStep` approximately once per real-time second. For example:

```json
{"type":"TimeStep","sequence":12,"previousVirtualTime":"2026-01-01T00:00:00Z","virtualTime":"2026-01-01T00:15:00Z","timeStep":"00:15:00","isRunning":true}
```

Treat `timeStep` as the absolute amount of simulated time for this tick. Advance from `previousVirtualTime` to `virtualTime`; normally, `virtualTime - previousVirtualTime` equals `timeStep`. Do not multiply by elapsed wall-clock time or a speed factor.

For energy calculations, convert the simulated interval to hours exactly once. For example, 40 kW for 15 simulated minutes produces `40 × 0.25 = 10 kWh`, subject to device limits and model behavior.

## Acknowledgement barrier

After applying a `TimeStep` and bringing the simulator's local virtual clock to the target, send an `Acknowledgement` with the same sequence and exact target timestamp:

```json
{"type":"Acknowledgement","sequence":12,"virtualTime":"2026-01-01T00:15:00Z"}
```

An optional `State` report may be sent before the ACK. A state report does not replace the ACK. The master accepts an ACK only when both `sequence` and `virtualTime` match the outstanding timestep.

The master advances its committed clock by one configured step only after all remaining participating clients have acknowledged that candidate time. It does not derive virtual-time progress from how long ACKs took to arrive. Clients that do not ACK within five seconds are disconnected; do not rely on a late ACK being accepted after disconnection.

While the master is paused, no ACK is expected. A client should wait for a `TimeStep` before advancing again.

## Recommended client loop

```text
connect and register
while connected:
	message = read one JSON line
	if message.type == Environment:
		update master status and configured timeStep
	else if message.type == TimeStep:
		delta = message.virtualTime - message.previousVirtualTime
		advance device models by delta
		set local virtual time to message.virtualTime
		optionally send State
		send Acknowledgement using message.sequence and message.virtualTime
	else if message.type == Error:
		log the error
```

Keep one reader responsible for incoming lines and serialize outgoing writes so that concurrent state reports and ACKs cannot interleave JSON lines.

## Master configuration and controls

- `SIMULATION_MASTER_PORT` selects the listening TCP port; default: `1510`.
- `SIMULATION_MASTER_TIME_STEP` selects the initial simulated duration per tick; examples: `15m`, `1h`, or `00:15:00`. Default: `1s`.
- The console ladder is `1s`, `10s`, `1m`, `15m`, `1h`, `2h`, `12h`, `24h`, `7d`. The `+` and `-` keys select the next or previous duration.
