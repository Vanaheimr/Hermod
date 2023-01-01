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

    public enum SendStatus
    {
        Success,
        Error,
        FatalError
    }

    /// <summary>
    /// A HTTP web socket connection.
    /// </summary>
    public class WebSocketConnection : IEquatable<WebSocketConnection>
    {

        #region Data

        private readonly  Dictionary<String, Object?>  customData;

        private readonly  TcpClient                    tcpClient;

        private readonly  NetworkStream                tcpStream;

        public  volatile  Boolean                      IsClosed;

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
        public WebSocketServer          WebSocketServer               { get; }

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
        public HTTPRequest?             Request                       { get; internal set; }

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
                    return tcpClient.Available;
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
        /// Create a new HTTP web socket connection.
        /// </summary>
        /// <param name="WebSocketServer">A HTTP web socket server.</param>
        /// <param name="TcpClient">A TCP connection abstraction.</param>
        /// <param name="Request">An optional HTTP request of this web socket connection. Can also be attached later.</param>
        /// <param name="CustomData">Optional custom data to be stored within this web socket connection.</param>
        public WebSocketConnection(WebSocketServer                              WebSocketServer,
                                   TcpClient                                    TcpClient,
                                   HTTPRequest?                                 Request                      = null,
                                   IEnumerable<KeyValuePair<String, Object?>>?  CustomData                   = null,
                                   TimeSpan?                                    SlowNetworkSimulationDelay   = null)
        {

            this.Created                     = Timestamp.Now;
            this.CancellationTokenSource     = new CancellationTokenSource();
            this.WebSocketServer             = WebSocketServer;
            this.tcpClient                   = TcpClient;
            this.tcpStream                   = TcpClient.GetStream();
            this.LocalSocket                 = IPSocket.FromIPEndPoint(TcpClient.Client.LocalEndPoint!);
            this.RemoteSocket                = IPSocket.FromIPEndPoint(TcpClient.Client.RemoteEndPoint!);
            this.Request                     = Request;
            this.customData                  = CustomData is not null
                                                   ? CustomData. ToDictionary(kvp => kvp.Key,
                                                                              kvp => kvp.Value)
                                                   : new Dictionary<String, Object?>();
            this.SlowNetworkSimulationDelay  = SlowNetworkSimulationDelay;

        }

        #endregion


        #region (private) SendData_private(Data)

        /// <summary>
        /// Send the given array of bytes.
        /// </summary>
        /// <param name="Data">The array of bytes to send.</param>
        private SendStatus SendData_private(Byte[] Data)
        {

            lock (tcpStream)
            {
                try
                {

                    if (SlowNetworkSimulationDelay.HasValue)
                    {
                        foreach (var singleByte in Data)
                        {
                            tcpStream.Write(new Byte[] { singleByte });
                            tcpStream.Flush();
                            Thread.Sleep(SlowNetworkSimulationDelay.Value);
                        }
                    }

                    else
                    {
                        tcpStream.Write(Data);
                        tcpStream.Flush();
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

                    DebugX.LogException(e, "Sending data within web socket connection " + RemoteSocket);

                }
            }

            return SendStatus.Error;

        }

        //public async Task SendWebSocketFrameAsync(WebSocketFrame  webSocketFrame)
        //{

        //    if (TCPStream is not null)
        //    {
        //        lock (TCPStream)
        //        {
        //            try
        //            {
        //                await TCPStream.WriteAsync(webSocketFrame.ToByteArray());
        //                await TCPStream.FlushAsync();
        //            }
        //            catch (Exception e)
        //            {
        //                DebugX.LogException(e, "Sending a web socket frame in " + nameof(WebSocketServer));
        //            }
        //        }
        //    }

        //}

        #endregion

        #region SendData(Data)

        /// <summary>
        /// Send the given array of bytes.
        /// </summary>
        /// <param name="Data">The array of bytes to send.</param>
        public SendStatus SendData(Byte[] Data)

            => SendData_private(Data);

        #endregion

        #region SendText(SendText)

        /// <summary>
        /// Send the given text.
        /// </summary>
        /// <param name="SendText">A text to send.</param>
        public SendStatus SendText(String SendText)

            => SendData_private(SendText.ToUTF8Bytes());

        #endregion

        #region SendWebSocketFrame(WebSocketFrame)

        /// <summary>
        /// Send the given web socket frame.
        /// </summary>
        /// <param name="WebSocketFrame">A web socket frame.</param>
        public SendStatus SendWebSocketFrame(WebSocketFrame WebSocketFrame)

            => SendData_private(WebSocketFrame.ToByteArray());

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

                tcpClient.Close();

                IsClosed = true;

            }
            catch (Exception e)
            {
                DebugX.Log(String.Concat(nameof(WebSocketConnection), ".", nameof(Close), ": Exception occured: ", e.Message));
            }
        }

        #endregion


        // Custom data

        #region AddCustomData(Key, Value)

        /// <summary>
        /// Add custom data to this HTTP web socket connection.
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
        /// Checks whether the given data key is present within this HTTP web socket connection.
        /// </summary>
        /// <param name="Key">A key.</param>
        public Boolean HasCustomData(String Key)
            => customData.ContainsKey(Key);

        #endregion


        #region TryGetCustomData  (Key)

        /// <summary>
        /// Tries to return the data associated with the given key from this HTTP web socket connection.
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
        /// Tries to return the data associated with the given key from this HTTP web socket connection.
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
        /// Tries to return the data associated with the given key from this HTTP web socket connection.
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
        /// Tries to return the data associated with the given key from this HTTP web socket connection.
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



        #region Operator overloading

        #region Operator == (WebSocketConnection1, WebSocketConnection2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="WebSocketConnection1">A HTTP web socket connection.</param>
        /// <param name="WebSocketConnection2">Another HTTP web socket connection.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (WebSocketConnection WebSocketConnection1,
                                           WebSocketConnection WebSocketConnection2)
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
        /// <param name="WebSocketConnection1">A HTTP web socket connection.</param>
        /// <param name="WebSocketConnection2">Another HTTP web socket connection.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (WebSocketConnection WebSocketConnection1,
                                           WebSocketConnection WebSocketConnection2)

            => !(WebSocketConnection1 == WebSocketConnection2);

        #endregion

        #region Operator <  (WebSocketConnection1, WebSocketConnection2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="WebSocketConnection1">A HTTP web socket connection.</param>
        /// <param name="WebSocketConnection2">Another HTTP web socket connection.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (WebSocketConnection WebSocketConnection1,
                                          WebSocketConnection WebSocketConnection2)
        {

            if (WebSocketConnection1 is null)
                throw new ArgumentNullException(nameof(WebSocketConnection2), "The given HTTP web socket connection must not be null!");

            return WebSocketConnection1.CompareTo(WebSocketConnection2) < 0;

        }

        #endregion

        #region Operator <= (WebSocketConnection1, WebSocketConnection2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="WebSocketConnection1">A HTTP web socket connection.</param>
        /// <param name="WebSocketConnection2">Another HTTP web socket connection.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (WebSocketConnection WebSocketConnection1,
                                           WebSocketConnection WebSocketConnection2)

            => !(WebSocketConnection1 > WebSocketConnection2);

        #endregion

        #region Operator >  (WebSocketConnection1, WebSocketConnection2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="WebSocketConnection1">A HTTP web socket connection.</param>
        /// <param name="WebSocketConnection2">Another HTTP web socket connection.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (WebSocketConnection WebSocketConnection1,
                                          WebSocketConnection WebSocketConnection2)
        {

            if (WebSocketConnection1 is null)
                throw new ArgumentNullException(nameof(WebSocketConnection2), "The given HTTP web socket connection must not be null!");

            return WebSocketConnection1.CompareTo(WebSocketConnection2) > 0;

        }

        #endregion

        #region Operator >= (WebSocketConnection1, WebSocketConnection2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="WebSocketConnection1">A HTTP web socket connection.</param>
        /// <param name="WebSocketConnection2">Another HTTP web socket connection.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (WebSocketConnection WebSocketConnection1,
                                           WebSocketConnection WebSocketConnection2)

            => !(WebSocketConnection1 < WebSocketConnection2);

        #endregion

        #endregion

        #region IComparable<WebSocketConnection> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two HTTP web socket connections.
        /// </summary>
        /// <param name="Object">A HTTP web socket connection to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is WebSocketConnection webSocketConnection
                   ? CompareTo(webSocketConnection)
                   : throw new ArgumentException("The given object is not a HTTP web socket connection!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(WebSocketConnection)

        /// <summary>
        /// Compares two HTTP web socket connections.
        /// </summary>
        /// <param name="WebSocketConnection">A HTTP web socket connection to compare with.</param>
        public Int32 CompareTo(WebSocketConnection? WebSocketConnection)
        {

            if (WebSocketConnection is null)
                throw new ArgumentNullException(nameof(WebSocketConnection), "The given HTTP web socket connection must not be null!");

            return LocalSocket.CompareTo(WebSocketConnection.LocalSocket);

        }

        #endregion

        #endregion

        #region IEquatable<WebSocketConnection> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two HTTP web socket connections for equality.
        /// </summary>
        /// <param name="Object">A HTTP web socket connection to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is WebSocketConnection webSocketConnection &&
                   Equals(webSocketConnection);

        #endregion

        #region Equals(WebSocketConnection)

        /// <summary>
        /// Compares two HTTP web socket connections for equality.
        /// </summary>
        /// <param name="WebSocketConnection">A HTTP web socket connection to compare with.</param>
        public Boolean Equals(WebSocketConnection? WebSocketConnection)

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

            => String.Concat(LocalSocket,
                             " <=> ",
                             RemoteSocket);

        #endregion


    }

}
