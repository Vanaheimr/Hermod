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

using org.GraphDefined.Vanaheimr.Illias;

namespace org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP
{

    public interface ITCPServer
    {

        #region Properties

        /// <summary>
        /// Gets the IPAddress on which the TCP server listens.
        /// </summary>
        public IIPAddress                        IPAddress                              { get; }

        /// <summary>
        /// Gets the port on which the TCP server listens.
        /// </summary>
        public IPPort                            TCPPort                                { get; }

        /// <summary>
        /// Gets the IP socket on which the TCP server listens.
        /// </summary>
        public IPSocket                          IPSocket                               { get; }

        /// <summary>
        /// The optional TLS certificate.
        /// </summary>
        //public X509Certificate2                  ServerCertificate                      { get; }

        /// <summary>
        /// Whether TLS client certification is required.
        /// </summary>
        public Boolean                           ClientCertificateRequired              { get; }

        /// <summary>
        /// Whether TLS client certificate revocation should be verified.
        /// </summary>
        public Boolean                           CheckCertificateRevocation             { get; }


        /// <summary>
        /// A delegate to build a connection identification based on IP socket information.
        /// </summary>
        public ConnectionIdBuilder               ConnectionIdBuilder                    { get; }

        /// <summary>
        /// The TCP client timeout for all incoming client connections.
        /// </summary>
        public TimeSpan                          ConnectionTimeout                      { get; set; }

        /// <summary>
        /// The maximum number of concurrent TCP client connections (default: 4096).
        /// </summary>
        public UInt32                            MaxClientConnections                   { get; set; }

        /// <summary>
        /// The current number of connected clients
        /// </summary>
        public UInt32                            NumberOfConnectedClients               { get; }


        /// <summary>
        /// True while the TCPServer is listening for new clients
        /// </summary>
        public Boolean                           IsRunning                 { get; }

        #endregion

        #region Events

        ///// <summary>
        ///// An event fired whenever the TCP servers instance was started.
        ///// </summary>
        //public event StartedEventHandler?                        OnStarted;

        ///// <summary>
        ///// An event fired whenever a new TCP connection was opened.
        ///// If this event closes the TCP connection the OnNotification event will never be fired!
        ///// Therefore you can use this event for filtering connection initiation requests.
        ///// </summary>
        //public event NewConnectionHandler?                       OnNewConnection;

        ///// <summary>
        ///// An event fired whenever a new TCP connection was opened.
        ///// </summary>
        //public event NotificationEventHandler<TCPConnection>?    OnNotification;

        ///// <summary>
        ///// An event fired whenever an exception occured.
        ///// </summary>
        //public event ExceptionOccurredEventHandler?               OnExceptionOccurred;

        ///// <summary>
        ///// An event fired whenever a new TCP connection was closed.
        ///// </summary>
        //public event ConnectionClosedHandler?                    OnConnectionClosed;

        ///// <summary>
        ///// An event fired whenever the TCP servers instance was stopped.
        ///// </summary>
        //public event CompletedEventHandler?                      OnCompleted;

        #endregion


        public Task SendConnectionClosed(DateTimeOffset      ServerTimestamp,
                                         EventTracking_Id    EventTrackingId,
                                         IPSocket            RemoteSocket,
                                         String              ConnectionId,
                                         ConnectionClosedBy  ClosedBy);


    }

}
