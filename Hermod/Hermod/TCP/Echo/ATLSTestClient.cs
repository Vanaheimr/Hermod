/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Illias;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// A simple TLS echo test client that can connect to a TCP echo server,
    /// </summary>
    public abstract class ATLSTestClient : ATCPTestClient
    {

        #region Data

        protected SslStream tlsStream;

        protected RemoteTLSServerCertificateValidationHandler<ATLSTestClient>? RemoteCertificateValidationHandler;

        #endregion

        #region Properties


        #endregion

        #region Constructor(s)

        #region (protected)   ATLSTestClient(...)

        //protected ATLSTestClient(RemoteTLSServerCertificateValidationHandler<ATLSTestClient>?  RemoteCertificateValidationHandler   = null,
        //                         TimeSpan?                                                     ConnectTimeout                       = null,
        //                         TimeSpan?                                                     ReceiveTimeout                       = null,
        //                         TimeSpan?                                                     SendTimeout                          = null,
        //                         UInt32?                                                       BufferSize                           = null,
        //                         TCPEchoLoggingDelegate?                                       LoggingHandler                       = null,
        //                         DNSClient?                                                    DNSClient                            = null)

        //    : base(ConnectTimeout,
        //           ReceiveTimeout,
        //           SendTimeout,
        //           BufferSize,
        //           LoggingHandler,
        //           DNSClient)

        //{

        //    this.RemoteCertificateValidationHandler = RemoteCertificateValidationHandler;

        //}

        #endregion

        #region (protected) ATLSTestClient(IPAddress, TCPPort, ...)

        protected ATLSTestClient(IIPAddress                                                    IPAddress,
                                 IPPort                                                        TCPPort,
                                 RemoteTLSServerCertificateValidationHandler<ATLSTestClient>?  RemoteCertificateValidationHandler   = null,
                                 TimeSpan?                                                     ConnectTimeout                       = null,
                                 TimeSpan?                                                     ReceiveTimeout                       = null,
                                 TimeSpan?                                                     SendTimeout                          = null,
                                 UInt32?                                                       BufferSize                           = null,
                                 TCPEchoLoggingDelegate?                                       LoggingHandler                       = null)

            : base(IPAddress,
                   TCPPort,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   BufferSize,
                   LoggingHandler)

        {

            this.RemoteCertificateValidationHandler = RemoteCertificateValidationHandler;

        }

        #endregion

        #region (protected) ATLSTestClient(URL,     DNSService = null, ..., DNSClient = null)

        protected ATLSTestClient(URL                                                           URL,
                                 SRV_Spec?                                                     DNSService                           = null,
                                 RemoteTLSServerCertificateValidationHandler<ATLSTestClient>?  RemoteCertificateValidationHandler   = null,
                                 TimeSpan?                                                     ConnectTimeout                       = null,
                                 TimeSpan?                                                     ReceiveTimeout                       = null,
                                 TimeSpan?                                                     SendTimeout                          = null,
                                 UInt32?                                                       BufferSize                           = null,
                                 TCPEchoLoggingDelegate?                                       LoggingHandler                       = null,
                                 DNSClient?                                                    DNSClient                            = null)

            : base(URL,
                   DNSService,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   BufferSize,
                   LoggingHandler,
                   DNSClient)

        {

            this.RemoteCertificateValidationHandler = RemoteCertificateValidationHandler;

        }

        #endregion

        #region (protected) ATLSTestClient(DomainName, DNSService,        ..., DNSClient = null)

        protected ATLSTestClient(DomainName                                                    DomainName,
                                 SRV_Spec                                                      DNSService,
                                 RemoteTLSServerCertificateValidationHandler<ATLSTestClient>?  RemoteCertificateValidationHandler   = null,
                                 TimeSpan?                                                     ConnectTimeout                       = null,
                                 TimeSpan?                                                     ReceiveTimeout                       = null,
                                 TimeSpan?                                                     SendTimeout                          = null,
                                 UInt32?                                                       BufferSize                           = null,
                                 TCPEchoLoggingDelegate?                                       LoggingHandler                       = null,
                                 DNSClient?                                                    DNSClient                            = null)

            : base(DomainName,
                   DNSService,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   BufferSize,
                   LoggingHandler,
                   DNSClient)

        {

            this.RemoteCertificateValidationHandler = RemoteCertificateValidationHandler;

        }

        #endregion

        #endregion


        #region ReconnectAsync(CancellationToken = default)

        public override async Task reconnectAsync(CancellationToken CancellationToken = default)
        {

            await base.reconnectAsync(CancellationToken);

            // Do TLS stuff!

        }

        #endregion

        #region (protected) ConnectAsync(CancellationToken = default)

        protected override async Task connectAsync(CancellationToken CancellationToken = default)
        {

            await base.connectAsync();

            if (tcpClient is not null)
            {

                var tcpStream = tcpClient.GetStream();

                if (tcpStream is not null)
                {

                    tlsStream = new SslStream(
                                    tcpStream,
                                    leaveInnerStreamOpen: false
                                );

                    var authenticationOptions = new SslClientAuthenticationOptions {
                        //ApplicationProtocols = new List<SslApplicationProtocol> {
                        //    SslApplicationProtocol.Http2, // Example: Add HTTP/2 protocol
                        //    SslApplicationProtocol.Http11  // Example: Add HTTP/1.1 protocol
                        //},
                        AllowRenegotiation = true, // Allow renegotiation if needed
                        AllowTlsResume = true, // Allow TLS resumption if needed
                        LocalCertificateSelectionCallback = null,
                       // TargetHost = RemoteIPAddress.ToString(),  //SNI!
                        ClientCertificates = null,
                        ClientCertificateContext = null,
                        CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                        EncryptionPolicy = EncryptionPolicy.RequireEncryption,
                        EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13, // Specify the TLS versions you want to support
                        CipherSuitesPolicy = null, // Use default cipher suites policy
                        CertificateChainPolicy = null // Use default certificate chain policy
                    };

                    if (RemoteCertificateValidationHandler is not null)
                    {
                        authenticationOptions.RemoteCertificateValidationCallback = (sender, certificate, chain, policyErrors) => {

                            var result = RemoteCertificateValidationHandler(
                                             sender,
                                             certificate is not null
                                                 ? new X509Certificate2(certificate)
                                                 : null,
                                             chain,
                                             this,
                                             policyErrors
                                         );

                            return result.Item1;

                        };
                    }
                    else
                    {
                        authenticationOptions.RemoteCertificateValidationCallback = (sender, certificate, chain, policyErrors) => {
                            return true;
                        };
                    }

                    try
                    {
                        await tlsStream.AuthenticateAsClientAsync(
                                  authenticationOptions,
                                  CancellationToken
                              );
                    }
                    catch (Exception e)
                    {
                        DebugX.Log($"Error during TLS authentication: {e.Message}");
                    }

                }

            }

        }

        #endregion


        #region (protected) SendText   (Text)

        /// <summary>
        /// Send the given message to the echo server and receive the echoed response.
        /// </summary>
        /// <param name="Text">The text message to send and echo.</param>
        /// <returns>Whether the echo was successful, the echoed response, an optional error response, and the time taken to send and receive it.</returns>
        protected async Task<(Boolean, String, String?, TimeSpan)> SendText(String Text)
        {

            var response  = await SendBinary(Encoding.UTF8.GetBytes(Text));
            var text      = Encoding.UTF8.GetString(response.Item2, 0, response.Item2.Length);

            return (response.Item1,
                    text,
                    response.Item3,
                    response.Item4);

        }

        #endregion

        #region (protected) SendBinary (Bytes)

        /// <summary>
        /// Send the given bytes to the echo server and receive the echoed response.
        /// </summary>
        /// <param name="Bytes">The bytes to send and echo.</param>
        /// <returns>Whether the echo was successful, the echoed response, an optional error response, and the time taken to send and receive it.</returns>
        protected async Task<(Boolean, Byte[], String?, TimeSpan)> SendBinary(Byte[] Bytes)
        {

            if (!IsConnected || tcpClient is null)
                return (false, Array.Empty<Byte>(), "Client is not connected.", TimeSpan.Zero);

            try
            {

                var stopwatch   = Stopwatch.StartNew();
                var stream      = tcpClient.GetStream();
                cts           ??= new CancellationTokenSource();

                // Send the data
                await stream.WriteAsync(Bytes, cts.Token).ConfigureAwait(false);
                await stream.FlushAsync(cts.Token).ConfigureAwait(false);

                using var responseStream = new MemoryStream();
                var buffer     = new Byte[8192];
                var bytesRead  = 0;

                while ((bytesRead = await stream.ReadAsync(buffer, cts.Token).ConfigureAwait(false)) > 0)
                {
                    await responseStream.WriteAsync(buffer.AsMemory(0, bytesRead), cts.Token).ConfigureAwait(false);
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


        #region (protected) Log        (Message)

        protected Task Log(String Message)
        {

            if (loggingHandler is not null)
            {
                try
                {
                    return loggingHandler(Message);
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"Error in logging handler: {e.Message}");
                }
            }

            return Task.CompletedTask;

        }

        #endregion


        #region Close()

        /// <summary>
        /// Close the TCP connection to the echo server.
        /// </summary>
        public async Task Close()
        {

            if (IsConnected)
            {
                try
                {
                    tcpClient.Client.Shutdown(SocketShutdown.Both);
                }
                catch { }
                tcpClient.Close();
                await Log("Client closed!");
            }

            cts.Cancel();

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"{nameof(ATLSTestClient)}: {RemoteIPAddress}:{RemoteTCPPort} (Connected: {IsConnected})";

        #endregion


        #region Dispose / IAsyncDisposable

        public async ValueTask DisposeAsync()
        {
            await Close();
            cts?.Dispose();
            GC.SuppressFinalize(this);
        }

        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
            GC.SuppressFinalize(this);
        }

        #endregion

    }

}
