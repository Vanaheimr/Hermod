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
    /// The algorithms agreed by both peers during KEXINIT (RFC 4253, section 7.1), plus the
    /// ext-info and strict-KEX outcomes.
    /// </summary>
    public sealed record NegotiatedAlgorithms(String   KeyExchange,
                                              String   HostKey,
                                              String   CipherClientToServer,
                                              String   CipherServerToClient,
                                              String   MacClientToServer,
                                              String   MacServerToClient,
                                              String   CompressionClientToServer,
                                              String   CompressionServerToClient,
                                              Boolean  ExtensionInfo,
                                              Boolean  StrictKex,
                                              Boolean  KexGuess2 = false);


    /// <summary>
    /// Implements the SSH algorithm negotiation (RFC 4253, section 7.1): for each category the chosen
    /// algorithm is the first one on the client's preference list that the server also supports.
    /// </summary>
    public static class AlgorithmNegotiation
    {

        #region Negotiate(ClientKexInit, ServerKexInit, WeAreServer)

        /// <summary>
        /// Negotiate the algorithms from both sides' KEXINIT messages.
        /// </summary>
        /// <param name="ClientKexInit">The client's KEXINIT.</param>
        /// <param name="ServerKexInit">The server's KEXINIT.</param>
        /// <param name="WeAreServer">Whether the caller is the server (affects the ext-info direction).</param>
        /// <exception cref="SshWireException">If no algorithm is common to both sides in some category.</exception>
        public static NegotiatedAlgorithms Negotiate(KexInitMessage  ClientKexInit,
                                                     KexInitMessage  ServerKexInit,
                                                     Boolean         WeAreServer)
        {

            // Strict-KEX (Terrapin): each side signals via its own marker; enabled when the peer's list
            // carries the peer's marker. We always include our own, so the peer's presence is decisive.
            var strictKex = WeAreServer
                                ? Contains(ClientKexInit.KexAlgorithms, SshAlgorithmNames.Kex.StrictKexClient)
                                : Contains(ServerKexInit.KexAlgorithms, SshAlgorithmNames.Kex.StrictKexServer);

            // Ext-info (RFC 8308): the peer advertises support with its own -c / -s marker.
            var extInfo = WeAreServer
                              ? Contains(ClientKexInit.KexAlgorithms, SshAlgorithmNames.Kex.ExtInfoClient)
                              : Contains(ServerKexInit.KexAlgorithms, SshAlgorithmNames.Kex.ExtInfoServer);

            // Dropbear's guessed-KEX extension, agreed only when both sides advertise it — and unlike the
            // -c/-s marker pairs both send the identical name, so it must be filtered out of the
            // key-exchange candidates or it could be "negotiated" as the key exchange itself.
            var kexGuess2 = Contains(ClientKexInit.KexAlgorithms, SshAlgorithmNames.Kex.KexGuess2) &&
                            Contains(ServerKexInit.KexAlgorithms, SshAlgorithmNames.Kex.KexGuess2);

            return new NegotiatedAlgorithms(
                       KeyExchange:                Pick("key exchange",       WithoutMarkers(ClientKexInit.KexAlgorithms),
                                                                              WithoutMarkers(ServerKexInit.KexAlgorithms)),
                       HostKey:                    Pick("host key",           ClientKexInit.ServerHostKeyAlgorithms,  ServerKexInit.ServerHostKeyAlgorithms),
                       CipherClientToServer:       Pick("cipher c2s",         ClientKexInit.EncryptionClientToServer, ServerKexInit.EncryptionClientToServer),
                       CipherServerToClient:       Pick("cipher s2c",         ClientKexInit.EncryptionServerToClient, ServerKexInit.EncryptionServerToClient),
                       MacClientToServer:          PickMac(                   ClientKexInit.MacClientToServer,        ServerKexInit.MacClientToServer),
                       MacServerToClient:          PickMac(                   ClientKexInit.MacServerToClient,        ServerKexInit.MacServerToClient),
                       CompressionClientToServer:  Pick("compression c2s",    ClientKexInit.CompressionClientToServer,ServerKexInit.CompressionClientToServer),
                       CompressionServerToClient:  Pick("compression s2c",    ClientKexInit.CompressionServerToClient,ServerKexInit.CompressionServerToClient),
                       ExtensionInfo:              extInfo,
                       StrictKex:                  strictKex,
                       KexGuess2:                  kexGuess2
                   );

        }

        #endregion

        #region (private) WithoutMarkers(Names)

        // The pseudo-algorithms carried in a key-exchange name-list are signals, never key exchanges.
        private static String[] WithoutMarkers(String[] Names)
            => [.. Names.Where(name => !SshAlgorithmNames.Kex.Markers.Contains(name, StringComparer.Ordinal))];

        #endregion


        #region (private) Pick(Category, ClientList, ServerList)

        // RFC 4253 §7.1: the guessed algorithm is the first on the client's list that the server also has.
        private static String Pick(String Category, String[] ClientList, String[] ServerList)
        {

            foreach (var candidate in ClientList)
                if (Contains(ServerList, candidate))
                    return candidate;

            throw new SshWireException(
                      $"No common {Category} algorithm! Client offered [{String.Join(", ", ClientList)}], " +
                      $"server offered [{String.Join(", ", ServerList)}].");

        }

        #endregion

        #region (private) PickMac(ClientList, ServerList)

        // The MAC is not used with an AEAD cipher; if no MAC is common we still allow the handshake to
        // continue (the caller decides, based on the chosen cipher, whether a MAC is actually needed).
        private static String PickMac(String[] ClientList, String[] ServerList)
        {

            foreach (var candidate in ClientList)
                if (Contains(ServerList, candidate))
                    return candidate;

            return String.Empty;

        }

        #endregion

        #region (private) Contains(List, Value)

        private static Boolean Contains(String[] List, String Value)
        {
            foreach (var item in List)
                if (String.Equals(item, Value, StringComparison.Ordinal))
                    return true;
            return false;
        }

        #endregion

    }

}
