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

using System.Text;
using System.Diagnostics;

using Microsoft.Extensions.Logging;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.TCP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// A TCP client.
    /// </summary>
    public class TCPClient : ATCPClient
    {

        #region Constructor(s)

        #region TCPClient(IPAddress,  TCPPort,           ...)

        public TCPClient(IIPAddress                       IPAddress,
                         IPPort                           TCPPort,
                         I18NString?                      Description              = null,

                         IPVersionPreference?             PreferIPv4               = null,
                         TimeSpan?                        ConnectTimeout           = null,
                         TimeSpan?                        ReceiveTimeout           = null,
                         TimeSpan?                        SendTimeout              = null,
                         TransmissionRetryDelayDelegate?  TransmissionRetryDelay   = null,
                         UInt16?                          MaxNumberOfRetries       = null,
                         UInt32?                          BufferSize               = null,
                         Boolean?                         DisableLogging           = null,
                         // String?                         LoggingPath              = null,
                         // String?                         LoggingContext           = Logger.DefaultContext,
                         // LogfileCreatorDelegate?         LogfileCreator           = null
                         ILogger<TCPClient>?              Logger                   = null,
                         ILoggerFactory?                  LoggerFactory            = null)

            : base(IPAddress,
                   TCPPort,
                   Description,

                   PreferIPv4,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   BufferSize,
                   DisableLogging,
                   Logger,
                   LoggerFactory)

        { }

        #endregion

        #region TCPClient(URL, ...)

        public TCPClient(URL                              URL,
                         I18NString?                      Description              = null,

                         IPVersionPreference?             PreferIPv4               = null,
                         TimeSpan?                        ConnectTimeout           = null,
                         TimeSpan?                        ReceiveTimeout           = null,
                         TimeSpan?                        SendTimeout              = null,
                         TransmissionRetryDelayDelegate?  TransmissionRetryDelay   = null,
                         UInt16?                          MaxNumberOfRetries       = null,
                         UInt32?                          BufferSize               = null,
                         Boolean?                         DisableLogging           = null,
                         // String?                         LoggingPath              = null,
                         // String?                         LoggingContext           = Logger.DefaultContext,
                         // LogfileCreatorDelegate?         LogfileCreator           = null,
                         IDNSClient?                      DNSClient                = null,
                         ILogger<TCPClient>?              Logger                   = null,
                         ILoggerFactory?                  LoggerFactory            = null)

            : base(URL,
                   Description,
                   PreferIPv4,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   BufferSize,
                   DisableLogging,
                   DNSClient,
                   Logger,
                   LoggerFactory)

        { }

        #endregion

        #region TCPClient(DomainName, DNSService,        ...)

        public TCPClient(DomainName                       DomainName,
                         SRV_Spec                         DNSService,
                         I18NString?                      Description              = null,

                         IPVersionPreference?             PreferIPv4               = null,
                         TimeSpan?                        ConnectTimeout           = null,
                         TimeSpan?                        ReceiveTimeout           = null,
                         TimeSpan?                        SendTimeout              = null,
                         TransmissionRetryDelayDelegate?  TransmissionRetryDelay   = null,
                         UInt16?                          MaxNumberOfRetries       = null,
                         UInt32?                          BufferSize               = null,
                         Boolean?                         DisableLogging           = null,
                         // String?                         LoggingPath              = null,
                         // String?                         LoggingContext           = Logger.DefaultContext,
                         // LogfileCreatorDelegate?         LogfileCreator           = null,
                         IDNSClient?                      DNSClient                = null,
                         ILogger<TCPClient>?              Logger                   = null,
                         ILoggerFactory?                  LoggerFactory            = null)

            : base(DomainName,
                   DNSService,
                   Description,
                   PreferIPv4,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   BufferSize,
                   DisableLogging,
                   DNSClient,
                   Logger,
                   LoggerFactory)

        { }

        #endregion

        #endregion


        #region ConnectNew (           TCPPort, ...)

        /// <summary>
        /// Create a new TCP client and connect it to the given TCP port on [::1], the IPv6 loopback address.
        /// </summary>
        /// <param name="TCPPort">The TCP port to connect to.</param>
        /// <param name="Description">An optional description of this TCP client.</param>
        /// <param name="PreferIPv4">An optional IP version preference.</param>
        /// <param name="ConnectTimeout">An optional timeout for the connection attempt.</param>
        /// <param name="ReceiveTimeout">An optional timeout for receiving data.</param>
        /// <param name="SendTimeout">An optional timeout for sending data.</param>
        /// <param name="BufferSize">An optional buffer size for sending and receiving data.</param>
        /// <param name="TransmissionRetryDelay">An optional delegate to calculate the delay between transmission retries.</param>
        /// <param name="MaxNumberOfRetries">An optional maximum number of transmission retries.</param>
        /// <returns>The new TCP client, also when the connect failed: see <see cref="ATCPClient.IsConnected"/>.</returns>
        public static async Task<TCPClient>

            ConnectNew(IPPort                           TCPPort,
                       I18NString?                      Description              = null,

                       IPVersionPreference?             PreferIPv4               = null,
                       TimeSpan?                        ConnectTimeout           = null,
                       TimeSpan?                        ReceiveTimeout           = null,
                       TimeSpan?                        SendTimeout              = null,
                       UInt32?                          BufferSize               = null,
                       TransmissionRetryDelayDelegate?  TransmissionRetryDelay   = null,
                       UInt16?                          MaxNumberOfRetries       = null)

                => await ConnectNew(
                             IPvXAddress.Localhost,
                             TCPPort,
                             Description,

                             PreferIPv4,
                             ConnectTimeout,
                             ReceiveTimeout,
                             SendTimeout,
                             TransmissionRetryDelay,
                             MaxNumberOfRetries,
                             BufferSize
                         );

        #endregion

        #region ConnectNew (IPAddress, TCPPort, ...)

        /// <summary>
        /// Create a new TCP client and connect it to the given IP address and TCP port.
        /// </summary>
        /// <param name="IPAddress">The IP address to connect to.</param>
        /// <param name="TCPPort">The TCP port to connect to.</param>
        /// <param name="Description">An optional description of this TCP client.</param>
        /// <param name="PreferIPv4">An optional IP version preference.</param>
        /// <param name="ConnectTimeout">An optional timeout for the connection attempt.</param>
        /// <param name="ReceiveTimeout">An optional timeout for receiving data.</param>
        /// <param name="SendTimeout">An optional timeout for sending data.</param>
        /// <param name="TransmissionRetryDelay">An optional delegate to calculate the delay between transmission retries.</param>
        /// <param name="MaxNumberOfRetries">An optional maximum number of transmission retries.</param>
        /// <param name="BufferSize">An optional buffer size for sending and receiving data.</param>
        /// <returns>The new TCP client, also when the connect failed: see <see cref="ATCPClient.IsConnected"/>.</returns>
        public static async Task<TCPClient>

            ConnectNew(IIPAddress                       IPAddress,
                       IPPort                           TCPPort,
                       I18NString?                      Description              = null,

                       IPVersionPreference?             PreferIPv4               = null,
                       TimeSpan?                        ConnectTimeout           = null,
                       TimeSpan?                        ReceiveTimeout           = null,
                       TimeSpan?                        SendTimeout              = null,
                       TransmissionRetryDelayDelegate?  TransmissionRetryDelay   = null,
                       UInt16?                          MaxNumberOfRetries       = null,
                       UInt32?                          BufferSize               = null)

        {

            var client = new TCPClient(
                             IPAddress,
                             TCPPort,
                             Description,

                             PreferIPv4,
                             ConnectTimeout,
                             ReceiveTimeout,
                             SendTimeout,
                             TransmissionRetryDelay,
                             MaxNumberOfRetries,
                             BufferSize
                         );

            await client.ConnectAsync();

            return client;

        }

        #endregion


        #region ReconnectAsync()

        public async Task ReconnectAsync()
        {
            await base.ReconnectAsync().ConfigureAwait(false);
        }

        #endregion

        #region ConnectAsync()

        public async Task<TCPConnectionResult> ConnectAsync()
        {

            return await base.ConnectAsync();

        }

        #endregion


        #region SendText   (Text)

        /// <summary>
        /// Send the given text as UTF-8 and read what comes back until the remote closes the connection.
        /// Against a remote that keeps it open, this waits until the client is closed.
        /// </summary>
        /// <param name="Text">The text to send.</param>
        /// <returns>Whether it all went without an error, what came back as UTF-8 text, an error message if not, and the time taken.</returns>
        public async Task<(Boolean, String, String?, TimeSpan)> SendText(String Text)
        {

            var response  = await SendBinary(Encoding.UTF8.GetBytes(Text));
            var text      = Encoding.UTF8.GetString(response.Item2, 0, response.Item2.Length);

            return (response.Item1,
                    text,
                    response.Item3,
                    response.Item4);

        }

        #endregion

        #region SendBinary (Bytes)

        /// <summary>
        /// Send the given bytes and read what comes back until the remote closes the connection.
        /// Against a remote that keeps it open, this waits until the client is closed.
        /// </summary>
        /// <param name="Bytes">The bytes to send.</param>
        /// <returns>Whether it all went without an error, the bytes read, an error message if not, and the time taken.</returns>
        public async Task<(Boolean, Byte[], String?, TimeSpan)> SendBinary(Byte[] Bytes)
        {

            if (!IsConnected || tcpClient is null)
                return (false, Array.Empty<Byte>(), "Client is not connected.", TimeSpan.Zero);

            try
            {

                var stopwatch   = Stopwatch.StartNew();
                var stream      = tcpClient.GetStream();

                // Taken once, and from LiveClient... rather than with ??=: the
                // latter fills in a token source which is missing, never one
                // which has already been cancelled.
                var clientToken = LiveClientCancellationTokenSource.Token;

                // Send the data
                await stream.WriteAsync(Bytes, clientToken).ConfigureAwait(false);
                await stream.FlushAsync(clientToken).ConfigureAwait(false);

                using var responseStream = new MemoryStream();
                var buffer     = new Byte[8192];
                var bytesRead  = 0;

                while ((bytesRead = await stream.ReadAsync(buffer, clientToken).ConfigureAwait(false)) > 0)
                {
                    await responseStream.WriteAsync(buffer.AsMemory(0, bytesRead), clientToken).ConfigureAwait(false);
                }

                stopwatch.Stop();

                return (true, responseStream.ToArray(), null, stopwatch.Elapsed);

            }
            catch (Exception ex)
            {
                await Log($"Error in SendBinary: {ex.Message}");
                return (false, Array.Empty<Byte>(), ex.Message, TimeSpan.Zero);
            }

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override string ToString()

            => $"{nameof(TCPClient)}: {RemoteIPAddress}:{RemotePort} (Connected: {IsConnected})";

        #endregion

    }

}
