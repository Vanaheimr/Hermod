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

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Frames;

/// <summary>
/// Frame type values in QUIC v1 (RFC 9000, table 3). Not exhaustive – grows with the phases.
/// </summary>
public static class FrameType
{
    public const ulong Padding = 0x00;
    public const ulong Ping = 0x01;
    public const ulong AckNoEcn = 0x02;
    public const ulong AckEcn = 0x03;
    public const ulong ResetStream = 0x04;
    public const ulong StopSending = 0x05;
    public const ulong Crypto = 0x06;
    public const ulong NewToken = 0x07;

    /// <summary>
    /// STREAM frames occupy 0x08..0x0f; the lower 3 bits encode OFF/LEN/FIN.
    /// </summary>
    public const ulong StreamBase = 0x08;
    public const ulong StreamMask = 0x08;
    public const byte StreamFinBit = 0x01;
    public const byte StreamLenBit = 0x02;
    public const byte StreamOffBit = 0x04;

    public const ulong MaxData = 0x10;
    public const ulong MaxStreamData = 0x11;
    public const ulong MaxStreamsBidi = 0x12;
    public const ulong MaxStreamsUni = 0x13;
    public const ulong DataBlocked = 0x14;
    public const ulong StreamDataBlocked = 0x15;
    public const ulong StreamsBlockedBidi = 0x16;
    public const ulong StreamsBlockedUni = 0x17;

    /// <summary>
    /// DATAGRAM frames (RFC 9221 §4): 0x30 without, 0x31 with a length field (LEN bit 0x01).
    /// </summary>
    public const ulong DatagramNoLength = 0x30;
    public const ulong DatagramWithLength = 0x31;

    public const ulong PathChallenge = 0x1a;
    public const ulong PathResponse = 0x1b;
    public const ulong ConnectionCloseQuic = 0x1c;
    public const ulong ConnectionCloseApp = 0x1d;
    public const ulong HandshakeDone = 0x1e;

    /// <summary>
    /// RESET_STREAM_AT (draft-ietf-quic-reliable-stream-reset §4): RESET_STREAM with guaranteed
    /// partial delivery up to a reliable size.
    /// </summary>
    public const ulong ResetStreamAt = 0x24;

    public static bool IsStream(ulong type) => type is >= StreamBase and <= 0x0f;
}
