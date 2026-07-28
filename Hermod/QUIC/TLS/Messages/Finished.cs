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

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Messages;

/// <summary>
/// The Finished handshake message (RFC 8446 §4.4.4): type 0x14 + 3-byte length + verify_data.
/// verify_data is an HMAC over the transcript (see <c>KeySchedule.FinishedVerifyData</c>).
/// </summary>
public static class Finished
{
    /// <summary>
    /// Builds the complete Finished message from the verify_data.
    /// </summary>
    public static byte[] BuildMessage(ReadOnlySpan<byte> verifyData)
    {
        byte[] message = new byte[4 + verifyData.Length];
        message[0] = (byte)HandshakeType.Finished;
        // 3-byte length (verify_data is 32 or 48 bytes, fits into one byte).
        message[1] = 0;
        message[2] = 0;
        message[3] = (byte)verifyData.Length;
        verifyData.CopyTo(message.AsSpan(4));
        return message;
    }
}
