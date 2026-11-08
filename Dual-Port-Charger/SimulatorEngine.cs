using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TeamShared;
using TeamShared.Simulation;
using EasyModbus;
using TeamRabbitMQShared.Configuration;
using TeamRabbitMQShared.Consumer;
using TeamRabbitMQShared.Models;
using TeamRabbitMQShared.Publisher;

namespace Dual_Port_Charger
{
    public class SimulatorEngine
    {
        private readonly Device _device;
        private readonly TimeSpan _timeStep;
        private readonly string _logFilePath;
        private readonly SimulationMasterClient? _simulationMasterClient;
        private bool _isRunning = true;
        private bool _locallyPaused;
        private bool _hasAdvanced;
        
        private readonly FieldDeviceModbusServer _modbusServer;
        private readonly IRabbitMqPublisher _rabbitPublisher;
        private readonly IRabbitMqConsumer _rabbitConsumer;

        public bool AutoMode { get; private set; } = true;
        public DateTime SimulatedTime { get; private set; }
        public int DelayMs { get; private set; } = 1000;

        public bool RabbitMqConnected => _rabbitPublisher.IsConnected;
        public long RabbitMqPublishedCount => _rabbitPublisher.PublishedCount;

        public SimulatorEngine(
            Device device,
            TimeSpan timeStep,
            string logFilePath,
            SimulationMasterClient? simulationMasterClient = null)
        {
            _device = device;
            _timeStep = timeStep;
            _logFilePath = logFilePath;
            _simulationMasterClient = simulationMasterClient;
            _modbusServer = new FieldDeviceModbusServer(1502, 1);
            
            var rabbitConfig = RabbitMqConfiguration.CreateDefault();
            _rabbitPublisher = new RabbitMqPublisher(rabbitConfig);
            _rabbitConsumer = new RabbitMqConsumer(rabbitConfig);

            SimulatedTime = new DateTime(2026, 7, 15, 0, 0, 0); 
    
            var logsDirectory = Path.GetDirectoryName(logFilePath);
            if (!string.IsNullOrEmpty(logsDirectory) && !Directory.Exists(logsDirectory))
            {
                Directory.CreateDirectory(logsDirectory);
            }
    
            if (!File.Exists(_logFilePath))
            {
                File.WriteAllText(_logFilePath, "Zeitstempel,StationMaxLeistung,Wirkleistung,Energie,SOC1,SOC2\n");
            }
        }

        public async Task StartAsync(Action renderUi, CancellationToken cancellationToken = default)
        {
            Console.Clear();
            var simulationStartTime = SimulatedTime;
            var endTime = SimulatedTime.AddHours(24);

            _modbusServer.Start();
            await _rabbitPublisher.StartAsync();
            await _rabbitConsumer.StartAsync();

            // Subscribe to RabbitMQ remote commands
            await _rabbitConsumer.SubscribeCommandsAsync("sim.commands.charger.#", async cmd =>
            {
                await HandleRemoteCommandAsync(cmd);
            });
            await _rabbitConsumer.SubscribeCommandsAsync("sim.commands.*.charger-1", async cmd =>
            {
                await HandleRemoteCommandAsync(cmd);
            });

            try 
            {
                Task? connectionTask = _simulationMasterClient?.ConnectWithRetryAsync(cancellationToken);
                Task<SimulationMessage?>? masterMessageTask = null;
                var masterConnected = false;
                var fallbackAccumulator = TimeSpan.Zero;
                var lastClockTimestamp = Stopwatch.GetTimestamp();
                using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
                var timerTask = timer.WaitForNextTickAsync(cancellationToken).AsTask();

                while (_isRunning && SimulatedTime < endTime && !cancellationToken.IsCancellationRequested)
                {
                    var waitTasks = new List<Task> { timerTask };
                    if (masterMessageTask is not null)
                        waitTasks.Add(masterMessageTask);
                    if (connectionTask is not null)
                        waitTasks.Add(connectionTask);

                    var completed = await Task.WhenAny(waitTasks).ConfigureAwait(false);

                    if (connectionTask is not null && completed == connectionTask)
                    {
                        await connectionTask.ConfigureAwait(false);
                        connectionTask = null;
                        masterConnected = true;
                        _simulationMasterClient!.UpdateStationTime(SimulatedTime);
                        masterMessageTask = _simulationMasterClient.WaitForNextMessageAsync(cancellationToken);
                        continue;
                    }

                    if (masterMessageTask is not null && completed == masterMessageTask)
                    {
                        var message = await masterMessageTask.ConfigureAwait(false);
                        masterMessageTask = null;
                        if (message is null)
                        {
                            masterConnected = false;
                            fallbackAccumulator = TimeSpan.Zero;
                            lastClockTimestamp = Stopwatch.GetTimestamp();
                            if (connectionTask is null && _simulationMasterClient is not null)
                                connectionTask = _simulationMasterClient.ConnectWithRetryAsync(cancellationToken);
                        }
                        else
                        {
                            if (message.Type == SimulationMessageTypes.Environment && !_hasAdvanced && message.VirtualTime != default)
                            {
                                SimulatedTime = message.VirtualTime;
                                simulationStartTime = SimulatedTime;
                                endTime = simulationStartTime.AddHours(24);
                                _simulationMasterClient!.UpdateStationTime(SimulatedTime);
                            }
                            else if (message.Type == SimulationMessageTypes.TimeStep)
                            {
                                var targetTime = DateTime.SpecifyKind(message.VirtualTime, DateTimeKind.Utc);
                                var timeStep = targetTime - SimulatedTime;
                                if (timeStep > TimeSpan.Zero)
                                {
                                    SimulatedTime = targetTime;
                                    await AdvanceAndPublishAsync(timeStep, renderUi).ConfigureAwait(false);
                                    _hasAdvanced = true;
                                }

                                _simulationMasterClient!.UpdateStationTime(targetTime);
                                try
                                {
                                    await _simulationMasterClient.SendStateAsync(new SimulationMessage
                                    {
                                        VirtualTime = targetTime,
                                        Sequence = message.Sequence,
                                        IsOn = !_device.Fault,
                                        Status = _device.Status,
                                        ActivePowerKW = (double)(_device.ActivePower / 1000m),
                                        CumulativeEnergyKWh = (double)(_device.Energy / 1000m)
                                    }, cancellationToken).ConfigureAwait(false);
                                    await _simulationMasterClient.SendAcknowledgementAsync(
                                        message.Sequence,
                                        targetTime,
                                        cancellationToken).ConfigureAwait(false);
                                }
                                catch (IOException)
                                {
                                    masterConnected = false;
                                    fallbackAccumulator = TimeSpan.Zero;
                                    lastClockTimestamp = Stopwatch.GetTimestamp();
                                    if (connectionTask is null)
                                        connectionTask = _simulationMasterClient.ConnectWithRetryAsync(cancellationToken);
                                }
                            }

                            if (_simulationMasterClient!.IsConnected)
                                masterMessageTask = _simulationMasterClient.WaitForNextMessageAsync(cancellationToken);
                            else if (masterConnected)
                            {
                                masterConnected = false;
                                fallbackAccumulator = TimeSpan.Zero;
                                lastClockTimestamp = Stopwatch.GetTimestamp();
                                if (connectionTask is null)
                                    connectionTask = _simulationMasterClient.ConnectWithRetryAsync(cancellationToken);
                            }
                        }
                    }

                    if (completed != timerTask)
                        continue;

                    if (!await timerTask.ConfigureAwait(false))
                        break;

                    var now = Stopwatch.GetTimestamp();
                    var wallElapsed = Stopwatch.GetElapsedTime(lastClockTimestamp, now);
                    lastClockTimestamp = now;
                    while (!Console.IsInputRedirected && Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true);
                        await HandleUserInputAsync(key.Key, masterConnected).ConfigureAwait(false);
                    }

                    if (_modbusServer.CheckAndClearErrorReset())
                        _device.ResetFault();

                    var setpoint = _modbusServer.ReadSetpoint();
                    if (setpoint > 0)
                        _device.SetPowerLimit((decimal)setpoint);

                    if (!masterConnected && !_locallyPaused)
                    {
                        fallbackAccumulator += wallElapsed;
                        while (fallbackAccumulator.TotalMilliseconds >= DelayMs && SimulatedTime < endTime)
                        {
                            var stepDuration = _timeStep <= endTime - SimulatedTime
                                ? _timeStep
                                : endTime - SimulatedTime;
                            SimulatedTime += stepDuration;
                            await AdvanceAndPublishAsync(stepDuration, renderUi).ConfigureAwait(false);
                            _simulationMasterClient?.UpdateStationTime(SimulatedTime);
                            _hasAdvanced = true;
                            fallbackAccumulator -= TimeSpan.FromMilliseconds(DelayMs);
                        }
                    }

                    timerTask = timer.WaitForNextTickAsync(cancellationToken).AsTask();
                }
            }
            finally 
            {
                _modbusServer.Stop();
                await _rabbitPublisher.StopAsync();
                await _rabbitConsumer.StopAsync();
            }
            
            Console.SetCursorPosition(0, 24);
            Console.WriteLine("Simulation beendet (24h erreicht oder manuell abgebrochen).");
        }

        private async Task AdvanceAndPublishAsync(TimeSpan timeStep, Action renderUi)
        {
            UpdateSimulation(timeStep);
            UpdateModbusRegisters();
            await PublishRabbitMqTelemetryAsync();
            LogData();
            renderUi();
        }

        private async Task PublishRabbitMqTelemetryAsync()
        {
            var telemetry = new DeviceTelemetryMessage
            {
                DeviceId = "charger-1",
                DeviceType = "charger",
                Timestamp = DateTime.UtcNow,
                VirtualTime = ((DateTimeOffset)SimulatedTime).ToUnixTimeSeconds(),
                OperatingState = (short)(_device.Fault ? 3 : 2),
                ErrorCode = (short)(_device.Fault ? 1 : 0),
                ActivePowerWatts = (double)_device.ActivePower,
                CumulativeEnergyKwh = (double)(_device.Energy / 1000m),
                IsOn = true,
                ModbusPort = 1502,
                ModbusUnitId = 1
            };

            telemetry.SetMetric("SOC1", (double)_device.ChargingPoints[0].SOC);
            telemetry.SetMetric("SOC2", (double)_device.ChargingPoints[1].SOC);
            telemetry.SetMetric("TargetSOC1", (double)_device.ChargingPoints[0].TargetSOC);
            telemetry.SetMetric("TargetSOC2", (double)_device.ChargingPoints[1].TargetSOC);
            telemetry.SetMetric("ActivePower1", (double)_device.ChargingPoints[0].ActivePower);
            telemetry.SetMetric("ActivePower2", (double)_device.ChargingPoints[1].ActivePower);

            await _rabbitPublisher.PublishTelemetryAsync(telemetry);
        }

        private async Task HandleRemoteCommandAsync(DeviceCommandMessage cmd)
        {
            if (cmd.CommandType.Equals("SetPowerLimit", StringComparison.OrdinalIgnoreCase) ||
                cmd.CommandType.Equals("Setpoint", StringComparison.OrdinalIgnoreCase))
            {
                if (cmd.Value > 0)
                {
                    _device.SetPowerLimit((decimal)cmd.Value);
                }
            }
            else if (cmd.CommandType.Equals("ResetFault", StringComparison.OrdinalIgnoreCase))
            {
                _device.ResetFault();
            }
            else if (cmd.CommandType.Equals("TriggerFault", StringComparison.OrdinalIgnoreCase))
            {
                _device.TriggerFault();
            }
            else if (cmd.CommandType.Equals("ToggleCar", StringComparison.OrdinalIgnoreCase))
            {
                _device.ToggleCar((int)cmd.Value);
            }
            else if (cmd.CommandType.Equals("SetMode", StringComparison.OrdinalIgnoreCase))
            {
                AutoMode = cmd.Value > 0;
            }

            await Task.CompletedTask;
        }

        private void UpdateModbusRegisters()
        {
            short opState = (short)(_device.Fault ? 3 : 2);
            short errCode = (short)(_device.Fault ? 1 : 0);
            int virtualTime = (int)((DateTimeOffset)SimulatedTime).ToUnixTimeSeconds();

            _modbusServer.UpdateCommonRegisters(
                opState, 
                errCode, 
                (float)_device.ActivePower, 
                (float)_device.Energy, 
                virtualTime, 
                1
            );

            // Gerätespezifische Register: SOC beider Ladepunkte
            var regs = _modbusServer.HoldingRegisters;
            int[] soc1 = ModbusClient.ConvertFloatToRegisters((float)_device.ChargingPoints[0].SOC);
            int[] soc2 = ModbusClient.ConvertFloatToRegisters((float)_device.ChargingPoints[1].SOC);

            regs[CommonRegisterMap.DeviceSpecificStart] = (short)soc1[0];
            regs[CommonRegisterMap.DeviceSpecificStart + 1] = (short)soc1[1];
            regs[CommonRegisterMap.DeviceSpecificStart + 2] = (short)soc2[0];
            regs[CommonRegisterMap.DeviceSpecificStart + 3] = (short)soc2[1];
        }

        private void UpdateSimulation(TimeSpan timeStep)
        {
            if (AutoMode) _device.UpdateAutoMode(timeStep);
            else _device.UpdateManualMode(timeStep);
        }

        private void LogData()
        {
            var data = new List<string>
            {
                SimulatedTime.ToString("yyyy-MM-dd HH:mm:ss"),
                _device.Power.ToString("F1"),
                _device.ActivePower.ToString("F1"),
                _device.Energy.ToString("F1"),
                _device.ChargingPoints[0].SOC.ToString("F2"),
                _device.ChargingPoints[1].SOC.ToString("F2")
            };
            File.AppendAllLines(_logFilePath, new[] { string.Join(",", data) });
        }

        private Task HandleUserInputAsync(ConsoleKey key, bool masterConnected)
        {
            switch (key)
            {
                case ConsoleKey.P:
                    if (masterConnected)
                        Console.WriteLine("Simulation master controls the clock; use the master to pause or resume.");
                    else
                        _locallyPaused = !_locallyPaused;
                    break;
                case ConsoleKey.A:
                    AutoMode = true;
                    break;
                case ConsoleKey.M: 
                    AutoMode = false;
                    _device.EnforceManualModeCars();
                    break;
                case ConsoleKey.F:
                    _device.TriggerFault();
                    break;
                case ConsoleKey.R:
                    _device.ResetFault();
                    break;
                case ConsoleKey.UpArrow:
                    if (!AutoMode) _device.AdjustManualPower(5000); 
                    break;
                case ConsoleKey.DownArrow:
                    if (!AutoMode) _device.AdjustManualPower(-5000);
                    break;
                case ConsoleKey.D1:
                case ConsoleKey.NumPad1:
                    if (!AutoMode) _device.ToggleCar(1);
                    break;
                case ConsoleKey.D2:
                case ConsoleKey.NumPad2:
                    if (!AutoMode) _device.ToggleCar(2);
                    break;
                case ConsoleKey.Add:
                case ConsoleKey.OemPlus:
                    DelayMs = Math.Max(10, DelayMs - 20); 
                    break;
                case ConsoleKey.Subtract:
                case ConsoleKey.OemMinus:
                    DelayMs = Math.Min(1000, DelayMs + 20); 
                    break;
                case ConsoleKey.X: 
                    _isRunning = false;
                    break;
            }

            return Task.CompletedTask;
        }
    }
}
