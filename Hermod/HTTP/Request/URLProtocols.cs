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

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Extension methods for URL protocols.
    /// </summary>
    public static class URLProtocolsExtensions
    {

        /// <summary>
        /// Return a string representation of the given URL protocol.
        /// </summary>
        /// <param name="URLProtocol">An URL protocol.</param>
        public static String AsString(this URLProtocols URLProtocol)

            => URLProtocol switch {
                   URLProtocols.http     => "http://",
                   URLProtocols.https    => "https://",
                   URLProtocols.ws       => "ws://",
                   URLProtocols.wss      => "wss://",
                   URLProtocols.modbus   => "modbus://",
                   URLProtocols.smodbus  => "smodbus://",
                   _                     => "https://",
               };


        /// <summary>
        /// Whether the URL protocol enforces TLS, or not.
        /// </summary>
        /// <param name="URLProtocol">An URL protocol.</param>
        public static Boolean EnforcesTLS(this URLProtocols URLProtocol)

            => URLProtocol switch {
                   URLProtocols.tcp      => false,
                   URLProtocols.udp      => false,
                   URLProtocols.tls      => true,
                   URLProtocols.http     => false,
                   URLProtocols.https    => true,
                   URLProtocols.ws       => false,
                   URLProtocols.wss      => true,
                   URLProtocols.modbus   => false,
                   URLProtocols.smodbus  => true,
                   _                     => false
               };


        /// <summary>
        /// Whether the URL protocol enforces TLS, or not.
        /// </summary>
        /// <param name="URLProtocol">An URL protocol.</param>
        public static IPPort? DefaultPorts(this URLProtocols URLProtocol)

            => URLProtocol switch {
                   URLProtocols.http     => IPPort.HTTP,
                   URLProtocols.https    => IPPort.HTTPS,
                   URLProtocols.ws       => IPPort.HTTP,
                   URLProtocols.wss      => IPPort.HTTPS,
                   URLProtocols.modbus   => IPPort.ModbusTCP,
                   URLProtocols.smodbus  => IPPort.ModbusTLS,
                   _                     => null
               };

    }


    /// <summary>
    /// Well-known protocols.
    /// </summary>
    public enum URLProtocols
    {

        /// <summary>
        /// Transmission Control Protocol (TCP)
        /// </summary>
        tcp,

        /// <summary>
        /// Transport Layer Security (TLS)
        /// </summary>
        tls,

        /// <summary>
        /// Hypertext Transfer Protocol (HTTP)
        /// </summary>
        http,

        /// <summary>
        /// Hypertext Transfer Protocol Secure (HTTPS)
        /// </summary>
        https,

        /// <summary>
        /// WebSocket Protocol (WS)
        /// </summary>
        ws,

        /// <summary>
        /// WebSocket Secure Protocol (WSS)
        /// </summary>
        wss,

        /// <summary>
        /// User Datagram Protocol (UDP)
        /// </summary>
        udp,

        /// <summary>
        /// Modbus/TCP
        /// </summary>
        modbus,

        /// <summary>
        /// Modbus/TLS (Modbus/TCP Security Protocol Specification)
        /// </summary>
        smodbus

    }

}
