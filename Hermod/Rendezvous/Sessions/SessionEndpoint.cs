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

using System.Net;
using System.Net.Sockets;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Rendezvous
{

    /// <summary>
    /// One TCP listener of a rendezvous, waiting for exactly one client.
    /// Additional connection attempts on the same port are rejected, but the
    /// listener stays open until the rendezvous is closed, so that nobody else
    /// can take over the port in the meantime.
    /// </summary>
    public sealed class SessionEndpoint : IDisposable
    {

        #region Data

        private readonly TcpListener  listener;
        private          Socket?      client;
        private          Int32        adopted;

        #endregion

        #region Properties

        /// <summary>
        /// The requested TCP port: a fixed port number or '?'.
        /// </summary>
        public PortSpecification  RequestedPort     { get; }

        /// <summary>
        /// The TCP port this endpoint is actually listening on.
        /// </summary>
        public IPPort             Port              { get; }

        /// <summary>
        /// The connected client, or null.
        /// </summary>
        public Socket?            Client
            => Volatile.Read(ref client);

        /// <summary>
        /// Whether a client is connected.
        /// </summary>
        public Boolean            IsConnected
            => Volatile.Read(ref client) is not null;

        /// <summary>
        /// The remote TCP socket of the connected client, or null.
        /// </summary>
        public IPSocket?          RemoteSocket      { get; private set; }

        #endregion

        #region Constructor(s)

        private SessionEndpoint(TcpListener        Listener,
                                PortSpecification  RequestedPort)
        {

            this.listener       = Listener;
            this.RequestedPort  = RequestedPort;
            this.Port           = IPPort.Parse(((IPEndPoint) Listener.LocalEndpoint).Port);

        }

        #endregion


        #region (static) Bind(Address, Port, RequestedPort, Backlog)

        /// <summary>
        /// Start a new TCP listener on the given IP address and TCP port.
        /// A port of zero asks the operating system for a free port.
        /// </summary>
        /// <param name="Address">The IP address to bind to.</param>
        /// <param name="Port">The TCP port to bind to, or zero.</param>
        /// <param name="RequestedPort">The originally requested port specification.</param>
        /// <param name="Backlog">The TCP listen backlog.</param>
        /// <exception cref="SocketException">When the TCP port could not be bound.</exception>
        internal static SessionEndpoint Bind(IIPAddress         Address,
                                             IPPort             Port,
                                             PortSpecification  RequestedPort,
                                             Int32              Backlog)
        {

            var listener = new TcpListener(Address.ToDotNet(), Port.ToUInt16());

            try
            {

                // Do not allow other processes to bind the same port via SO_REUSEADDR.
                if (OperatingSystem.IsWindows())
                    listener.ExclusiveAddressUse = true;

                listener.Start(Backlog);

            }
            catch
            {
                listener.Dispose();
                throw;
            }

            return new SessionEndpoint(listener, RequestedPort);

        }

        #endregion

        #region AcceptAsync(CancellationToken)

        /// <summary>
        /// Wait for the next incoming TCP connection.
        /// </summary>
        /// <param name="CancellationToken">A token to cancel waiting.</param>
        internal ValueTask<Socket> AcceptAsync(CancellationToken CancellationToken)

            => listener.AcceptSocketAsync(CancellationToken);

        #endregion

        #region TryAdopt(Socket)

        /// <summary>
        /// Try to accept the given socket as *the* client of this endpoint.
        /// Returns false when this endpoint already has a client.
        /// </summary>
        /// <param name="Socket">A freshly accepted socket.</param>
        internal Boolean TryAdopt(Socket Socket)
        {

            if (Interlocked.CompareExchange(ref adopted, 1, 0) != 0)
                return false;

            RemoteSocket = IPSocket.FromIPEndPoint(Socket.RemoteEndPoint);
            Volatile.Write(ref client, Socket);

            return true;

        }

        #endregion

        #region StopListening()

        /// <summary>
        /// Stop accepting new connections, but keep a connected client.
        /// </summary>
        internal void StopListening()
        {
            try
            {
                listener.Stop();
            }
            catch (SocketException)
            { }
            catch (ObjectDisposedException)
            { }
        }

        #endregion

        #region CloseClient()

        /// <summary>
        /// Close the connected client, if any.
        /// </summary>
        internal void CloseClient()
        {

            var socket = Interlocked.Exchange(ref client, null);

            if (socket is null)
                return;

            try
            {
                if (socket.Connected)
                    socket.Shutdown(SocketShutdown.Both);
            }
            catch (SocketException)
            { }
            catch (ObjectDisposedException)
            { }

            socket.Dispose();

        }

        #endregion

        #region Dispose()

        /// <summary>
        /// Stop the listener and close the connected client.
        /// </summary>
        public void Dispose()
        {

            StopListening();
            CloseClient();

            listener.Dispose();

        }

        #endregion

        #region ToString()

        /// <summary>
        /// Return a text representation of this endpoint.
        /// </summary>
        public override String ToString()

            => IsConnected
                   ? $"TCP/{Port} <- {RemoteSocket}"
                   : $"TCP/{Port} (waiting)";

        #endregion

    }

}
