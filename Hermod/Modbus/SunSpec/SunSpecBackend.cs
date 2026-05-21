/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System.Buffers.Binary;

namespace org.GraphDefined.Vanaheimr.Hermod.SunSpecModbusTLS.Common;

/// <summary>
/// IModbusBackend that executes already-authorized requests against an
/// <see cref="ISunSpecDevice"/>. The backend translates Modbus PDUs into
/// device read/write calls and back; the SunSpec RBAC policy has already
/// been enforced upstream (in the TLS frontend).
///
/// This backend is content-agnostic - it works for any SunSpec device shape
/// (meter, inverter, battery, EVSE controller, ...) because the entire
/// device-specific knowledge lives in <see cref="ISunSpecDevice"/>.
/// </summary>
public sealed class SunSpecBackend : IModbusBackend
{
    private readonly ISunSpecDevice _device;

    public SunSpecBackend(ISunSpecDevice device) => _device = device;

    public Task<byte[]> ProcessRequestAsync(byte unitId, ReadOnlyMemory<byte> requestPdu, CancellationToken ct)
    {
        var pdu = requestPdu.Span;
        var fc = (ModbusFunctionCodes)pdu[0];

        byte[] resp = fc switch
        {
            ModbusFunctionCodes.ReadHoldingRegisters
              or ModbusFunctionCodes.ReadInputRegisters => HandleRead(fc, pdu),

            ModbusFunctionCodes.WriteSingleRegister     => HandleWriteSingle(fc, pdu),
            ModbusFunctionCodes.WriteMultipleRegisters  => HandleWriteMultiple(fc, pdu),

            // Anything else slipped past the frontend's policy parser; the
            // device doesn't know it -> Illegal Function.
            _ => ModbusPDU.BuildException(fc, ModbusExceptionCode.IllegalFunction),
        };

        return Task.FromResult(resp);
    }

    private byte[] HandleRead(ModbusFunctionCodes fc, ReadOnlySpan<byte> pdu)
    {
        var addr = BinaryPrimitives.ReadUInt16BigEndian(pdu.Slice(1, 2));
        var qty  = BinaryPrimitives.ReadUInt16BigEndian(pdu.Slice(3, 2));
        if (qty is 0 or > 125)
            return ModbusPDU.BuildException(fc, ModbusExceptionCode.IllegalDataValue);

        var values = _device.ReadHolding(addr, qty);
        if (values is null)
            return ModbusPDU.BuildException(fc, ModbusExceptionCode.IllegalDataAddress);

        return ModbusPDU.BuildReadHoldingResponse(values);
    }

    private byte[] HandleWriteSingle(ModbusFunctionCodes fc, ReadOnlySpan<byte> pdu)
    {
        var addr = BinaryPrimitives.ReadUInt16BigEndian(pdu.Slice(1, 2));
        var val  = BinaryPrimitives.ReadUInt16BigEndian(pdu.Slice(3, 2));
        if (!_device.WriteHolding(addr, val))
            return ModbusPDU.BuildException(fc, ModbusExceptionCode.IllegalDataAddress);
        return ModbusPDU.BuildWriteSingleRegisterResponse(addr, val);
    }

    private byte[] HandleWriteMultiple(ModbusFunctionCodes fc, ReadOnlySpan<byte> pdu)
    {
        var addr      = BinaryPrimitives.ReadUInt16BigEndian(pdu.Slice(1, 2));
        var qty       = BinaryPrimitives.ReadUInt16BigEndian(pdu.Slice(3, 2));
        var byteCount = pdu[5];
        if (qty is 0 or > 123 || byteCount != qty * 2 || pdu.Length != 6 + byteCount)
            return ModbusPDU.BuildException(fc, ModbusExceptionCode.IllegalDataValue);

        var values = new ushort[qty];
        for (var i = 0; i < qty; i++)
            values[i] = BinaryPrimitives.ReadUInt16BigEndian(pdu.Slice(6 + 2 * i, 2));

        if (!_device.WriteHolding(addr, values))
            return ModbusPDU.BuildException(fc, ModbusExceptionCode.IllegalDataAddress);

        return ModbusPDU.BuildWriteMultipleRegistersResponse(addr, qty);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Factory that hands out a thin <see cref="SunSpecBackend"/> wrapper per
/// inbound TLS connection. The underlying device is shared (singleton
/// lifetime - scoped to the server process).
/// </summary>
public sealed class SunSpecBackendFactory : IModbusBackendFactory
{
    private readonly ISunSpecDevice _device;
    public SunSpecBackendFactory(ISunSpecDevice device) => _device = device;

    public Task<IModbusBackend> CreateAsync(long connectionId, string? role, CancellationToken ct)
        => Task.FromResult<IModbusBackend>(new SunSpecBackend(_device));
}
