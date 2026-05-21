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
/// Modbus Application Protocol (MBAP) ADU as defined in [MBTCP].
/// Wire format (BIG-ENDIAN):
///
///   +-----+-----+-----+-----+-----+-----+-----+--------------------+
///   | TID(2)    | PID(2)=0  | LEN(2)    | UID | PDU = FC + Data    |
///   +-----+-----+-----+-----+-----+-----+-----+--------------------+
///
///   TID  = Transaction Identifier (echoed by server)
///   PID  = Protocol Identifier, MUST be 0 for Modbus
///   LEN  = number of following bytes (= UID + PDU length)
///   UID  = Unit Identifier
///   PDU  = function code + payload
///
/// In mbaps the entire ADU is transported INSIDE the TLS Application Data
/// stream. There is NO change to the ADU vs. plain mbap (R-05).
/// </summary>
public sealed record ModbusFrame(ushort TransactionId, byte UnitId, byte[] Pdu)
{
    public const int MbapHeaderLength = 7;
    public const int MaxPduLength     = 253;          // [MB] §4.1
    public const int MaxAduLength     = MbapHeaderLength + MaxPduLength;

    /// <summary>Encode the frame into a fresh byte array.</summary>
    public byte[] ToBytes()
    {
        if (Pdu.Length is 0 or > MaxPduLength)
            throw new ArgumentOutOfRangeException(nameof(Pdu), Pdu.Length, "PDU length out of range.");

        var buf = new byte[MbapHeaderLength + Pdu.Length];
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(0, 2), TransactionId);
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(2, 2), 0);                    // ProtocolID
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(4, 2), (ushort)(Pdu.Length + 1)); // Length = UID(1) + PDU
        buf[6] = UnitId;
        Pdu.CopyTo(buf.AsSpan(7));
        return buf;
    }

    /// <summary>Read exactly one frame from <paramref name="stream"/>. Throws on protocol error.</summary>
    public static async Task<ModbusFrame> ReadAsync(Stream stream, CancellationToken ct)
    {
        var header = new byte[MbapHeaderLength];
        await ReadExactAsync(stream, header, ct).ConfigureAwait(false);

        var tid       = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(0, 2));
        var pid       = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(2, 2));
        var length    = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4, 2));
        var unitId    = header[6];

        if (pid != 0)
            throw new InvalidDataException($"Invalid Protocol ID 0x{pid:X4} (must be 0x0000).");
        if (length < 2 || length > MaxPduLength + 1)
            throw new InvalidDataException($"Invalid MBAP length {length}.");

        var pdu = new byte[length - 1];
        await ReadExactAsync(stream, pdu, ct).ConfigureAwait(false);
        return new ModbusFrame(tid, unitId, pdu);
    }

    private static async Task ReadExactAsync(Stream s, Memory<byte> buf, CancellationToken ct)
    {
        var read = 0;
        while (read < buf.Length)
        {
            var n = await s.ReadAsync(buf[read..], ct).ConfigureAwait(false);
            if (n == 0) throw new EndOfStreamException("Peer closed connection mid-frame.");
            read += n;
        }
    }
}
