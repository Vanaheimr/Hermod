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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP3.WebTransport;

/// <summary>
/// Constants from draft-ietf-webtrans-http3-13 (WebTransport over HTTP/3): SETTINGS, stream/frame
/// signal values, error codes and capsule types (§4, §5, §6, §9).
/// </summary>
public static class WebTransportConstants
{
    // ---- HTTP/3 SETTINGS (§9.2) -----------------------------------------------------------

    /// <summary>SETTINGS_WT_MAX_SESSIONS — max. concurrent sessions; &gt;1 activates flow control (§5.1).</summary>
    public const ulong SettingMaxSessions = 0x14e9cd29;

    /// <summary>
    /// The same setting under the codepoint draft-07 assigned it (<c>SETTINGS_WEBTRANS_MAX_SESSIONS</c>
    /// in quiche). Browsers are behind the draft: Chrome 150 logs 0x14e9cd29 as an unsupported setting
    /// type and then refuses the opening handshake, because it looks for this value instead. We
    /// announce and accept both, which costs an ignored SETTINGS entry per peer and is the only way a
    /// browser can open a session at all.
    /// </summary>
    public const ulong SettingMaxSessionsDraft07 = 0xc671706a;
    public const ulong SettingInitialMaxStreamsUni = 0x2b64;
    public const ulong SettingInitialMaxStreamsBidi = 0x2b65;
    public const ulong SettingInitialMaxData = 0x2b61;

    // ---- Stream/frame signals (§4.1/§4.2, §9.3/§9.4) --------------------------------------

    /// <summary>Uni-stream type: 0x54 ‖ session ID ‖ payload (§4.1).</summary>
    public const ulong UniStreamType = 0x54;

    /// <summary>WT_STREAM signal at the start of a bidi stream: 0x41 ‖ session ID ‖ payload (§4.2).</summary>
    public const ulong BidiStreamSignal = 0x41;

    // ---- Error codes (§9.5) ---------------------------------------------------------------

    public const ulong BufferedStreamRejected = 0x3994bd84;
    public const ulong SessionGone = 0x170d7b68;

    /// <summary>WT_APPLICATION_ERROR range (§4.3): 32-bit app codes are mapped into it.</summary>
    public const ulong ApplicationErrorFirst = 0x52e4a40fa8db;
    public const ulong ApplicationErrorLast = 0x52e5ac983162;

    // ---- Capsule types (§6, §5.6, §9.6) ---------------------------------------------------

    public const ulong CapsuleCloseSession = 0x2843;
    public const ulong CapsuleMaxStreamsBidi = 0x190B4D3F;
    public const ulong CapsuleMaxStreamsUni = 0x190B4D40;
    public const ulong CapsuleStreamsBlockedBidi = 0x190B4D43;
    public const ulong CapsuleStreamsBlockedUni = 0x190B4D44;
    public const ulong CapsuleMaxData = 0x190B4D3D;
    public const ulong CapsuleDataBlocked = 0x190B4D41;

    /// <summary>
    /// Maps a 32-bit WebTransport application error onto the HTTP/3 error-code range
    /// (§4.3): <c>first + n + floor(n / 0x1e)</c> — skipping the reserved greasing codepoints
    /// of the form 0x1f·N + 0x21.
    /// </summary>
    public static ulong ApplicationErrorToHttp(uint code)
        => ApplicationErrorFirst + code + code / 0x1e;

    /// <summary>
    /// Reverse-maps an HTTP/3 error code in the WT_APPLICATION_ERROR range to the 32-bit code
    /// (§4.3); returns <c>null</c> when it is outside the range or a reserved codepoint.
    /// </summary>
    public static uint? HttpToApplicationError(ulong http)
    {
        if (http < ApplicationErrorFirst || http > ApplicationErrorLast)
            return null;
        if ((http - 0x21) % 0x1f == 0)
            return null; // reserved greasing codepoint (§8.1 HTTP/3)
        ulong shifted = http - ApplicationErrorFirst;
        return (uint)(shifted - shifted / 0x1f);
    }
}
