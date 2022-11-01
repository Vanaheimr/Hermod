/*
 * Copyright (c) 2010-2022 GraphDefined GmbH <achim.friedland@graphdefined.com>
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
using System.Diagnostics.CodeAnalysis;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{

    /// <summary>
    /// A HTTP web socket connection.
    /// </summary>
    public class WebSocketConnection : IEquatable<WebSocketConnection>
    {

        #region Data

        private readonly Dictionary<String, Object?> customData;

        #endregion

        #region Properties

        /// <summary>
        /// The creation timestamp.
        /// </summary>
        public DateTime                 Created                    { get; }

        public CancellationTokenSource  CancellationTokenSource    { get; }

        /// <summary>
        /// The HTTP web socket server.
        /// </summary>
        public WebSocketServer          WebSocketServer            { get; }

        /// <summary>
        /// The TCP connection abstraction.
        /// </summary>
        public TcpClient                TcpClient                  { get; }

        /// <summary>
        /// The network stream of the TCP connection abstraction.
        /// </summary>
        public NetworkStream?           TCPStream                  { get; internal set; }

        /// <summary>
        /// The local TCP socket.
        /// </summary>
        public IPSocket                 LocalSocket                { get; }

        /// <summary>
        /// The remote TCP socket.
        /// </summary>
        public IPSocket                 RemoteSocket               { get; }

        /// <summary>
        /// The optional HTTP request of this web socket connection. Can also be attached later.
        /// </summary>
        public HTTPRequest?             Request                    { get; internal set; }

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
                                   HTTPRequest?                                 Request      = null,
                                   IEnumerable<KeyValuePair<String, Object?>>?  CustomData   = null)
        {

            this.Created                  = Timestamp.Now;
            this.CancellationTokenSource  = new CancellationTokenSource();
            this.WebSocketServer          = WebSocketServer;
            this.TcpClient                = TcpClient;
            this.TCPStream                = TcpClient.GetStream();
            this.LocalSocket              = IPSocket.FromIPEndPoint(TcpClient.Client.LocalEndPoint!);
            this.RemoteSocket             = IPSocket.FromIPEndPoint(TcpClient.Client.RemoteEndPoint!);
            this.Request                  = Request;
            this.customData               = CustomData is not null
                                                ? CustomData. ToDictionary(kvp => kvp.Key,
                                                                           kvp => kvp.Value)
                                                : new Dictionary<String, Object?>();

        }

        #endregion





        public void SendWebSocketFrame(WebSocketFrame  webSocketFrame)
        {

            if (TCPStream is not null)
            {
                lock (TCPStream)
                {
                    try
                    {
                        TCPStream.Write(webSocketFrame.ToByteArray());
                        TCPStream.Flush();
                    }
                    catch (Exception e)
                    {
                        DebugX.LogException(e, "Sending a web socket frame in " + nameof(WebSocketServer));
                    }
                }
            }

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
                   : throw new ArgumentException("The given object is not an HTTP web socket connection!",
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
