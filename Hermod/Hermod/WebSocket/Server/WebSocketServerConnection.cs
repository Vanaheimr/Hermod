/*
 * Copyright (c) 2010-2024 GraphDefined GmbH <achim.friedland@graphdefined.com>
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
using System.Net.Security;
using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

using static org.GraphDefined.Vanaheimr.Hermod.WebSocket.WebSocketFrame;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{

    /// <summary>
    /// A HTTP WebSocket server connection.
    /// </summary>
    public class WebSocketServerConnection : IWebSocketConnection,
                                             IEquatable<WebSocketServerConnection>
    {

        #region Data

        private readonly  ConcurrentDictionary<String, Object?>  customData             = [];

        private readonly  TcpClient                              tcpClient;

        private readonly  Stream                                 networkStream;

        private readonly  NetworkStream                          tcpStream;

        private readonly  SslStream?                             tlsStream;

        public  volatile  Boolean                                IsClosed;

        private readonly  SemaphoreSlim                          socketWriteSemaphore   = new (1, 1);

        #endregion

        #region Properties

        /// <summary>
        /// The creation timestamp.
        /// </summary>
        public DateTime                 Created                       { get; }

        public CancellationTokenSource  CancellationTokenSource       { get; }

        /// <summary>
        /// The HTTP web socket server.
        /// </summary>
        public AWebSocketServer         WebSocketServer               { get; }

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
                return TimeSpan.FromMilliseconds(networkStream.ReadTimeout);
            }
            set
            {
                networkStream.ReadTimeout = (Int32) value.TotalMilliseconds;
            }
        }

        /// <summary>
        /// The amount of time a write operation blocks before failing.
        /// </summary>
        public TimeSpan WriteTimeout
        {
            get
            {
                return TimeSpan.FromMilliseconds(networkStream.WriteTimeout);
            }
            set
            {
                networkStream.WriteTimeout = (Int32) value.TotalMilliseconds;
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
                    return tcpClient.Available;
                }
                catch
                {
                    return 0;
                }
            }
        }

        public String?                  Login                         { get; internal set; }

        public X509Certificate2?        ClientCertificate             { get; private set; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTTP web socket server connection.
        /// </summary>
        /// <param name="WebSocketServer">A HTTP web socket server.</param>
        /// <param name="TcpClient">A TCP connection abstraction.</param>
        /// <param name="HTTPRequest">An optional HTTP request of this web socket connection. Can also be attached later.</param>
        /// <param name="HTTPResponse">An optional HTTP response of this web socket connection. Can also be attached later.</param>
        /// <param name="CustomData">Optional custom data to be stored within this web socket connection.</param>
        public WebSocketServerConnection(AWebSocketServer                             WebSocketServer,
                                         TcpClient                                    TcpClient,
                                         SslStream?                                   TLSStream,
                                         X509Certificate2?                            ClientCertificate            = null,
                                         HTTPRequest?                                 HTTPRequest                  = null,
                                         HTTPResponse?                                HTTPResponse                 = null,
                                         IEnumerable<KeyValuePair<String, Object?>>?  CustomData                   = null,
                                         TimeSpan?                                    SlowNetworkSimulationDelay   = null)
        {

            this.Created                     = Timestamp.Now;
            this.CancellationTokenSource     = new CancellationTokenSource();
            this.WebSocketServer             = WebSocketServer;
            this.tcpClient                   = TcpClient;
            this.tcpStream                   = TcpClient.GetStream();
            this.tlsStream                   = TLSStream;
            this.networkStream               = (Stream?) tlsStream ?? tcpStream;
            this.LocalSocket                 = IPSocket.FromIPEndPoint(TcpClient.Client.LocalEndPoint)  ?? IPSocket.Zero;
            this.RemoteSocket                = IPSocket.FromIPEndPoint(TcpClient.Client.RemoteEndPoint) ?? IPSocket.Zero;
            this.ClientCertificate           = ClientCertificate;
            this.HTTPRequest                 = HTTPRequest;
            this.HTTPResponse                = HTTPResponse;
            this.SlowNetworkSimulationDelay  = SlowNetworkSimulationDelay;

            if (CustomData is not null)
            {
                foreach (var customData in CustomData)
                {
                    this.customData.TryAdd(customData.Key,
                                           customData.Value);
                }
            }

        }

        #endregion


        #region Send     (Data,           CancellationToken = default)

        /// <summary>
        /// Send the given array of bytes.
        /// </summary>
        /// <param name="Data">The array of bytes to send.</param>
        /// <param name="CancellationToken">An optional cancellation token to cancel this request.</param>
        public async Task<SentStatus> Send(Byte[]             Data,
                                           CancellationToken  CancellationToken = default)
        {

            await socketWriteSemaphore.WaitAsync(CancellationToken);

            try
            {

                if (SlowNetworkSimulationDelay.HasValue)
                {
                    foreach (var singleByte in Data)
                    {

                        await networkStream.WriteAsync(new[] {
                                                           singleByte
                                                       },
                                                       CancellationToken);

                        await networkStream.FlushAsync(CancellationToken);

                        await Task.Delay(SlowNetworkSimulationDelay.Value,
                                         CancellationToken);

                    }
                }

                else
                {

                    await networkStream.WriteAsync(Data,
                                                   CancellationToken);

                    await networkStream.FlushAsync(CancellationToken);

                }

                return SentStatus.Success;

            }
            catch (Exception e)
            {

                if (e.InnerException is SocketException socketException)
                {
                    if (socketException.SocketErrorCode == SocketError.ConnectionReset)
                        return SentStatus.FatalError;
                }

                //DebugX.LogException(e, "Sending data within web socket connection " + RemoteSocket);

            }
            finally
            {
                socketWriteSemaphore.Release();
            }

            return SentStatus.Error;

        }

        #endregion

        #region SendText           (Text,           CancellationToken = default)

        ///// <summary>
        ///// Send the given plain text.
        ///// Do NOT use it to send web socket frames!
        ///// </summary>
        ///// <param name="Text">A text to send.</param>
        ///// <param name="CancellationToken">An optional cancellation token to cancel this request.</param>
        //public Task<SentStatus> SendText(String             Text,
        //                                 CancellationToken  CancellationToken   = default)

        //    => Send(Text.ToUTF8Bytes(),
        //            CancellationToken);

        #endregion

        #region SendBinary         (Data,           CancellationToken = default)

        ///// <summary>
        ///// Send the given plain array of bytes.
        ///// Do NOT use it to send web socket frames!
        ///// </summary>
        ///// <param name="Data">The array of bytes to send.</param>
        ///// <param name="CancellationToken">An optional cancellation token to cancel this request.</param>
        //public Task<SentStatus> SendBinary(Byte[]             Data,
        //                                   CancellationToken  CancellationToken   = default)

        //    => Send(Data,
        //            CancellationToken);

        #endregion

        #region SendWebSocketFrame (WebSocketFrame, CancellationToken = default)

        /// <summary>
        /// Send the given web socket frame.
        /// </summary>
        /// <param name="WebSocketFrame">A web socket frame.</param>
        /// <param name="CancellationToken">An optional cancellation token to cancel this request.</param>
        public Task<SentStatus> SendWebSocketFrame(WebSocketFrame     WebSocketFrame,
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

                return (UInt32) networkStream.Read(Buffer,
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

                return (UInt32) networkStream.Read(Buffer,
                                                   Offset,
                                                   Count);

            }
            catch
            {
                return 0;
            }

        }

        #endregion

        #region Close(StatusCode = NormalClosure, Reason = null, CancellationToken = default)

        /// <summary>
        /// Close this web socket connection.
        /// When a status code or reason is given, a HTTP WebSocket close frame will be sent.
        /// </summary>
        /// <param name="StatusCode">An optional closing status code for the HTTP WebSocket close frame.</param>
        /// <param name="Reason">An optional closing reason for the HTTP WebSocket close frame.</param>
        /// <param name="CancellationToken">An optional cancellation token to cancel this request.</param>
        public async Task Close(ClosingStatusCode? StatusCode          = null,
                                String?            Reason              = null,
                                CancellationToken  CancellationToken   = default)
        {
            try
            {

                if (IsClosed)
                    return;

                if (StatusCode.HasValue || Reason is not null)
                    await SendWebSocketFrame(
                              WebSocketFrame.Close(
                                  StatusCode ?? ClosingStatusCode.NormalClosure,
                                  Reason
                              ),
                              CancellationToken
                          );

                tlsStream?.Close();
                tcpClient. Close();

                IsClosed = true;

            }
            catch (Exception e)
            {
                DebugX.Log($"{nameof(WebSocketServerConnection)}.{nameof(Close)}(...): Exception occured: {e.Message}");
            }
        }

        #endregion


        #region Custom data

        #region TryAddCustomData  (Key, Value)

        /// <summary>
        /// Add custom data to this HTTP web socket server connection.
        /// </summary>
        /// <param name="Key">A key.</param>
        /// <param name="Value">A value.</param>
        public Boolean TryAddCustomData(String Key, Object? Value)

            => customData.TryAdd(Key,
                                 Value);

        #endregion

        #region TryAddCustomData  (Key, NewValue, ComparisonValue)

        /// <summary>
        /// Add custom data to this HTTP web socket server connection.
        /// </summary>
        /// <param name="Key">A key.</param>
        /// <param name="Value">A new value.</param>
        /// <param name="ComparisonValue">The old value to be updated.</param>
        public Boolean TryAddCustomData(String   Key,
                                        Object?  NewValue,
                                        Object?  ComparisonValue)

            => customData.TryUpdate(Key,
                                    NewValue,
                                    ComparisonValue);

        #endregion

        #region HasCustomData     (Key)

        /// <summary>
        /// Checks whether the given data key is present within this HTTP web socket server connection.
        /// </summary>
        /// <param name="Key">A key.</param>
        public Boolean HasCustomData(String Key)

            => customData.ContainsKey(Key);

        #endregion

        #region TryGetCustomData  (Key)

        /// <summary>
        /// Tries to return the data associated with the given key from this HTTP web socket server connection.
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
        /// Tries to return the data associated with the given key from this HTTP web socket server connection.
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
        /// Tries to return the data associated with the given key from this HTTP web socket server connection.
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
        /// Tries to return the data associated with the given key from this HTTP web socket server connection.
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


        #region ToJSON(CustomWebSocketServerConnectionSerializer = null)

        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        /// <param name="CustomWebSocketConnectionSerializer">A delegate to serialize custom HTTP WebSocket connections.</param>
        public JObject ToJSON(CustomJObjectSerializerDelegate<IWebSocketConnection>? CustomWebSocketConnectionSerializer = null)
        {

            var json = JSONObject.Create(

                                 new JProperty("localSocket",   LocalSocket. ToString()),
                                 new JProperty("remoteSocket",  RemoteSocket.ToString()),

                           customData.IsEmpty
                               ? null
                               : new JProperty("customData",    JSONObject.Create(
                                                                    customData.Select(kvp => kvp.Value is not null
                                                                                                 ? new JProperty(kvp.Key, kvp.Value.ToString())
                                                                                                 : null)
                                                                ))

                       );

            return CustomWebSocketConnectionSerializer is not null
                       ? CustomWebSocketConnectionSerializer(this, json)
                       : json;

        }


        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        /// <param name="CustomWebSocketServerConnectionSerializer">A delegate to serialize custom HTTP WebSocket server connections.</param>
        public JObject ToJSON(CustomJObjectSerializerDelegate<WebSocketServerConnection>? CustomWebSocketServerConnectionSerializer)
        {

            var json = JSONObject.Create(

                                 new JProperty("localSocket",   LocalSocket. ToString()),
                                 new JProperty("remoteSocket",  RemoteSocket.ToString()),

                           customData.IsEmpty
                               ? null
                               : new JProperty("customData",    JSONObject.Create(
                                                                    customData.Select(kvp => kvp.Value is not null
                                                                                                 ? new JProperty(kvp.Key, kvp.Value.ToString())
                                                                                                 : null)
                                                                ))

                       );

            return CustomWebSocketServerConnectionSerializer is not null
                       ? CustomWebSocketServerConnectionSerializer(this, json)
                       : json;

        }

        #endregion


        #region Operator overloading

        #region Operator == (WebSocketConnection1, WebSocketConnection2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="WebSocketConnection1">A HTTP web socket server connection.</param>
        /// <param name="WebSocketConnection2">Another HTTP web socket server connection.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (WebSocketServerConnection? WebSocketConnection1,
                                           WebSocketServerConnection? WebSocketConnection2)
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
        /// <param name="WebSocketConnection1">A HTTP web socket server connection.</param>
        /// <param name="WebSocketConnection2">Another HTTP web socket server connection.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (WebSocketServerConnection? WebSocketConnection1,
                                           WebSocketServerConnection? WebSocketConnection2)

            => !(WebSocketConnection1 == WebSocketConnection2);

        #endregion

        #region Operator <  (WebSocketConnection1, WebSocketConnection2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="WebSocketConnection1">A HTTP web socket server connection.</param>
        /// <param name="WebSocketConnection2">Another HTTP web socket server connection.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (WebSocketServerConnection? WebSocketConnection1,
                                          WebSocketServerConnection? WebSocketConnection2)
        {

            if (WebSocketConnection1 is null)
                throw new ArgumentNullException(nameof(WebSocketConnection2), "The given HTTP web socket server connection must not be null!");

            return WebSocketConnection1.CompareTo(WebSocketConnection2) < 0;

        }

        #endregion

        #region Operator <= (WebSocketConnection1, WebSocketConnection2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="WebSocketConnection1">A HTTP web socket server connection.</param>
        /// <param name="WebSocketConnection2">Another HTTP web socket server connection.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (WebSocketServerConnection? WebSocketConnection1,
                                           WebSocketServerConnection? WebSocketConnection2)

            => !(WebSocketConnection1 > WebSocketConnection2);

        #endregion

        #region Operator >  (WebSocketConnection1, WebSocketConnection2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="WebSocketConnection1">A HTTP web socket server connection.</param>
        /// <param name="WebSocketConnection2">Another HTTP web socket server connection.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (WebSocketServerConnection? WebSocketConnection1,
                                          WebSocketServerConnection? WebSocketConnection2)
        {

            if (WebSocketConnection1 is null)
                throw new ArgumentNullException(nameof(WebSocketConnection2), "The given HTTP web socket server connection must not be null!");

            return WebSocketConnection1.CompareTo(WebSocketConnection2) > 0;

        }

        #endregion

        #region Operator >= (WebSocketConnection1, WebSocketConnection2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="WebSocketConnection1">A HTTP web socket server connection.</param>
        /// <param name="WebSocketConnection2">Another HTTP web socket server connection.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (WebSocketServerConnection? WebSocketConnection1,
                                           WebSocketServerConnection? WebSocketConnection2)

            => !(WebSocketConnection1 < WebSocketConnection2);

        #endregion

        #endregion

        #region IComparable<WebSocketConnection> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two HTTP web socket server connections.
        /// </summary>
        /// <param name="Object">A HTTP web socket server connection to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is WebSocketServerConnection webSocketServerConnection
                   ? CompareTo(webSocketServerConnection)
                   : throw new ArgumentException("The given object is not a HTTP web socket server connection!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(WebSocketConnection)

        /// <summary>
        /// Compares two HTTP web socket server connections.
        /// </summary>
        /// <param name="WebSocketConnection">A HTTP web socket server connection to compare with.</param>
        public Int32 CompareTo(WebSocketServerConnection? WebSocketConnection)
        {

            if (WebSocketConnection is null)
                throw new ArgumentNullException(nameof(WebSocketConnection), "The given HTTP web socket server connection must not be null!");

            return LocalSocket.CompareTo(WebSocketConnection.LocalSocket);

        }

        #endregion

        #endregion

        #region IEquatable<WebSocketConnection> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two HTTP web socket server connections for equality.
        /// </summary>
        /// <param name="Object">A HTTP web socket server connection to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is WebSocketServerConnection webSocketServerConnection &&
                   Equals(webSocketServerConnection);

        #endregion

        #region Equals(WebSocketConnection)

        /// <summary>
        /// Compares two HTTP web socket server connections for equality.
        /// </summary>
        /// <param name="WebSocketConnection">A HTTP web socket server connection to compare with.</param>
        public Boolean Equals(WebSocketServerConnection? WebSocketConnection)

            => WebSocketConnection is not null &&
                   LocalSocket.Equals(WebSocketConnection.LocalSocket);

        #endregion

        #endregion

        #region (override) GetHashCode()

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
