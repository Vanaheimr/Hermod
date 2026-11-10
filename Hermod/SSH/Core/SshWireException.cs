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

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// An error while reading or writing the SSH binary wire format (RFC 4251, section 5):
    /// truncated data, a length field that exceeds the available bytes or a configured limit,
    /// or otherwise malformed input.
    /// </summary>
    /// <remarks>
    /// This is always raised on untrusted, peer-supplied data, so it must be caught and turned
    /// into a clean protocol error / disconnect rather than being allowed to escape as an
    /// unexpected exception.
    /// </remarks>
    public class SshWireException : Exception
    {

        /// <summary>
        /// Create a new SSH wire-format exception.
        /// </summary>
        /// <param name="Message">A message describing what was malformed.</param>
        public SshWireException(String Message)

            : base(Message)

        { }


        /// <summary>
        /// Create a new SSH wire-format exception wrapping an inner exception.
        /// </summary>
        /// <param name="Message">A message describing what was malformed.</param>
        /// <param name="InnerException">The inner exception.</param>
        public SshWireException(String     Message,
                                Exception  InnerException)

            : base(Message, InnerException)

        { }

    }


    /// <summary>
    /// The connection ended while a packet was being read: the peer went away rather than sending
    /// something malformed.
    /// </summary>
    /// <remarks>
    /// A wire exception still, so that everything that catches those goes on catching this. Its own
    /// type so that a server can tell a client that closed its window from one that spoke nonsense -
    /// the first is not a protocol error, and a log that called every logout one would be no use.
    /// </remarks>
    public sealed class SshConnectionClosedException : SshWireException
    {

        /// <summary>
        /// Create a new connection-closed exception.
        /// </summary>
        /// <param name="Message">What was being read when the connection ended.</param>
        public SshConnectionClosedException(String Message)

            : base(Message)

        { }

    }


    /// <summary>
    /// The peer did not signal strict KEX in its first KEXINIT, and this side requires it.
    /// </summary>
    /// <remarks>
    /// Strict KEX (<c>kex-strict-c-v00@openssh.com</c>, <c>kex-strict-s-v00@openssh.com</c>) is what
    /// stops the prefix truncation attack Terrapin (CVE-2023-48795) on <c>chacha20-poly1305@openssh.com</c>
    /// and on CBC with Encrypt-then-MAC. A side that requires it ends the connection after the first
    /// key exchange, before anything is authenticated over it. A wire exception still, so that what
    /// catches those goes on catching this; its own type, so that a caller can say what went wrong
    /// rather than "the handshake failed".
    /// </remarks>
    public sealed class SshStrictKexRequiredException : SshWireException
    {

        /// <summary>
        /// Whether this side is the server, so the peer that did not signal strict KEX is a client.
        /// </summary>
        public Boolean  WeAreServer  { get; }

        /// <summary>
        /// Create a new exception for a peer that did not signal strict KEX.
        /// </summary>
        /// <param name="WeAreServer">Whether this side is the server.</param>
        public SshStrictKexRequiredException(Boolean WeAreServer)

            : base($"The {(WeAreServer ? "client" : "server")} does not signal strict KEX, which this side requires against Terrapin (CVE-2023-48795).")

        {
            this.WeAreServer = WeAreServer;
        }

    }

}
