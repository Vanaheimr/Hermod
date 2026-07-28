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

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;

/// <summary>
/// The encryption levels of QUIC/TLS 1.3 (RFC 9001 §2.1). Each level has its own keys, its own
/// packet-number space and its own CRYPTO byte stream. 0-RTT is not (yet) used.
/// </summary>
public enum EncryptionLevel : byte
{
    Initial = 0,
    Handshake = 1,
    Application = 2,
}
