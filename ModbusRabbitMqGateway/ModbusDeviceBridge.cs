using System;
using EasyModbus;
using TeamRabbitMQShared.Helpers;
using TeamRabbitMQShared.Models;

namespace ModbusRabbitMqGateway;

public class ModbusDeviceBridge : IDisposable
{
    private readonly ModbusDeviceEndpoint _endpoint;
    private ModbusClient? _client;
    private readonly object _lock = new();

    public ModbusDeviceEndpoint Endpoint => _endpoint;
    public bool IsConnected { get; private set; }
    public long PollSuccessCount { get; private set; }
    public long PollFailureCount { get; private set; }
    public DeviceTelemetryMessage? LastTelemetry { get; private set; }
    public string LastError { get; private set; } = string.Empty;

    public ModbusDeviceBridge(ModbusDeviceEndpoint endpoint)
    {
        _endpoint = endpoint;
    }

    public DeviceTelemetryMessage? PollTelemetry()
    {
        lock (_lock)
        {
            try
            {
                EnsureConnected();
                if (_client is null || !_client.Connected)
                {
                    IsConnected = false;
                    PollFailureCount++;
                    return null;
                }

                // In EasyModbus, ReadHoldingRegisters(startingAddress, quantity)
                // Register 1 corresponds to offset 1
                var registers = _client.ReadHoldingRegisters(1, _endpoint.RegisterCount);
                if (registers is null || registers.Length == 0)
                {
                    PollFailureCount++;
                    return null;
                }

                // In EasyModbus, returned array has index 0 corresponding to startingAddress
                // Build a 1-indexed array for the mapper
                var normalizedRegisters = new int[registers.Length + 1];
                Array.Copy(registers, 0, normalizedRegisters, 1, registers.Length);

                var telemetry = ModbusTelemetryMapper.FromRegisters(
                    _endpoint.DeviceId,
                    _endpoint.DeviceType,
                    normalizedRegisters,
                    _endpoint.Port,
                    _endpoint.UnitId);

                LastTelemetry = telemetry;
                PollSuccessCount++;
                IsConnected = true;
                LastError = string.Empty;
                return telemetry;
            }
            catch (Exception ex)
            {
                IsConnected = false;
                PollFailureCount++;
                LastError = ex.Message;
                Disconnect();
                return null;
            }
        }
    }

    public bool ExecuteCommand(DeviceCommandMessage command)
    {
        lock (_lock)
        {
            try
            {
                EnsureConnected();
                if (_client is null || !_client.Connected) return false;

                ModbusTelemetryMapper.ApplyCommandToModbusClient(_client, command);
                return true;
            }
            catch (Exception ex)
            {
                LastError = $"Command failed: {ex.Message}";
                return false;
            }
        }
    }

    private void EnsureConnected()
    {
        if (_client is not null && _client.Connected) return;

        try
        {
            _client = new ModbusClient(_endpoint.Host, _endpoint.Port)
            {
                UnitIdentifier = _endpoint.UnitId,
                ConnectionTimeout = 1000
            };
            _client.Connect();
            IsConnected = _client.Connected;
        }
        catch (Exception ex)
        {
            IsConnected = false;
            LastError = ex.Message;
        }
    }

    private void Disconnect()
    {
        try
        {
            if (_client is not null && _client.Connected)
            {
                _client.Disconnect();
            }
        }
        catch { }
        finally
        {
            _client = null;
        }
    }

    public void Dispose()
    {
        Disconnect();
    }
}
