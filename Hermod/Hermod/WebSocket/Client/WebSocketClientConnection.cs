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

using System.Net.Sockets;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{

    /// <summary>
    /// A HTTP web socket client connection.
    /// </summary>
    public class WebSocketClientConnection : IEquatable<WebSocketClientConnection>
    {

        #region Data

        private readonly  Dictionary<String, Object?>  customData;

        private readonly  Socket                       tcpSocket;

        private readonly  MyNetworkStream              tcpStream;

        private readonly  Stream                       httpStream;

        public  volatile  Boolean                      IsClosed;

        private readonly  SemaphoreSlim                socketWriteSemaphore = new (1, 1);

        #endregion

        #region Properties

        /// <summary>
        /// The creation timestamp.
        /// </summary>
        public DateTime                 Created                       { get; }

        public CancellationTokenSource  CancellationTokenSource       { get; }

        /// <summary>
        /// The HTTP web socket client.
        /// </summary>
        public WebSocketClient          WebSocketClient               { get; }

        /// <summary>
        /// The local TCP socket.
        /// </summary>
        public IPSocket                 LocalSocket                   { get; }

        /// <summary>
        /// The remote TCP socket.
        /// </summary>
        public IPSocket                 RemoteSocket                  { get; }

        /// <summary>
        /// The optional HTTP request of this web socket connection. Can also be attached later.
        /// </summary>
        public HTTPRequest?             HTTPRequest                   { get; internal set; }

        /// <summary>
        /// The optional HTTP response of this web socket connection. Can also be attached later.
        /// </summary>
        public HTTPResponse?            HTTPResponse                  { get; internal set; }

        /// <summary>
        /// For debugging reasons data can be send really really slow...
        /// </summary>
        public TimeSpan?                SlowNetworkSimulationDelay    { get; }

        /// <summary>
        /// The amount of time a read operation blocks waiting for data.
        /// </summary>
        public TimeSpan ReadTimeout
        {
            get
            {
                return TimeSpan.FromMilliseconds(tcpStream.ReadTimeout);
            }
            set
            {
                tcpStream.ReadTimeout = (Int32) value.TotalMilliseconds;
            }
        }

        /// <summary>
        /// The amount of time a write operation blocks before failing.
        /// </summary>
        public TimeSpan WriteTimeout
        {
            get
            {
                return TimeSpan.FromMilliseconds(tcpStream.WriteTimeout);
            }
            set
            {
                tcpStream.WriteTimeout = (Int32) value.TotalMilliseconds;
            }
        }

        /// <summary>
        /// Whether data on the network stream is available for reading.
        /// </summary>
        public Boolean DataAvailable
        {
            get
            {
                try
                {
                    return tcpStream.DataAvailable;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// The amount of data on the network stream available for reading.
        /// </summary>
        public Int32 Available
        {
            get
            {
                try
                {
                    return tcpSocket.Available;
                }
                catch
                {
                    return 0;
                }
            }
        }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTTP web socket client connection.
        /// </summary>
        /// <param name="WebSocketClient">A HTTP web socket client.</param>
        /// <param name="TCPSocket">A TCP connection abstraction.</param>
        /// <param name="HTTPRequest">An optional HTTP request of this web socket connection. Can also be attached later.</param>
        /// <param name="HTTPResponse">An optional HTTP response of this web socket connection. Can also be attached later.</param>
        /// <param name="CustomData">Optional custom data to be stored within this web socket connection.</param>
        public WebSocketClientConnection(WebSocketClient                              WebSocketClient,
                                         Socket                                       TCPSocket,
                                         MyNetworkStream                              NetworkStream,
                                         Stream                                       HTTPStream,
                                         HTTPRequest?                                 HTTPRequest                  = null,
                                         HTTPResponse?                                HTTPResponse                 = null,
                                         IEnumerable<KeyValuePair<String, Object?>>?  CustomData                   = null,
                                         TimeSpan?                                    SlowNetworkSimulationDelay   = null)
        {

            this.Created                     = Timestamp.Now;
            this.CancellationTokenSource     = new CancellationTokenSource();
            this.WebSocketClient             = WebSocketClient;
            this.tcpSocket                   = TCPSocket;
            this.tcpStream                   = NetworkStream;
            this.httpStream                  = HTTPStream;
            this.LocalSocket                 = IPSocket.FromIPEndPoint(TCPSocket.LocalEndPoint)  ?? IPSocket.Zero;
            this.RemoteSocket                = IPSocket.FromIPEndPoint(TCPSocket.RemoteEndPoint) ?? IPSocket.Zero;
            this.HTTPRequest                 = HTTPRequest;
            this.HTTPResponse                = HTTPResponse;
            this.customData                  = CustomData is not null
                                                   ? CustomData. ToDictionary(kvp => kvp.Key,
                                                                              kvp => kvp.Value)
                                                   : new Dictionary<String, Object?>();
            this.SlowNetworkSimulationDelay  = SlowNetworkSimulationDelay;

        }

        #endregion


        #region (private) Send    (Data,           CancellationToken = default)

        /// <summary>
        /// Send the given array of bytes.
        /// </summary>
        /// <param name="Data">The array of bytes to send.</param>
        /// <param name="CancellationToken">An optional cancellation token to cancel this request.</param>
        private async Task<SendStatus> Send(Byte[]             Data,
                                            CancellationToken  CancellationToken = default)
        {

            await socketWriteSemaphore.WaitAsync(CancellationToken);

            try
            {

                if (SlowNetworkSimulationDelay.HasValue)
                {
                    foreach (var singleByte in Data)
                    {

                        await tcpStream.WriteAsync(new[] {
                                                       singleByte
                                                   },
                                                   CancellationToken);

                        await tcpStream.FlushAsync(CancellationToken);

                        await Task.Delay(SlowNetworkSimulationDelay.Value,
                                         CancellationToken);

                    }
                }

                else
                {

                    await tcpStream.WriteAsync(Data,
                                               CancellationToken);

                    await tcpStream.FlushAsync(CancellationToken);

                }

                return SendStatus.Success;

            }
            catch (Exception e)
            {

                if (e.InnerException is SocketException socketException)
                {
                    if (socketException.SocketErrorCode == SocketError.ConnectionReset)
                        return SendStatus.FatalError;
                }

                //DebugX.LogException(e, "Sending data within web socket connection " + RemoteSocket);

            }
            finally
            {
                socketWriteSemaphore.Release();
            }

            return SendStatus.Error;

        }

        #endregion

        #region SendData          (Data,           CancellationToken = default)

        /// <summary>
        /// Send the given array of bytes.
        /// </summary>
        /// <param name="Data">The array of bytes to send.</param>
        /// <param name="CancellationToken">An optional cancellation token to cancel this request.</param>
        public Task<SendStatus> SendData(Byte[]             Data,
                                         CancellationToken  CancellationToken   = default)

            => Send(Data,
                    CancellationToken);

        #endregion

        #region SendText          (SendText,       CancellationToken = default)

        /// <summary>
        /// Send the given text.
        /// </summary>
        /// <param name="SendText">A text to send.</param>
        /// <param name="CancellationToken">An optional cancellation token to cancel this request.</param>
        public Task<SendStatus> SendText(String             SendText,
                                         CancellationToken  CancellationToken   = default)

            => Send(SendText.ToUTF8Bytes(),
                    CancellationToken);

        #endregion

        #region SendWebSocketFrame(WebSocketFrame, CancellationToken = default)

        /// <summary>
        /// Send the given web socket frame.
        /// </summary>
        /// <param name="WebSocketFrame">A web socket frame.</param>
        /// <param name="CancellationToken">An optional cancellation token to cancel this request.</param>
        public Task<SendStatus> SendWebSocketFrame(WebSocketFrame     WebSocketFrame,
                                                   CancellationToken  CancellationToken   = default)

            => Send(WebSocketFrame.ToByteArray(),
                    CancellationToken);

        #endregion


        #region Read(Buffer, Offset, Count)

        /// <summary>
        /// Tries to reads the given amount of data from the network stream.
        /// </summary>
        /// <param name="Buffer">The byte array for storing the received data.</param>
        /// <param name="Offset">An offset within the byte array.</param>
        /// <param name="Count">The maximum number of bytes to read and store.</param>
        /// <returns>Number of bytes read, or 0.</returns>
        public UInt32 Read(Byte[]  Buffer,
                           UInt32  Offset,
                           UInt32  Count)
        {

            try
            {

                return (UInt32) tcpStream.Read(Buffer,
                                               (Int32) Offset,
                                               (Int32) Count);

            }
            catch
            {
                return 0;
            }

        }

        /// <summary>
        /// Tries to reads the given amount of data from the network stream.
        /// </summary>
        /// <param name="Buffer">The byte array for storing the received data.</param>
        /// <param name="Offset">An offset within the byte array.</param>
        /// <param name="Count">The maximum number of bytes to read and store.</param>
        /// <returns>Number of bytes read, or 0.</returns>
        public UInt32 Read(Byte[]  Buffer,
                           Int32   Offset,
                           Int32   Count)
        {

            try
            {

                return (UInt32) tcpStream.Read(Buffer,
                                               Offset,
                                               Count);

            }
            catch
            {
                return 0;
            }

        }

        #endregion

        #region Close()

        /// <summary>
        /// Close this web socket connection.
        /// </summary>
        public void Close()
        {
            try
            {

                tcpSocket.Close();

                IsClosed = true;

            }
            catch (Exception e)
            {
                DebugX.Log(String.Concat(nameof(WebSocketClientConnection), ".", nameof(Close), ": Exception occured: ", e.Message));
            }
        }

        #endregion


        #region Custom data

        #region AddCustomData(Key, Value)

        /// <summary>
        /// Add custom data to this HTTP web socket client connection.
        /// </summary>
        /// <param name="Key">A key.</param>
        /// <param name="Value">A value.</param>
        public void AddCustomData(String Key, Object? Value)
        {
            lock (customData)
            {

                if (customData.ContainsKey(Key))
                    customData[Key] = Value;

                else
                    customData.Add(Key, Value);

            }
        }

        #endregion


        #region HasCustomData(Key)

        /// <summary>
        /// Checks whether the given data key is present within this HTTP web socket client connection.
        /// </summary>
        /// <param name="Key">A key.</param>
        public Boolean HasCustomData(String Key)
            => customData.ContainsKey(Key);

        #endregion


        #region TryGetCustomData  (Key)

        /// <summary>
        /// Tries to return the data associated with the given key from this HTTP web socket client connection.
        /// </summary>
        /// <param name="Key">A key.</param>
        public Object? TryGetCustomData(String Key)
        {

            if (customData.TryGetValue(Key, out var data))
                return data;

            return default;

        }

        #endregion

        #region TryGetCustomDataAs(Key)

        /// <summary>
        /// Tries to return the data associated with the given key from this HTTP web socket client connection.
        /// </summary>
        /// <typeparam name="T">The type of the stored value.</typeparam>
        /// <param name="Key">A key.</param>
        public T? TryGetCustomDataAs<T>(String Key)
            where T : struct
        {

            if (customData.TryGetValue(Key, out var data) && data is T dataT)
                return dataT;

            return default;

        }

        #endregion


        #region TryGetCustomData  (Key, out Value)

        /// <summary>
        /// Tries to return the data associated with the given key from this HTTP web socket client connection.
        /// </summary>
        /// <param name="Key">A key.</param>
        /// <param name="Value">The requested value.</param>
        public Boolean TryGetCustomData(String Key, out Object? Value)
        {

            if (customData.TryGetValue(Key, out Value))
                return true;

            Value = default;
            return false;

        }

        #endregion

        #region TryGetCustomDataAs(Key, out Value)

        /// <summary>
        /// Tries to return the data associated with the given key from this HTTP web socket client connection.
        /// </summary>
        /// <typeparam name="T">The type of the stored value.</typeparam>
        /// <param name="Key">A key.</param>
        /// <param name="Value">The requested value.</param>
        public Boolean TryGetCustomDataAs<T>(String Key, out T? Value)
        {

            if (customData.TryGetValue(Key, out var data) && data is T dataT)
            {
                Value = (T) dataT;
                return true;
            }

            Value = default;
            return false;

        }

        #endregion

        #endregion


        #region Operator overloading

        #region Operator == (WebSocketConnection1, WebSocketConnection2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="WebSocketConnection1">A HTTP web socket client connection.</param>
        /// <param name="WebSocketConnection2">Another HTTP web socket client connection.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (WebSocketClientConnection WebSocketConnection1,
                                           WebSocketClientConnection WebSocketConnection2)
        {

            // If both are null, or both are same instance, return true.
            if (ReferenceEquals(WebSocketConnection1, WebSocketConnection2))
                return true;

            // If one is null, but not both, return false.
            if (WebSocketConnection1 is null || WebSocketConnection2 is null)
                return false;

            return WebSocketConnection1.Equals(WebSocketConnection2);

        }

        #endregion

        #region Operator != (WebSocketConnection1, WebSocketConnection2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="WebSocketConnection1">A HTTP web socket client connection.</param>
        /// <param name="WebSocketConnection2">Another HTTP web socket client connection.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (WebSocketClientConnection WebSocketConnection1,
                                           WebSocketClientConnection WebSocketConnection2)

            => !(WebSocketConnection1 == WebSocketConnection2);

        #endregion

        #region Operator <  (WebSocketConnection1, WebSocketConnection2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="WebSocketConnection1">A HTTP web socket client connection.</param>
        /// <param name="WebSocketConnection2">Another HTTP web socket client connection.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (WebSocketClientConnection WebSocketConnection1,
                                          WebSocketClientConnection WebSocketConnection2)
        {

            if (WebSocketConnection1 is null)
                throw new ArgumentNullException(nameof(WebSocketConnection2), "The given HTTP web socket client connection must not be null!");

            return WebSocketConnection1.CompareTo(WebSocketConnection2) < 0;

        }

        #endregion

        #region Operator <= (WebSocketConnection1, WebSocketConnection2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="WebSocketConnection1">A HTTP web socket client connection.</param>
        /// <param name="WebSocketConnection2">Another HTTP web socket client connection.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (WebSocketClientConnection WebSocketConnection1,
                                           WebSocketClientConnection WebSocketConnection2)

            => !(WebSocketConnection1 > WebSocketConnection2);

        #endregion

        #region Operator >  (WebSocketConnection1, WebSocketConnection2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="WebSocketConnection1">A HTTP web socket client connection.</param>
        /// <param name="WebSocketConnection2">Another HTTP web socket client connection.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (WebSocketClientConnection WebSocketConnection1,
                                          WebSocketClientConnection WebSocketConnection2)
        {

            if (WebSocketConnection1 is null)
                throw new ArgumentNullException(nameof(WebSocketConnection2), "The given HTTP web socket client connection must not be null!");

            return WebSocketConnection1.CompareTo(WebSocketConnection2) > 0;

        }

        #endregion

        #region Operator >= (WebSocketConnection1, WebSocketConnection2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="WebSocketConnection1">A HTTP web socket client connection.</param>
        /// <param name="WebSocketConnection2">Another HTTP web socket client connection.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (WebSocketClientConnection WebSocketConnection1,
                                           WebSocketClientConnection WebSocketConnection2)

            => !(WebSocketConnection1 < WebSocketConnection2);

        #endregion

        #endregion

        #region IComparable<WebSocketConnection> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two HTTP web socket client connections.
        /// </summary>
        /// <param name="Object">A HTTP web socket client connection to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is WebSocketClientConnection webSocketClientConnection
                   ? CompareTo(webSocketClientConnection)
                   : throw new ArgumentException("The given object is not a HTTP web socket client connection!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(WebSocketConnection)

        /// <summary>
        /// Compares two HTTP web socket client connections.
        /// </summary>
        /// <param name="WebSocketConnection">A HTTP web socket client connection to compare with.</param>
        public Int32 CompareTo(WebSocketClientConnection? WebSocketConnection)
        {

            if (WebSocketConnection is null)
                throw new ArgumentNullException(nameof(WebSocketConnection), "The given HTTP web socket client connection must not be null!");

            return LocalSocket.CompareTo(WebSocketConnection.LocalSocket);

        }

        #endregion

        #endregion

        #region IEquatable<WebSocketConnection> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two HTTP web socket client connections for equality.
        /// </summary>
        /// <param name="Object">A HTTP web socket client connection to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is WebSocketClientConnection webSocketClientConnection &&
                   Equals(webSocketClientConnection);

        #endregion

        #region Equals(WebSocketConnection)

        /// <summary>
        /// Compares two HTTP web socket client connections for equality.
        /// </summary>
        /// <param name="WebSocketConnection">A HTTP web socket client connection to compare with.</param>
        public Boolean Equals(WebSocketClientConnection? WebSocketConnection)

            => WebSocketConnection is not null &&
                   LocalSocket.Equals(WebSocketConnection.LocalSocket);

        #endregion

        #endregion

        #region GetHashCode()

        /// <summary>
        /// Get the hashcode of this object.
        /// </summary>
        public override Int32 GetHashCode()

            => LocalSocket.GetHashCode();

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"{LocalSocket} <=> {RemoteSocket}";

        #endregion


    }

}
