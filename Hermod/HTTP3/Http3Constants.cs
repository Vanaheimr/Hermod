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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP3;

/// <summary>
/// HTTP/3 frame types (RFC 9114 §7.2). Unknown types are ignored (greasing).
/// </summary>
public static class Http3FrameType
{
    public const ulong Data = 0x00;
    public const ulong Headers = 0x01;
    public const ulong CancelPush = 0x03;
    public const ulong Settings = 0x04;
    public const ulong PushPromise = 0x05;
    public const ulong GoAway = 0x07;
    public const ulong MaxPushId = 0x0d;

    /// <summary>
    /// PRIORITY_UPDATE for request streams (RFC 9218 §7.2) — client→server only, on the control stream.
    /// </summary>
    public const ulong PriorityUpdateRequest = 0xF0700;

    /// <summary>
    /// PRIORITY_UPDATE for push streams (RFC 9218 §7.2).
    /// </summary>
    public const ulong PriorityUpdatePush = 0xF0701;
}

/// <summary>
/// Type prefixes of unidirectional HTTP/3 streams (RFC 9114 §6.2, RFC 9204 §4.2).
/// </summary>
public static class Http3StreamType
{
    public const ulong Control = 0x00;
    public const ulong Push = 0x01;
    public const ulong QpackEncoder = 0x02;
    public const ulong QpackDecoder = 0x03;
}

/// <summary>
/// SETTINGS parameter IDs (RFC 9114 §7.2.4.1, RFC 9204 §5).
/// </summary>
public static class Http3Setting
{
    public const ulong QpackMaxTableCapacity = 0x01;
    public const ulong MaxFieldSectionSize = 0x06;
    public const ulong QpackBlockedStreams = 0x07;

    /// <summary>
    /// SETTINGS_ENABLE_CONNECT_PROTOCOL (RFC 8441 §3 / RFC 9220 §3, value 0x08): the server permits
    /// Extended CONNECT (the :protocol pseudo-header). The value MUST be 0 or 1.
    /// </summary>
    public const ulong EnableConnectProtocol = 0x08;

    /// <summary>
    /// SETTINGS_H3_DATAGRAM (RFC 9297 §2.1.1, value 0x33): the endpoint accepts HTTP/3 datagrams.
    /// The value MUST be 0 or 1; QUIC DATAGRAM frames may only flow once the setting has been
    /// SENT and RECEIVED with 1.
    /// </summary>
    public const ulong H3Datagram = 0x33;
}

/// <summary>
/// HTTP/3 error codes (RFC 9114 §8.1) — as application protocol error code in RESET_STREAM/
/// STOP_SENDING (stream errors) or CONNECTION_CLOSE type 0x1d (connection errors).
/// </summary>
public static class Http3Error
{
    public const ulong NoError = 0x0100;
    public const ulong GeneralProtocolError = 0x0101;
    public const ulong InternalError = 0x0102;
    public const ulong StreamCreationError = 0x0103;
    public const ulong ClosedCriticalStream = 0x0104;
    public const ulong FrameUnexpected = 0x0105;
    public const ulong FrameError = 0x0106;
    public const ulong ExcessiveLoad = 0x0107;
    public const ulong IdError = 0x0108;
    public const ulong SettingsError = 0x0109;
    public const ulong MissingSettings = 0x010a;
    public const ulong RequestRejected = 0x010b;
    public const ulong RequestCancelled = 0x010c;
    public const ulong RequestIncomplete = 0x010d;
    public const ulong MessageError = 0x010e;
    public const ulong ConnectError = 0x010f;
    public const ulong VersionFallback = 0x0110;

    /// <summary>
    /// H3_DATAGRAM_ERROR (RFC 9297, value 0x33): violations of the HTTP datagram format
    /// (connection error) or datagrams for requests without datagram semantics (stream error).
    /// </summary>
    public const ulong DatagramError = 0x33;
}
