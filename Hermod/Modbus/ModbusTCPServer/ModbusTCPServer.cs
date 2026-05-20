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

using System.Security.Authentication;

using Microsoft.Extensions.Logging;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.TCP;
using org.GraphDefined.Vanaheimr.Hermod.Sockets;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Modbus
{

    public delegate Task                               ReadHoldingRegistersRequestEventHandler (DateTimeOffset                 Timestamp,
                                                                                                ModbusTCPServer                ModbusTCPServer,
                                                                                                IPSocket                       RemoteSocket,
                                                                                                String                         ConnectionId,
                                                                                                ReadHoldingRegistersRequest    ReadHoldingRegistersRequest);

    public delegate Task<ReadHoldingRegistersResponse> ReadHoldingRegistersEventHandler        (DateTimeOffset                 Timestamp,
                                                                                                ModbusTCPServer                ModbusTCPServer,
                                                                                                IPSocket                       RemoteSocket,
                                                                                                String                         ConnectionId,
                                                                                                ReadHoldingRegistersRequest    ReadHoldingRegistersRequest);

    public delegate Task                               ReadHoldingRegistersResponseEventHandler(DateTimeOffset                 Timestamp,
                                                                                                ModbusTCPServer                ModbusTCPServer,
                                                                                                IPSocket                       RemoteSocket,
                                                                                                String                         ConnectionId,
                                                                                                ReadHoldingRegistersRequest    ReadHoldingRegistersRequest,
                                                                                                ReadHoldingRegistersResponse   ReadHoldingRegistersResponse);


    public class ModbusTCPServer : ATCPServer
    {

        #region Data

        /// <summary>
        /// The default Modbus/TCP service name.
        /// </summary>
        public  const            String                                         __DefaultServiceName            = "Modbus/TCP Server";

        /// <summary>
        /// The default Modbus/TCP service banner.
        /// </summary>
        public  const            String                                         __DefaultServiceBanner          = "Vanaheimr Hermod Modbus/TCP Server v0.1";

        /// <summary>
        /// The default server thread name.
        /// </summary>
        public  const            String                                         __DefaultServerThreadName       = "Modbus/TCP Server thread on ";

        /// <summary>
        /// The default maximum number of concurrent Modbus/TCP client connections.
        /// </summary>
        public  const            UInt32                                         __DefaultMaxClientConnections   = 100;

        /// <summary>
        /// The default Modbus/TCP client timeout for all incoming client connections.
        /// </summary>
        public  static readonly  TimeSpan                                       __DefaultConnectionTimeout      = TimeSpan.FromSeconds(10);

        private const UInt32 ReadTimeout = 5000U;

        #endregion

        #region Properties

        public new RemoteTLSClientCertificateValidationHandler<ModbusTCPServer>?  ClientCertificateValidator    { get; set; }

        #endregion

        #region Events

        /// <summary>
        /// An event fired whenever the TCP servers instance was started.
        /// </summary>
        //public event StartedEventHandler?                           OnStarted;

        /// <summary>
        /// An event fired whenever a new TCP connection was opened.
        /// If this event closes the TCP connection the OnNotification event will never be fired!
        /// Therefore you can use this event for filtering connection initiation requests.
        /// </summary>
        //public event NewConnectionHandler?                          OnNewConnection;


        public event ReadHoldingRegistersRequestEventHandler?         OnReadHoldingRegistersRequest;

        public event ReadHoldingRegistersEventHandler?                OnReadHoldingRegisters;

        public event ReadHoldingRegistersResponseEventHandler?        OnReadHoldingRegistersResponse;

        /// <summary>
        /// An event fired whenever an exception occurred.
        /// </summary>
        //public event ExceptionOccurredEventHandler?                  OnExceptionOccurred;

        /// <summary>
        /// An event fired whenever a new TCP connection was closed.
        /// </summary>
        //public event ConnectionClosedHandler?                       OnConnectionClosed;

        #endregion

        #region Constructor(s)

        public ModbusTCPServer(IPPort                                                         TCPPort,
                               I18NString?                                                    Description                  = null,

                               ServerCertificateSelectorDelegate?                             ServerCertificateSelector    = null,
                               RemoteTLSClientCertificateValidationHandler<ModbusTCPServer>?  ClientCertificateValidator   = null,
                               LocalCertificateSelectionHandler?                              LocalCertificateSelector     = null,
                               SslProtocols?                                                  AllowedTLSProtocols          = null,
                               Boolean?                                                       ClientCertificateRequired    = null,
                               Boolean?                                                       CheckCertificateRevocation   = null,

                               ServerThreadNameCreatorDelegate?                               ServerThreadNameCreator      = null,
                               ServerThreadPriorityDelegate?                                  ServerThreadPrioritySetter   = null,
                               Boolean?                                                       ServerThreadIsBackground     = null,
                               ConnectionIdBuilder?                                           ConnectionIdBuilder          = null,
                               TimeSpan?                                                      ConnectionTimeout            = null,
                               UInt32?                                                        MaxClientConnections         = null,

                               IDNSClient?                                                    DNSClient                    = null,
                               Boolean                                                        AutoStart                    = false)

            : base(TCPPort:                    TCPPort,
                   ReceiveTimeout:             ConnectionTimeout ?? __DefaultConnectionTimeout,
                   SendTimeout:                ConnectionTimeout ?? __DefaultConnectionTimeout,
                   ServerCertificateSelector:  ServerCertificateSelector,
                   ClientCertificateValidator: null,
                   LocalCertificateSelector:   LocalCertificateSelector,
                   AllowedTLSProtocols:        AllowedTLSProtocols,
                   ClientCertificateRequired:  ClientCertificateRequired,
                   CheckCertificateRevocation: CheckCertificateRevocation,
                   ConnectionIdBuilder:        ConnectionIdBuilder,
                   MaxClientConnections:       MaxClientConnections ?? __DefaultMaxClientConnections,
                   DNSClient:                  DNSClient,
                   Description:                Description,
                   AutoStart:                  false)
        {

            this.ClientCertificateValidator = ClientCertificateValidator;
            base.ClientCertificateValidator = (sender,
                                               certificate,
                                               certificateChain,
                                               tlsServer,
                                               policyErrors) => this.ClientCertificateValidator?.Invoke(
                                                                     sender,
                                                                     certificate,
                                                                     certificateChain,
                                                                     this,
                                                                     policyErrors
                                                                 ) ?? TLSValidationResult.GeneralError();

            if (AutoStart)
                Start().GetAwaiter().GetResult();

        }

        #endregion


        protected override async Task HandleConnection(TCPConnection      Connection,
                                                       CancellationToken  Token)
        {

            Byte Byte;
            var bytesRead     = 0;
            var packet        = new Byte[5000];
            var clientClose   = false;
            var serverClose   = false;

            UInt16 transactionId  = 0;
            UInt16 protocolId     = 0;
            UInt16 packetLength   = 0;
            Byte   unitId         = 0;
            Byte   functionCode   = 0;

            try
            {

                do
                {

                    switch (Connection.TryRead(out Byte, MaxInitialWaitingTimeMS: ReadTimeout))
                    {

                        #region DataAvailable

                        case TCPClientResponse.DataAvailable:

                            packet[bytesRead++] = Byte;

                            if (bytesRead == 2)
                                transactionId  = (UInt16) System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt16(packet, 0));

                            if (bytesRead == 4)
                                protocolId     = (UInt16) System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt16(packet, 2));

                            if (bytesRead == 6)
                                packetLength   = (UInt16) System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt16(packet, 4));

                            if (bytesRead == 7)
                                unitId         = Byte;

                            if (bytesRead == 8)
                                functionCode   = Byte;

                            if (bytesRead == 6 + packetLength)
                            {

                                switch (functionCode)
                                {

                                    case 0x03:

                                        var startingAddress    = (UInt16) (System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt16(packet, 8)) + 1);
                                        var numberOfregisters  = (UInt16) System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt16(packet, 10));

                                        var rhrr = new ReadHoldingRegistersRequest(null,
                                                                                   transactionId,
                                                                                   startingAddress,
                                                                                   numberOfregisters,
                                                                                   unitId,
                                                                                   protocolId);

                                        if (OnReadHoldingRegistersRequest is not null)
                                            await OnReadHoldingRegistersRequest.Invoke(Timestamp.Now,
                                                                                       this,
                                                                                       Connection.RemoteSocket,
                                                                                       Connection.ConnectionId,
                                                                                       rhrr);

                                        ReadHoldingRegistersResponse? resp = null;

                                        if (OnReadHoldingRegisters is not null)
                                            resp = await OnReadHoldingRegisters.Invoke(Timestamp.Now,
                                                                                       this,
                                                                                       Connection.RemoteSocket,
                                                                                       Connection.ConnectionId,
                                                                                       rhrr);

                                        resp ??= new ReadHoldingRegistersResponse(rhrr,
                                                                                  new Byte[9] {

                                                                                      // Transaction identification
                                                                                      packet[0], packet[1],

                                                                                      // Protocol identification (always zero)
                                                                                      packet[2], packet[3],

                                                                                      // Length of frame
                                                                                      0x00, 0x03,

                                                                                      // Unit address
                                                                                      packet[6],

                                                                                      // Function code
                                                                                      packet[7],

                                                                                      0x00

                                                                                  });

                                        Connection.WriteToResponseStream(resp.EntirePDU);
                                        Connection.Flush();

                                        if (OnReadHoldingRegistersResponse is not null)
                                            await OnReadHoldingRegistersResponse.Invoke(Timestamp.Now,
                                                                                        this,
                                                                                        Connection.RemoteSocket,
                                                                                        Connection.ConnectionId,
                                                                                        rhrr,
                                                                                        resp);

                                        break;

                                }

                            }

                            break;

                        #endregion

                        #region CanNotRead

                        case TCPClientResponse.CanNotRead:
                            logger.LogDebug("Modbus/TCP server closes connection {ConnectionId}.", Connection.ConnectionId);
                            serverClose = true;
                            break;

                        #endregion

                        #region ClientClose

                        case TCPClientResponse.ClientClose:
                            clientClose = true;
                            break;

                        #endregion

                        #region Timeout

                        case TCPClientResponse.Timeout:
                            serverClose = true;
                            break;

                        #endregion

                    }

                } while (!clientClose && !serverClose);

            }

            #region Process exceptions

            catch (IOException ioe)
            {

                if      (ioe.Message.StartsWith("Unable to read data from the transport connection")) { }
                else if (ioe.Message.StartsWith("Unable to write data to the transport connection")) { }

                else
                    logger.LogWarning(ioe, "Modbus/TCP server IO exception.");

            }

            catch (Exception e)
            {
                logger.LogError(e, "Modbus/TCP server exception.");
            }

            #endregion

            #region Close the TCP connection

            try
            {

                Connection?.Close(clientClose
                                      ? ConnectionClosedBy.Client
                                      : ConnectionClosedBy.Server);

            }
            catch (Exception e)
            {
                logger.LogDebug(e, "Modbus/TCP server exception when closing the TCP connection.");
            }

            #endregion

        }


        #region Shutdown(EventTrackingId = null, Message = null, Wait = true)

        /// <summary>
        /// Shutdown the TCP listener.
        /// </summary>
        /// <param name="EventTrackingId">An unique event tracking identification for correlating this request with other events.</param>
        /// <param name="Wait">Wait until the server finally shutted down.</param>
        /// <param name="Message">An optional shutdown message.</param>
        public async Task<Boolean> Shutdown(EventTracking_Id?  EventTrackingId   = null,
                                            String?            Message           = null,
                                            Boolean            Wait              = true)
        {
            await Stop(EventTrackingId);
            return true;
        }

        #endregion

        #region StopAndWait()

        ///// <summary>
        ///// Stop the TCPServer and wait until all connections are closed.
        ///// </summary>
        //public Task<Boolean> StopAndWait()

        //    => TCPServer.StopAndWait();

        #endregion


    }

}
