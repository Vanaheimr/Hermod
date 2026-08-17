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

#region Usings

using System.Net.Sockets;
using System.Security.Cryptography;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    // Hermod has an IPAddress of its own — a static helper class in the
    // enclosing namespace — and a member of an enclosing namespace binds before
    // a file-level using alias, so these have to live inside the declaration to
    // win.
    using IPAddress   = System.Net.IPAddress;
    using IPEndPoint  = System.Net.IPEndPoint;


    /// <summary>
    /// The client half of a DNS Cookie: a value a client sends to one server and
    /// recognises coming back (RFC 7873 §4.1).
    /// </summary>
    /// <remarks>
    /// <para>
    /// §4.1 asks for "a pseudorandom function of the Client IP Address, the
    /// Server IP Address, and a secret quantity known only to the client", and
    /// each of the three inputs is there for a reason worth keeping straight.
    /// </para>
    /// <para>
    /// The <b>secret</b> is what makes the value unguessable, which is the whole
    /// mechanism: a cookie an off-path attacker could predict proves nothing when
    /// it comes back.
    /// </para>
    /// <para>
    /// The <b>server address</b> is a MUST in disguise — §4.1: "a client MUST
    /// send Client Cookies that will usually be different for any two servers at
    /// different IP addresses". One server must not learn the value another
    /// server sees.
    /// </para>
    /// <para>
    /// The <b>client address</b> is the one that is easy to leave out, and it is
    /// there for privacy rather than for correctness. §4.1 gives both reasons:
    /// so that the cookie "cannot be used to track a client if the Client IP
    /// Address changes due to privacy mechanisms", and so that a network device
    /// "formerly on path but ... no longer on path" cannot impersonate the client
    /// afterwards. Deriving the cookie rather than storing one gets that for
    /// free: change address and the cookie changes with it, with nothing to
    /// remember to invalidate.
    /// </para>
    /// <para>
    /// Which is why this derives rather than remembers. A stored random value
    /// would be just as stable and just as unguessable, and would follow the
    /// client across every address change for as long as the process lived.
    /// </para>
    /// </remarks>
    public sealed class DNSClientCookies
    {

        #region Data

        private readonly Byte[] secret;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a cookie source with a fresh secret.
        /// </summary>
        /// <param name="Secret">
        /// The client secret. A fresh 32-octet one when omitted — §4.1 asks for
        /// "at least 64 bits of entropy", and there is no reason to be near the
        /// floor of that.
        /// </param>
        public DNSClientCookies(Byte[]? Secret = null)
        {

            if (Secret is not null && Secret.Length < 8)
                throw new ArgumentException("RFC 7873 §4.1: the client secret should have at least 64 bits of entropy.", nameof(Secret));

            this.secret = Secret ?? RandomNumberGenerator.GetBytes(32);

        }

        #endregion

        #region For(ServerAddress)

        /// <summary>
        /// The client cookie to send to this server.
        /// </summary>
        /// <param name="ServerAddress">The server the query is going to.</param>
        /// <remarks>
        /// The same eight octets every time, for as long as this client keeps its
        /// secret and reaches that server from the same address — which is what
        /// makes a server cookie worth caching. A server cookie is bound to the
        /// client cookie it was issued for, so a client cookie that changed per
        /// query would throw away the server's answer with every question.
        /// </remarks>
        public Byte[] For(IPAddress ServerAddress)
        {

            var localAddress = LocalAddressFor(ServerAddress);

            var input        = new List<Byte>(64);

            input.AddRange(localAddress?.GetAddressBytes() ?? []);
            input.AddRange(ServerAddress.GetAddressBytes());

            return HMACSHA256.HashData(secret, input.ToArray())[..8];

        }


        /// <summary>
        /// The COOKIE option to send to this server, carrying a stored server cookie when there is one.
        /// </summary>
        /// <param name="ServerAddress">The server the query is going to.</param>
        /// <param name="ServerCookie">The server cookie remembered for it, if any.</param>
        public EDNSCookieOption OptionFor(IPAddress  ServerAddress,
                                          Byte[]?    ServerCookie = null)

            => new (For(ServerAddress),
                    ServerCookie);

        #endregion

        #region (private) LocalAddressFor(ServerAddress)

        /// <summary>
        /// The local address this host would use to reach that server.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A connected UDP socket answers this without sending anything: connect
        /// is a purely local operation for datagrams, and the kernel fills in the
        /// local endpoint from its own routing table.
        /// </para>
        /// <para>
        /// Asked every time, deliberately. Caching it would be the obvious
        /// optimisation and would undo the point of deriving the cookie at all:
        /// a client that moved networks would go on producing cookies from the
        /// address it used to have, which is exactly the value §4.1 puts the
        /// client address into the input to retire. The cost of not caching is a
        /// socket that is created, asked one question and closed — no packets, no
        /// round trip — against a query that is about to cross a network.
        /// </para>
        /// <para>
        /// A failure here is not fatal. Deriving from the server address and the
        /// secret alone still gives a cookie that is unguessable and
        /// server-specific; it merely stops changing by itself when this host's
        /// address does.
        /// </para>
        /// </remarks>
        private static IPAddress? LocalAddressFor(IPAddress ServerAddress)
        {

            try
            {

                using var socket = new Socket(ServerAddress.AddressFamily, SocketType.Dgram, ProtocolType.Udp);

                socket.Connect(ServerAddress, 53);

                return (socket.LocalEndPoint as IPEndPoint)?.Address;

            }
            catch (SocketException)
            {
                return null;
            }

        }

        #endregion

    }

}
