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
/// Abstract base for SunSpec devices. Owns the in-memory register array, the
/// access lock, and the Common Model 1 init pattern that every SunSpec device
/// uses identically.
///
/// Subclasses contribute:
///   * a register count (via the constructor)
///   * the per-device base address (via <see cref="BaseAddress"/>)
///   * the per-device address classification (Protected / Commanded sets)
///   * a register-by-register read-only mask (via <see cref="IsReadOnlyOffset"/>)
///   * device-specific model fields (via the protected init helpers / direct register writes)
///   * optional reaction to writes (e.g. command registers) via <see cref="OnRegistersWritten"/>
///
/// All read/write paths are mutex-guarded. Subclasses MUST hold <see cref="Lock"/>
/// when they modify <see cref="Registers"/> outside of the framework methods
/// (the simulator pattern in <see cref="SunSpecMeterDevice"/> is the canonical
/// example).
/// </summary>
public abstract class ASunSpecDeviceBase : ISunSpecDevice
{

    /// <summary>
    /// Backing store for holding registers. Indexed by *offset*, not address.
    /// </summary>
    protected readonly ushort[] Registers;

    /// <summary>
    /// Coarse mutex protecting <see cref="Registers"/>.
    /// </summary>
    protected readonly Lock Lock = new();

    public abstract ushort BaseAddress { get; }
    public int RegisterCount => Registers.Length;
    public abstract string DisplayName { get; }
    public abstract IReadOnlySet<ushort> ProtectedAddresses { get; }
    public abstract IReadOnlySet<ushort> CommandedAddresses { get; }

    protected ASunSpecDeviceBase(int registerCount)
    {

        if (registerCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(registerCount));

        Registers = new ushort[registerCount];

    }





    // ---- ISunSpecDevice surface ----

    public ushort[]? ReadHolding(ushort startAddress, ushort quantity)
    {
        lock (Lock)
        {
            var startOffset = startAddress - BaseAddress;
            if (startOffset < 0 || startOffset + quantity > Registers.Length) return null;
            var slice = new ushort[quantity];
            Array.Copy(Registers, startOffset, slice, 0, quantity);
            return slice;
        }
    }

    public bool WriteHolding(ushort address, ushort value) => WriteHolding(address, [value]);

    public bool WriteHolding(ushort address, ReadOnlySpan<ushort> values)
    {
        lock (Lock)
        {
            var startOffset = address - BaseAddress;
            if (startOffset < 0 || startOffset + values.Length > Registers.Length) return false;

            // Refuse writes if ANY register in the requested range is read-only.
            // (Per Modbus tradition, FC10 is atomic - either all or nothing.)
            for (var i = 0; i < values.Length; i++)
            {
                var off = (ushort)(startOffset + i);
                if (IsReadOnlyOffset(off))
                    return false;
            }

            for (var i = 0; i < values.Length; i++)
                Registers[startOffset + i] = values[i];

            // Fire the per-device write hook (still inside the lock).
            OnRegistersWritten(address, values);
        }
        return true;
    }

    /// <summary>
    /// Returns true if the register at <paramref name="offset"/> (0-based, relative
    /// to <see cref="BaseAddress"/>) is read-only. The default refuses no writes;
    /// subclasses typically refuse marker / model-header / measurement registers.
    /// </summary>
    protected virtual bool IsReadOnlyOffset(ushort offset) => false;

    /// <summary>
    /// Hook invoked AFTER a successful write while still holding <see cref="Lock"/>.
    /// Use for command registers ("write magic value to trigger action") or other
    /// side-effects that must be atomic with the write itself.
    /// </summary>
    protected virtual void OnRegistersWritten(ushort startAddress, ReadOnlySpan<ushort> values) { }











    // ---- Helpers for subclass init code ----

    /// <summary>
    /// Lay out the SunSpec Common Model 1 at the standard offsets [0..69]. Must be
    /// called from the subclass constructor BEFORE writing the first device-specific
    /// model. Assumes <see cref="BaseAddress"/> sits at the SunS marker.
    /// </summary>
    protected void InitCommonModel(String manufacturer,
                                   String model,
                                   String options,
                                   String version,
                                   String serialNumber,
                                   ushort deviceAddress = 1)
    {

        // SunS marker - high half in reg[0], low half in reg[1] (big-endian on the wire).
        Registers[SunSpecCommon.OffMarker]     = (ushort)(SunSpecCommon.SunSpecMarker >> 16);
        Registers[SunSpecCommon.OffMarker + 1] = (ushort)(SunSpecCommon.SunSpecMarker & 0xFFFF);

        Registers[SunSpecCommon.OffCommonId]  = SunSpecCommon.CommonModelId;
        Registers[SunSpecCommon.OffCommonLen] = SunSpecCommon.CommonModelLength;

        WriteString(Registers.AsSpan(SunSpecCommon.OffCommonMn,  16), manufacturer);
        WriteString(Registers.AsSpan(SunSpecCommon.OffCommonMd,  16), model);
        WriteString(Registers.AsSpan(SunSpecCommon.OffCommonOpt,  8), options);
        WriteString(Registers.AsSpan(SunSpecCommon.OffCommonVr,   8), version);
        WriteString(Registers.AsSpan(SunSpecCommon.OffCommonSN,  16), serialNumber);
        Registers[SunSpecCommon.OffCommonDA]  = deviceAddress;
        Registers[SunSpecCommon.OffCommonPad] = 0;

    }

    /// <summary>
    /// Write the SunSpec end-of-models marker (id=0xFFFF, length=0) at the given offset.
    /// </summary>
    protected void WriteEndModelMarker(ushort offset)
    {
        Registers[offset]     = SunSpecCommon.EndModelId;
        Registers[offset + 1] = 0;
    }

    /// <summary>Pack an ASCII string into <paramref name="dst"/>, big-endian, padded with NUL.</summary>
    protected static void WriteString(Span<ushort> dst, string s)
    {
        var bytes = new byte[dst.Length * 2];
        var src = System.Text.Encoding.ASCII.GetBytes(s);
        Array.Copy(src, 0, bytes, 0, Math.Min(src.Length, bytes.Length));
        for (var i = 0; i < dst.Length; i++)
            dst[i] = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(2 * i, 2));
    }

    /// <summary>Read a uint32 stored across two big-endian holding registers.</summary>
    protected uint ReadUInt32(ushort offset)
        => ((uint)Registers[offset] << 16) | Registers[offset + 1];

    /// <summary>Write a uint32 across two big-endian holding registers.</summary>
    protected void WriteUInt32(ushort offset, uint value)
    {
        Registers[offset]     = (ushort)(value >> 16);
        Registers[offset + 1] = (ushort)(value & 0xFFFF);
    }

    public virtual void Dispose() {
        GC.SuppressFinalize(this);
    }

}
