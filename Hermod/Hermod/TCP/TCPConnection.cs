/*
 * Copyright (c) 2010-2023 GraphDefined GmbH
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
using System.Text;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP
{

    public delegate X509Certificate2 ServerCertificateSelectorDelegate(TCPServer TCPServer, TcpClient TCPClient);

    /// <summary>
    /// An abstract class for all TCP connections.
    /// </summary>
    public class TCPConnection : AReadOnlyLocalRemoteSockets
    {

        #region Data

        // For method 'ReadLine(...)'...
        private Boolean SkipNextN = false;

        #endregion

        #region Properties

        /// <summary>
        /// The associated TCP server.
        /// </summary>
        public TCPServer          TCPServer            { get; }

        /// <summary>
        /// The associated TCP client.
        /// </summary>
        public TcpClient          TCPClient            { get; }

        /// <summary>
        /// The underlying network stream.
        /// </summary>
        public NetworkStream      NetworkStream        { get; }

        /// <summary>
        /// An optional SSL/TLS server certificate.
        /// </summary>
        public X509Certificate2?  ServerCertificate    { get; }

        /// <summary>
        /// The optional HTTP client certificate.
        /// </summary>
        public X509Certificate2?  ClientCertificate    { get; }

        /// <summary>
        /// The SSL/TLS protocol(s) to use.
        /// </summary>
        public SslProtocols       TLSProtocols         { get; }

        /// <summary>
        /// The underlying SSL/TLS stream.
        /// </summary>
        public SslStream?         SSLStream            { get; }

        /// <summary>
        /// The timestamp of the packet.
        /// </summary>
        public DateTime           ServerTimestamp      { get; }

        /// <summary>
        /// The TCP connection identification.
        /// </summary>
        public String             ConnectionId         { get;}

        #region IsConnected

        /// <summary>
        /// Check if the client is still connected to the server.
        /// </summary>
        public Boolean IsConnected
        {

            get
            {

                if (TCPClient is null)
                    return false;

                // This is not working as expected! Damn you Microsoft! Perhaps
                // you better hire some good networking people!
                //return TCPClientConnection.Connected;

                // A better, but not really smart way to check if the
                // TCP connection is/was closed
                if (TCPClient.Client.Poll(1, SelectMode.SelectRead) &&
                    TCPClient.Available == 0)
                {
                    return false;
                }

                return true;

            }

        }

        #endregion

        #region DataAvailable

        /// <summary>
        /// Gets a value that indicates whether data is available
        /// on the System.Net.Sockets.NetworkStream to be read.
        /// </summary>
        public Boolean DataAvailable

            => this.NetworkStream?.DataAvailable == true;

        #endregion

        #region ReadTimeout

        private Int32 readTimeoutMS;

        /// <summary>
        /// Gets or sets the amount of time, that a read operation
        /// blocks waiting for data. On default the read operation does not time out.
        /// </summary>
        public TimeSpan ReadTimeout
        {

            get
            {
                return TimeSpan.FromMilliseconds(readTimeoutMS);
            }

            set
            {

                this.readTimeoutMS              = (Int32) value.TotalMilliseconds;
                this.NetworkStream.ReadTimeout  = readTimeoutMS;

                if (SSLStream is not null)
                    SSLStream.ReadTimeout       = readTimeoutMS;

            }

        }

        #endregion

        #region NoDelay

        /// <summary>
        /// Gets or sets a value that disables a delay when send or receive
        /// buffers are not full.
        /// </summary>
        public Boolean NoDelay
        {

            get
            {
                return TCPClient.NoDelay;
            }

            set
            {
                TCPClient.NoDelay = value;
            }

        }

        #endregion

        #region KeepAlive

        /// <summary>
        /// The connection is keepalive
        /// </summary>
        public Boolean KeepAlive { get; set; }

        #endregion

        #region StopRequested

        /// <summary>
        /// Server was requested to stop.
        /// </summary>
        public Boolean StopRequested { get; set; }

        #endregion

        #region IsClosed

        private volatile Boolean isClosed;

        public Boolean IsClosed
            => isClosed;

        #endregion

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new TCP connection.
        /// </summary>
        /// <param name="TCPServer">A TCP server.</param>
        /// <param name="TCPClient">A TCP client.</param>
        /// <param name="ServerCertificateSelector">An optional delegate to select a SSL/TLS server certificate.</param>
        /// <param name="ClientCertificateValidator">An optional delegate to verify the SSL/TLS client certificate used for authentication.</param>
        /// <param name="ClientCertificateSelector">An optional delegate to select the SSL/TLS client certificate used for authentication.</param>
        /// <param name="AllowedTLSProtocols">The SSL/TLS protocol(s) allowed for this connection.</param>
        public TCPConnection(TCPServer                             TCPServer,
                             TcpClient                             TCPClient,
                             ServerCertificateSelectorDelegate?    ServerCertificateSelector    = null,
                             RemoteCertificateValidationCallback?  ClientCertificateValidator   = null,
                             LocalCertificateSelectionCallback?    ClientCertificateSelector    = null,
                             SslProtocols?                         AllowedTLSProtocols          = null,
                             TimeSpan?                             ReadTimeout                  = null,
                             TimeSpan?                             WriteTimeout                 = null)

#pragma warning disable CS8602 // Dereference of a possibly null reference.
            : base(new IPSocket(new IPv4Address((         TCPClient.Client.LocalEndPoint  as IPEndPoint).Address),
                                IPPort.Parse   ((UInt16) (TCPClient.Client.LocalEndPoint  as IPEndPoint).Port)),
                   new IPSocket(new IPv4Address((         TCPClient.Client.RemoteEndPoint as IPEndPoint).Address),
                                IPPort.Parse   ((UInt16) (TCPClient.Client.RemoteEndPoint as IPEndPoint).Port)))
#pragma warning restore CS8602 // Dereference of a possibly null reference.

        {

            this.TCPServer            = TCPServer ?? throw new ArgumentNullException(nameof(TCPServer), "The given TCP server must not be null!");
            this.TCPClient            = TCPClient ?? throw new ArgumentNullException(nameof(TCPClient), "The given TCP client must not be null!");
            this.ServerTimestamp      = Timestamp.Now;
            this.ConnectionId         = TCPServer.ConnectionIdBuilder(this,
                                                                      this.ServerTimestamp,
                                                                      base.LocalSocket,
                                                                      base.RemoteSocket);
            this.isClosed             = false;
            this.NetworkStream        = TCPClient.GetStream();

            if (ReadTimeout.HasValue)
            {
                this.readTimeoutMS               = (Int32) ReadTimeout.Value.TotalMilliseconds;
                this.NetworkStream.ReadTimeout   = (Int32) ReadTimeout.Value.TotalMilliseconds;
            }

            if (WriteTimeout.HasValue)
            {
                //this.readTimeoutMS              = (Int32) ReadTimeout.Value.TotalMilliseconds;
                this.NetworkStream.WriteTimeout  = (Int32) WriteTimeout.Value.TotalMilliseconds;
            }

            this.ServerCertificate    = ServerCertificateSelector?.Invoke(TCPServer, TCPClient);

            if (ServerCertificate is not null)
            {

                try
                {

                    //DebugX.Log(" [TCPServer:", LocalPort.ToString(), "] New TLS connection using server certificate: " + this.ServerCertificate.Subject);

                    this.SSLStream      = new SslStream(innerStream:                        NetworkStream,
                                                        leaveInnerStreamOpen:               true,
                                                        userCertificateValidationCallback:  ClientCertificateValidator,
                                                        userCertificateSelectionCallback:   ClientCertificateSelector,
                                                        encryptionPolicy:                   EncryptionPolicy.RequireEncryption);

                    this.SSLStream.AuthenticateAsServer(serverCertificate:                  ServerCertificate,
                                                        clientCertificateRequired:          ClientCertificateValidator is not null,
                                                        enabledSslProtocols:                AllowedTLSProtocols ?? SslProtocols.Tls12 | SslProtocols.Tls13,
                                                        checkCertificateRevocation:         false);

                    if (this.SSLStream.RemoteCertificate is not null)
                    {

                        this.ClientCertificate = new X509Certificate2(this.SSLStream.RemoteCertificate);

                        //DebugX.Log(" [TCPServer:", LocalPort.ToString(), "] New TLS connection using client certificate: " + this.ClientCertificate.Subject);

                    }

                }
                catch (Exception e)
                {
                    DebugX.Log(" [TCPServer:", LocalPort.ToString(), "] TLS exception: ", e.Message, e.StackTrace is not null ? Environment.NewLine + e.StackTrace : "");
                    throw;
                }

            }

            //_TCPClient.ReceiveTimeout           = (Int32) ConnectionTimeout.TotalMilliseconds;
            //_TCPConnection.Value.ReadTimeout    = ClientTimeout;
            //_TCPConnection.Value.StopRequested  = false;

            //Thread.CurrentThread.Name = (TCPServer.ConnectionThreadsNameBuilder is not null)
            //                                 ? TCPServer.ConnectionThreadsNameBuilder(this,
            //                                                                          this.ServerTimestamp,
            //                                                                          base.LocalSocket,
            //                                                                          base.RemoteSocket)
            //                                 : "TCP connection from " +
            //                                         base.RemoteSocket.IPAddress.ToString() +
            //                                         ":" +
            //                                         base.RemoteSocket.Port.ToString();

        }

        #endregion


        #region Read(SleepingTimeMS = 5, MaxInitialWaitingTimeMS = 500)

        /// <summary>
        /// Read a byte value from the TCP connection.
        /// </summary>
        /// <param name="SleepingTimeMS">When no data is currently available wait at least this amount of time [milliseconds].</param>
        /// <param name="MaxInitialWaitingTimeMS">When no data is currently available wait at most this amount of time [milliseconds].</param>
        /// <returns>The read byte OR 0x00 if nothing could be read.</returns>
        public Byte Read(UInt16 SleepingTimeMS = 5, UInt32 MaxInitialWaitingTimeMS = 500)
        {

            if (!NetworkStream.CanRead)
                return 0x00;

            var WaitingTimeMS = 0;
            var Value         = -1;

            while (!NetworkStream.DataAvailable && (WaitingTimeMS < MaxInitialWaitingTimeMS))
            {
                Thread.Sleep(SleepingTimeMS);
                WaitingTimeMS += SleepingTimeMS;
            }

            if (NetworkStream.DataAvailable)
            {
                Value = SSLStream != null
                            ? SSLStream.    ReadByte()
                            : NetworkStream.ReadByte();
            }

            if (Value == -1)
                return 0x00;

            return (Byte) Value;

        }

        #endregion

        #region TryRead(out Byte, SleepingTimeMS = 5, MaxInitialWaitingTimeMS = 500)

        /// <summary>
        /// Try to read a byte value from the TCP connection.
        /// </summary>
        /// <param name="Byte">The byte value OR 0x00 if nothing could be read.</param>
        /// <param name="SleepingTimeMS">When no data is currently available wait at least this amount of time [milliseconds].</param>
        /// <param name="MaxInitialWaitingTimeMS">When no data is currently available wait at most this amount of time [milliseconds].</param>
        /// <returns>True, if the byte value is valid; False otherwise.</returns>
        public TCPClientResponse TryRead(out Byte Byte, UInt16 SleepingTimeMS = 5, UInt32 MaxInitialWaitingTimeMS = 50)
        {

            Byte = 0x00;

            if (!NetworkStream.CanRead)
                return TCPClientResponse.CanNotRead;

            if (SSLStream is not null)
            {

                var value = SSLStream.ReadByte();

                if (value == -1)
                    return TCPClientResponse.CanNotRead;

                Byte = (Byte) value;

                return TCPClientResponse.DataAvailable;

            }

            var WaitingTimeMS  = 0;
            var Value          = -1;


            //if (TCPClientConnection.Client.Poll(1, SelectMode.SelectRead) &&
            //    TCPClientConnection.Available == 0)


            while (TCPClient.Client.Poll(1, SelectMode.SelectRead) == false &&
                   NetworkStream.DataAvailable == false &&
                   (WaitingTimeMS < MaxInitialWaitingTimeMS))
            {
                Thread.Sleep(SleepingTimeMS);
                WaitingTimeMS += SleepingTimeMS;
            }

            if (NetworkStream.DataAvailable)
            {
                Value = SSLStream is not null
                            ? SSLStream.    ReadByte()
                            : NetworkStream.ReadByte();
            }
            else
            {
                if (WaitingTimeMS >= MaxInitialWaitingTimeMS)
                    return TCPClientResponse.Timeout;
                else
                    return TCPClientResponse.ClientClose;
            }

            if (Value == -1)
                return TCPClientResponse.CanNotRead;

            Byte = (Byte) Value;

            return TCPClientResponse.DataAvailable;

        }

        #endregion

        #region Read(Buffer, SleepingTimeMS = 5, MaxInitialWaitingTimeMS = 500)

        /// <summary>
        /// Read multiple byte values from the TCP connection into the given buffer.
        /// </summary>
        /// <param name="Buffer">An array of byte values.</param>
        /// <param name="SleepingTimeMS">When no data is currently available wait at least this amount of time [milliseconds].</param>
        /// <param name="MaxInitialWaitingTimeMS">When no data is currently available wait at most this amount of time [milliseconds].</param>
        /// <returns>The number of read bytes.</returns>
        public Int32 Read(Byte[] Buffer, UInt16 SleepingTimeMS = 5, UInt32 MaxInitialWaitingTimeMS = 500)
        {

            if (Buffer is null || Buffer.Length < 1)
                throw new ArgumentNullException(nameof(Buffer), "The given buffer must not be null or empty!");

            if (!NetworkStream.CanRead)
                return 0;

            var WaitingTimeMS = 0;

            while (!NetworkStream.DataAvailable && (WaitingTimeMS < MaxInitialWaitingTimeMS))
            {
                Thread.Sleep(SleepingTimeMS);
                WaitingTimeMS += SleepingTimeMS;
            }

            if (NetworkStream.DataAvailable)
            {
                return SSLStream != null
                           ? SSLStream.    Read(Buffer, 0, Buffer.Length)
                           : NetworkStream.Read(Buffer, 0, Buffer.Length);
            }

            return 0;

        }

        #endregion

        #region ReadString(MaxLength = 1024, Encoding = null, SleepingTimeMS = 5, MaxInitialWaitingTimeMS = 500)

        /// <summary>
        /// Read a string value from the TCP connection.
        /// </summary>
        /// <param name="MaxLength">The maximal length of the string.</param>
        /// <param name="Encoding">The character encoding of the string (default: UTF8).</param>
        /// <param name="SleepingTimeMS">When no data is currently available wait at least this amount of time [milliseconds].</param>
        /// <param name="MaxInitialWaitingTimeMS">When no data is currently available wait at most this amount of time [milliseconds].</param>
        public String ReadString(Int32 MaxLength = 1024, Encoding Encoding = null, UInt16 SleepingTimeMS = 5, UInt32 MaxInitialWaitingTimeMS = 500)
        {

            if (!NetworkStream.CanRead)
                return String.Empty;

            var WaitingTimeMS = 0;

            while (!NetworkStream.DataAvailable && (WaitingTimeMS < MaxInitialWaitingTimeMS))
            {
                Thread.Sleep(SleepingTimeMS);
                WaitingTimeMS += SleepingTimeMS;
            }

            if (NetworkStream.DataAvailable)
            {

                Byte ByteValue;
                var ByteArray  = new Byte[MaxLength];
                var Position   = 0U;

                while (NetworkStream.DataAvailable)
                {

                    ByteValue = SSLStream != null
                                    ? (Byte) SSLStream.    ReadByte()
                                    : (Byte) NetworkStream.ReadByte();

                    ByteArray[Position++] = ByteValue;

                    if (ByteValue == 0x00 ||
                        Position  == MaxLength)
                        break;

                }

                if (Position > 0)
                {

                    if (Encoding == null)
                        Encoding = Encoding.UTF8;

                    Array.Resize(ref ByteArray, (Int32) Position - 1);

                    return Encoding.GetString(ByteArray);

                }

            }

            return String.Empty;

        }

        #endregion

        #region ReadLine(MaxLength = 65535, Encoding = null, SleepingTimeMS = null, MaxInitialWaitingTimeMS = null, ReadTimeout = null)

        /// <summary>
        /// Read a line from the TCP connection.
        /// </summary>
        /// <param name="MaxLength">The maximal length of the string.</param>
        /// <param name="Encoding">The character encoding of the string (default: UTF8).</param>
        /// <param name="SleepingTime">When no data is currently available wait at least this amount of time [5ms].</param>
        /// <param name="MaxInitialWaitingTime">When no data is currently available wait at most this amount of time [500ms].</param>
        /// <param name="__ReadTimeout">The read timeout [20sec].</param>
        public String? ReadLine(Int32      MaxLength               = 65535,
                                Encoding?  Encoding                = null,
                                TimeSpan?  SleepingTime            = null,
                                TimeSpan?  MaxInitialWaitingTime   = null,
                                TimeSpan?  __ReadTimeout           = null)
        {

            if (!NetworkStream.CanRead)
                return null;

            if (!SleepingTime.HasValue)
                SleepingTime = TimeSpan.FromMilliseconds(5);

            if (__ReadTimeout.HasValue)
            {

                NetworkStream.ReadTimeout  = (Int32) __ReadTimeout.Value.TotalMilliseconds;

                if (SSLStream != null)
                    SSLStream.ReadTimeout  = (Int32) __ReadTimeout.Value.TotalMilliseconds;

            }

            var sleepingTimeMS           = (Int32) SleepingTime.Value.TotalMilliseconds;
            var maxInitialWaitingTimeMS  = (Int32) MaxInitialWaitingTime.Value.TotalMilliseconds;
            var totalWaitingTime         = 0;

            while (!NetworkStream.DataAvailable && (totalWaitingTime < maxInitialWaitingTimeMS))
            {
                Thread.Sleep(sleepingTimeMS);
                totalWaitingTime += sleepingTimeMS;
            }

            var Started = Timestamp.Now;

            if (NetworkStream.DataAvailable)
            {

                Int32 ByteValue;
                var ByteArray  = new Byte[MaxLength];
                var Position   = 0;

                try
                {

                    // Do not stop (on slow connections)
                    //  before a valid EOL was found!
                    do
                    {

                        ByteValue = SSLStream is not null
                                        ? SSLStream.ReadByte()
                                        : NetworkStream.ReadByte();

                        #region Nothing or a '\r' or a '\n' was read!

                        // Nothing was read!
                        if (ByteValue == -1)
                        {
                            Thread.Sleep(sleepingTimeMS);
                            continue;
                        }

                        // Last time a '\r' was read, thus this might be the '\n' of it!
                        if (SkipNextN && ByteValue == '\n')
                        {
                            SkipNextN = false;
                            continue;
                        }

                        if (ByteValue == '\r')
                        {
                            SkipNextN = true;
                            break;
                        }

                        if (ByteValue == '\n' ||
                            Position == MaxLength)
                            break;

                        #endregion

                        ByteArray[Position++] = (Byte) ByteValue;

                    } while (Timestamp.Now - Started < ReadTimeout);

                }
                catch (Exception e)
                {

                }

                if (Position > 0)
                {

                    Encoding ??= Encoding.UTF8;

                    Array.Resize(ref ByteArray, Position);

                    return Encoding.GetString(ByteArray);

                }

                // An empty line was read!
                return "";

            }

            return null;

        }

        #endregion


        #region WriteToResponseStream(UTF8Text)

        /// <summary>
        /// Writes some UTF-8 text to the underlying stream.
        /// </summary>
        /// <param name="UTF8Text">Some UTF-8 text.</param>
        public void WriteToResponseStream(String UTF8Text)
        {
            WriteToResponseStream(UTF8Text.ToUTF8Bytes());
        }

        #endregion

        #region WriteLineToResponseStream(UTF8Text)

        /// <summary>
        /// Writes some UTF-8 text to the underlying stream.
        /// </summary>
        /// <param name="UTF8Text">Some UTF-8 text.</param>
        public void WriteLineToResponseStream(String UTF8Text)
        {
            WriteToResponseStream((UTF8Text + "\r\n").ToUTF8Bytes());
        }

        #endregion

        #region WriteToResponseStream(Byte)

        /// <summary>
        /// Writes the given byte to the underlying stream.
        /// </summary>
        /// <param name="Byte">A single byte.</param>
        public void WriteToResponseStream(Byte Byte)
        {
            if (IsConnected)
            {

                if (SSLStream is not null)
                    SSLStream.    WriteByte(Byte);

                else
                    NetworkStream.WriteByte(Byte);

            }
        }

        #endregion

        #region WriteToResponseStream(ByteArray)

        /// <summary>
        /// Writes the given byte array to the underlying stream.
        /// </summary>
        /// <param name="ByteArray">An array of bytes.</param>
        public void WriteToResponseStream(Byte[] ByteArray)
        {

            if (ByteArray is not null && IsConnected)
            {

                if (SSLStream is not null)
                    SSLStream.    Write(ByteArray, 0, ByteArray.Length);

                else if (NetworkStream is not null)
                    NetworkStream.Write(ByteArray, 0, ByteArray.Length);

                else
                    DebugX.LogT(nameof(TCPConnection) + " SSLStream and NetworkStream are both null!");

            }

            else if (!IsConnected)
                DebugX.LogT(nameof(TCPConnection) + " could not write to response stream: Not connected!");

            else
                DebugX.LogT(nameof(TCPConnection) + " could not write to response stream: Byte array is null!");

        }

        #endregion

        #region WriteToResponseStream(InputStream, ReadTimeout = 1000, BufferSize = 65535)

        /// <summary>
        /// Reads the given input stream and writes its content to the underlying stream.
        /// </summary>
        /// <param name="InputStream">A data source.</param>
        /// <param name="ReadTimeout">A read timeout on the source.</param>
        /// <param name="BufferSize">The buffer size for reading.</param>
        public void WriteToResponseStream(Stream InputStream, Int32 ReadTimeout = 1000, Int32 BufferSize = 65535)
        {

            if (IsConnected)
            {

                var buffer     = new Byte[BufferSize];
                var bytesRead  = 0;

                if (InputStream.CanTimeout && ReadTimeout != 1000)
                    InputStream.ReadTimeout = ReadTimeout;

                if (IsConnected)
                {
                    do
                    {

                        bytesRead = InputStream.Read(buffer, 0, buffer.Length);

                        if (SSLStream is not null)
                            SSLStream.    Write(buffer, 0, bytesRead);

                        else
                            NetworkStream.Write(buffer, 0, bytesRead);

                    } while (bytesRead != 0);
                }

            }

        }

        #endregion


        #region Flush()

        /// <summary>
        /// Flush all streams.
        /// </summary>
        public void Flush()
        {
            SSLStream?.    Flush();
            NetworkStream?.Flush();
        }

        #endregion

        #region Close(ClosedBy = ConnectionClosedBy.Server)

        /// <summary>
        /// Close this TCP connection.
        /// </summary>
        /// <param name="ClosedBy">Whether the connection was closed by the client or the server.</param>
        public void Close(ConnectionClosedBy ClosedBy = ConnectionClosedBy.Server)
        {
            if (!isClosed)
            {

                //DebugX.Log($" [TCPServer:{LocalPort}] TCP closing connection with {RemoteSocket}!");

                var stream = TCPClient?.GetStream();
                if (stream is not null)
                {

                    if (stream.DataAvailable)
                    {
                        var buffer = new Byte[1024];
                        do
                        {

                            try
                            {

                                var read = stream.Read(buffer, 0, buffer.Length);

                                //DebugX.Log($"Read {read} unexpected byte(s) before closing the TCP stream!");

                            }
                            catch (Exception e)
                            {
                                DebugX.Log($"Exception occured while reading unexpected byte(s) before closing the TCP stream: {e.Message}!");
                            }

                        } while (stream.DataAvailable);
                    }

                    stream.Close();

                }

                TCPClient?.Close();

                TCPServer.SendConnectionClosed(Timestamp.Now,
                                               RemoteSocket,
                                               ConnectionId,
                                               ClosedBy);

                isClosed = true;

                //DebugX.Log($" [TCPServer:{LocalPort}] TCP connection with {RemoteSocket} closed!");

            }
        }

        #endregion

        #region IDisposable Members

        /// <summary>
        /// Dispose this connection.
        /// </summary>
        public void Dispose()
        {
            //Close(ConnectionClosedBy.Server);
        }

        #endregion

    }

}
