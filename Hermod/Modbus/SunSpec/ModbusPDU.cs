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
/// Helpers for building / parsing Modbus PDUs for the FCs we implement.
/// </summary>
public static class ModbusPDU
{



    // -------- Requests (used by client) --------

    public static Byte[] BuildReadHoldingRegisters(ushort startAddress, ushort quantity)
    {

        if (quantity is 0 or > 125)
            throw new ArgumentOutOfRangeException(nameof(quantity));

        var pdu = new byte[5];
        pdu[0] = (Byte) ModbusFunctionCodes.ReadHoldingRegisters;
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1, 2), startAddress);
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3, 2), quantity);

        return pdu;

    }

    public static Byte[] BuildWriteSingleRegister(ushort address, ushort value)
    {
        var pdu = new byte[5];
        pdu[0] = (Byte) ModbusFunctionCodes.WriteSingleRegister;
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1, 2), address);
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3, 2), value);

        return pdu;

    }

    public static Byte[] BuildWriteMultipleRegisters(ushort address, ushort[] values)
    {

        if (values.Length is 0 or > 123)
            throw new ArgumentOutOfRangeException(nameof(values));

        var byteCount = (Byte) (values.Length * 2);
        var pdu       = new Byte[6 + byteCount];

        pdu[0] = (Byte) ModbusFunctionCodes.WriteMultipleRegisters;
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1, 2), address);
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3, 2), (ushort) values.Length);
        pdu[5] = byteCount;

        for (var i = 0; i < values.Length; i++)
            BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(6 + 2 * i, 2), values[i]);

        return pdu;

    }







    // -------- Response decoding --------

    /// <summary>
    /// If the PDU is an exception response, returns the exception code; otherwise null.
    /// </summary>
    public static ModbusExceptionCode? TryGetException(ReadOnlySpan<byte> pdu)
    {

        if (pdu.Length < 2)
            return null;

        return (pdu[0] & 0x80) != 0
                   ? (ModbusExceptionCode) pdu[1]
                   : null;

    }

    /// <summary>Decode the register payload of an FC03/FC04 response.</summary>
    public static ushort[] DecodeReadResponse(ReadOnlySpan<byte> pdu)
    {

        if (pdu.Length < 2)
            throw new InvalidDataException("Response too short.");

        if ((pdu[0] & 0x80) != 0)
            throw new ModbusException((ModbusExceptionCode)pdu[1]);

        var byteCount = pdu[1];
        if (pdu.Length < 2 + byteCount || byteCount % 2 != 0)
            throw new InvalidDataException("Inconsistent byte count in response.");

        var regs = new ushort[byteCount / 2];
        for (var i = 0; i < regs.Length; i++)
            regs[i] = BinaryPrimitives.ReadUInt16BigEndian(pdu.Slice(2 + 2 * i, 2));

        return regs;

    }








    // -------- Server-side response builders --------

    public static byte[] BuildException(ModbusFunctionCodes fc,
                                        ModbusExceptionCode ex)

        => [(Byte)((Byte) fc | 0x80), (Byte) ex];

    public static byte[] BuildReadHoldingResponse(ushort[] registers)
    {
        var byteCount = (Byte)(registers.Length * 2);
        var pdu       = new Byte[2 + byteCount];
        pdu[0] = (Byte)ModbusFunctionCodes.ReadHoldingRegisters;
        pdu[1] = byteCount;

        for (var i = 0; i < registers.Length; i++)
            BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(2 + 2 * i, 2), registers[i]);

        return pdu;

    }

    public static byte[] BuildWriteSingleRegisterResponse(ushort address, ushort value)
        => BuildWriteSingleRegister(address, value); // request and response have identical PDU

    public static byte[] BuildWriteMultipleRegistersResponse(ushort address, ushort quantity)
    {

        var pdu = new Byte[5];

        pdu[0] = (Byte) ModbusFunctionCodes.WriteMultipleRegisters;
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1, 2), address);
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3, 2), quantity);

        return pdu;

    }

}
