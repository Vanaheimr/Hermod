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

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

/// <summary>
/// A server sent a post-handshake CertificateRequest, which RFC 9001 §4.4 forbids: QUIC's
/// multiplexing "prevents clients from correlating the certificate request with the application-level
/// event that triggered it", so servers MUST NOT send one and clients MUST treat receipt as a
/// connection error of type PROTOCOL_VIOLATION.
/// <para>
/// Its own type, so the QUIC layer above can map it to that specific transport error rather than to
/// the generic crypto failure every other handshake exception becomes.
/// </para>
/// </summary>
public sealed class PostHandshakeAuthenticationException(string message) : Exception(message);
