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

using System.Security.Authentication;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Styx.Arrows;
using org.GraphDefined.Vanaheimr.Hermod.Sockets;
using org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP;
using org.GraphDefined.Vanaheimr.Hermod.Mail;
using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    public class MAIL_FROM_FilterResponse
    {

        public readonly Boolean  Forward;
        public readonly String   Description;

        public MAIL_FROM_FilterResponse(Boolean  Forward,
                                        String   Description)
        {

            this.Forward      = Forward;
            this.Description  = Description;

        }

        public static MAIL_FROM_FilterResponse Allowed
        {
            get
            {
                return new MAIL_FROM_FilterResponse(true, "Sender ok");
            }
        }

        public static MAIL_FROM_FilterResponse Denied
        {
            get
            {
                return new MAIL_FROM_FilterResponse(false, "Access denied");
            }
        }

    }

    public class RCPT_TO_FilterResponse
    {

        public readonly Boolean Forward;
        public readonly String Description;

        public RCPT_TO_FilterResponse(Boolean Forward,
                                      String Description)
        {

            this.Forward = Forward;
            this.Description = Description;

        }

        public static RCPT_TO_FilterResponse Allowed
        {
            get
            {
                return new RCPT_TO_FilterResponse(true, "Recipient ok");
            }
        }

        public static RCPT_TO_FilterResponse Denied
        {
            get
            {
                return new RCPT_TO_FilterResponse(false, "Access denied");
            }
        }

        public static RCPT_TO_FilterResponse RelayDenied
        {
            get
            {
                return new RCPT_TO_FilterResponse(false, "Relay access denied");
            }
        }

    }


    public delegate void NewSMTPConnectionHandler     (SMTPServer SMTPServer, DateTimeOffset Timestamp, IPSocket RemoteSocket, TCPConnection TCPConnection);
    public delegate void IncomingEMailEnvelopeHandler (SMTPServer SMTPServer, IEnumerable<String> MAIL_FROM, IEnumerable<String> RCPT_TO);


    /// <summary>
    /// A SMTP server.
    /// </summary>
    public class SMTPServer : ATCPServers,
                              IArrowSender<SMTPServer, EMailEnvelop>
    {

        #region Data

        internal const    String             __DefaultServerName  = "Vanaheimr Hermod SMTP Service v0.1";

        private readonly  SMTPConnection     _SMTPConnection;

        #endregion

        #region Properties

        #region DefaultServerName

        private String _DefaultServerName;

        /// <summary>
        /// The default SMTP server name.
        /// </summary>
        public String DefaultServerName
        {

            get
            {
                return _DefaultServerName;
            }

            set
            {
                if (value.IsNotNullOrEmpty())
                    _DefaultServerName = value;
            }

        }

        #endregion

        public Boolean AllowStartTLS { get; }

        #endregion

        #region Events

        public event NewSMTPConnectionHandler                            OnNewConnection;

        public delegate MAIL_FROM_FilterResponse MAIL_FROM_FilterHandler(SMTPServer SMTPServer, String MAIL_FROM);
        public delegate RCPT_TO_FilterResponse   RCPT_TO_FilterHandler  (SMTPServer SMTPServer, String RCPT_TO);

        public event MAIL_FROM_FilterHandler      MAIL_FROMFilter;
        public event RCPT_TO_FilterHandler        RCPT_TOFilter;
        public event IncomingEMailEnvelopeHandler OnIncomingEMailEnvelope;

        public event NotificationEventHandler<SMTPServer, EMailEnvelop>  OnNotification;

        /// <summary>
        /// An event called whenever a request resulted in an error.
        /// </summary>
        public event SMTPErrorLogHandler                                     ErrorLog;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Initialize the SMTP server using the given parameters.
        /// </summary>
        /// <param name="TCPPort"></param>
        /// <param name="DefaultServerName">The default SMTP server name.</param>
        /// <param name="ServiceName">The TCP service name shown e.g. on service startup.</param>
        /// <param name="ServerCertificateSelector">An optional delegate to select a TLS server certificate.</param>
        /// <param name="ClientCertificateValidator">An optional delegate to verify the TLS client certificate used for authentication.</param>
        /// <param name="LocalCertificateSelector">An optional delegate to select the TLS client certificate used for authentication.</param>
        /// <param name="AllowedTLSProtocols">The TLS protocol(s) allowed for this connection.</param>
        /// <param name="AllowStartTLS">Allow to start TLS via the 'STARTTLS' SMTP command.</param>
        /// <param name="ServerThreadName">The optional name of the TCP server thread.</param>
        /// <param name="ServerThreadPriority">The optional priority of the TCP server thread.</param>
        /// <param name="ServerThreadIsBackground">Whether the TCP server thread is a background thread or not.</param>
        /// <param name="ConnectionIdBuilder">An optional delegate to build a connection identification based on IP socket information.</param>
        /// <param name="ConnectionTimeout">The TCP client timeout for all incoming client connections in seconds (default: 30 sec).</param>
        /// <param name="MaxClientConnections">The maximum number of concurrent TCP client connections (default: 4096).</param>
        /// <param name="DNSClient">The DNS client to use.</param>
        /// <param name="AutoStart">Start the SMTP server thread immediately (default: no).</param>
        public SMTPServer(IPPort?                                                   TCPPort                      = null,
                          String                                                    DefaultServerName            = __DefaultServerName,
                          String?                                                   ServiceName                  = null,
                          Boolean?                                                  AllowStartTLS                = true,

                          ServerCertificateSelectorDelegate?                        ServerCertificateSelector    = null,
                          RemoteTLSClientCertificateValidationHandler<SMTPServer>?  ClientCertificateValidator   = null,
                          LocalCertificateSelectionHandler?                         LocalCertificateSelector     = null,
                          SslProtocols?                                             AllowedTLSProtocols          = null,
                          Boolean?                                                  ClientCertificateRequired    = null,
                          Boolean?                                                  CheckCertificateRevocation   = null,

                          ServerThreadNameCreatorDelegate?                          ServerThreadNameCreator      = null,
                          ServerThreadPriorityDelegate?                             ServerThreadPrioritySetter   = null,
                          Boolean?                                                  ServerThreadIsBackground     = null,
                          ConnectionIdBuilder?                                      ConnectionIdBuilder          = null,
                          TimeSpan?                                                 ConnectionTimeout            = null,
                          UInt32?                                                   MaxClientConnections         = null,

                          DNSClient?                                                DNSClient                    = null,
                          Boolean                                                   AutoStart                    = false)

            : base(ServiceName,
                   DefaultServerName,

                   ServerCertificateSelector,
                   null,
                   LocalCertificateSelector,
                   AllowedTLSProtocols,
                   ClientCertificateRequired,
                   CheckCertificateRevocation,

                   ServerThreadNameCreator,
                   ServerThreadPrioritySetter,
                   ServerThreadIsBackground,
                   ConnectionIdBuilder,
                   ConnectionTimeout,
                   MaxClientConnections,

                   DNSClient,
                   false)

        {

            this._DefaultServerName         = DefaultServerName;
            this.AllowStartTLS              = AllowStartTLS ?? (ServerCertificateSelector != null);

            _SMTPConnection                 = new SMTPConnection(
                                                  DefaultServerName,
                                                  this.AllowStartTLS
                                              );

            _SMTPConnection.MAIL_FROMFilter += Process_MAIL_FROMFilter;
            _SMTPConnection.RCPT_TOFilter   += Process_RCPT_TOFilter;
            _SMTPConnection.OnNotification  += ProcessNotification;
            _SMTPConnection.ErrorLog        += (HTTPProcessor, ServerTimestamp, SMTPCommand, Request, Response, Error, LastException) =>
                                                     LogError (ServerTimestamp, SMTPCommand, Request, Response, Error, LastException);

            if (TCPPort is not null)
                this.AttachTCPPort(TCPPort ?? IPPort.SMTP);

            if (AutoStart)
                Start();

        }

        #endregion


        // Manage the underlying TCP sockets...

        #region AttachTCPPort(Port)

        public SMTPServer AttachTCPPort(IPPort Port)
        {

            this.AttachTCPPorts(Port);

            return this;

        }

        #endregion

        #region AttachTCPPorts(params Ports)

        public async Task<SMTPServer> AttachTCPPorts(params IPPort[] Ports)
        {

            await base.AttachTCPPorts(
                      tcpServer => {
                          tcpServer.OnNewConnection += ProcessTCPServerOnNewConnection;
                          tcpServer.SendTo(_SMTPConnection);
                      },
                      Ports
                  );

            return this;

        }

        #endregion

        #region AttachTCPSocket(Socket)

        public Task<SMTPServer> AttachTCPSocket(IPSocket Socket)

            => AttachTCPSockets(Socket);

        #endregion

        #region AttachTCPSockets(params Sockets)

        public async Task<SMTPServer> AttachTCPSockets(params IPSocket[] Sockets)
        {

            await base.AttachTCPSockets(
                      tcpServer => {
                          tcpServer.OnNewConnection += ProcessTCPServerOnNewConnection;
                          tcpServer.SendTo(_SMTPConnection);
                      },
                      Sockets
                  );

            return this;

        }

        #endregion


        #region DetachTCPPort(Port)

        public Task<SMTPServer> DetachTCPPort(IPPort Port)

            => DetachTCPPorts(Port);

        #endregion

        #region DetachTCPPorts(params Sockets)

        public async Task<SMTPServer> DetachTCPPorts(params IPPort[] Ports)
        {

            await base.DetachTCPPorts(
                      tcpServer => {
                          tcpServer.OnNotification      -= _SMTPConnection.ProcessArrow;
                          tcpServer.OnExceptionOccurred  -= _SMTPConnection.ProcessExceptionOccurred;
                          tcpServer.OnCompleted         -= _SMTPConnection.ProcessCompleted;
                      },
                      Ports
                  );

            return this;

        }

        #endregion


        // Events

        private Task ProcessTCPServerOnNewConnection(ITCPServer        TCPServer,
                                                     DateTimeOffset    Timestamp,
                                                     EventTracking_Id  EventTrackingId,
                                                     IPSocket          RemoteSocket,
                                                     String            ConnectionId,
                                                     TCPConnection     TCPConnection)
        {

            OnNewConnection?.Invoke(this, Timestamp, RemoteSocket, TCPConnection);

            return Task.CompletedTask;

        }

        private MAIL_FROM_FilterResponse Process_MAIL_FROMFilter(String MAIL_FROM)
        {

            var MAIL_FROMFilterLocal = MAIL_FROMFilter;
            if (MAIL_FROMFilterLocal != null)
                return MAIL_FROMFilterLocal(this, MAIL_FROM);

            return null;

        }

        private RCPT_TO_FilterResponse Process_RCPT_TOFilter(String RCPT_TO)
        {

            var RCPT_TOFilterLocal = RCPT_TOFilter;
            if (RCPT_TOFilterLocal != null)
                return RCPT_TOFilterLocal(this, RCPT_TO);

            return null;

        }

        private void ProcessNotification(EventTracking_Id EventTrackingId, EMailEnvelop MailEnvelop)
        {
            OnNotification?.Invoke(EventTrackingId, this, MailEnvelop);
        }




        // SMTP Logging...

        #region LogError(ServerTimestamp, EMail, Response, Error = null, LastException = null)

        /// <summary>
        /// Log an error during request processing.
        /// </summary>
        /// <param name="ServerTimestamp">The timestamp of the incoming request.</param>
        /// <param name="EMail">The incoming request.</param>
        /// <param name="SMTPCommand">The SMTP command.</param>
        /// <param name="Response">The outgoing response.</param>
        /// <param name="Error">The occured error.</param>
        /// <param name="LastException">The last occured exception.</param>
        public void LogError(DateTimeOffset        ServerTimestamp,
                             String                SMTPCommand,
                             EMail                 EMail,
                             SMTPExtendedResponse  Response,
                             String?               Error           = null,
                             Exception?            LastException   = null)
        {
            ErrorLog?.Invoke(this, ServerTimestamp, SMTPCommand, EMail, Response, Error, LastException);
        }

        #endregion


    }

}
