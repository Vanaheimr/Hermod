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

namespace org.GraphDefined.Vanaheimr.Hermod.Quic;

/// <summary>
/// The ECN codepoint in the IP header (RFC 3168, the two low-order bits of the DS/TOS field). QUIC
/// uses it as a congestion signal (RFC 9000 §13.4, RFC 9002 §7.3): the sender marks packets with
/// ECT(0)/ECT(1); an overloaded router may "upgrade" them to CE. The receiver counts the codepoints
/// per packet-number space and reports the sums back in the ACK frame (type 0x03).
/// </summary>
public enum EcnCodepoint : byte
{
    /// <summary>
    /// Not-ECT (00): ECN is not used for this packet.
    /// </summary>
    NotEct = 0b00,

    /// <summary>
    /// ECT(1) (01): ECN-capable, variant 1.
    /// </summary>
    Ect1 = 0b01,

    /// <summary>
    /// ECT(0) (10): ECN-capable, variant 0.
    /// </summary>
    Ect0 = 0b10,

    /// <summary>
    /// CE (11): congestion experienced – a router signals overload.
    /// </summary>
    Ce = 0b11,
}
