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

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Messaging;

using static org.GraphDefined.Vanaheimr.Hermod.WebSocket.WebSocketFrame;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{

    /// <summary>
    /// The common interface for all HTTP WebSockets connections.
    /// </summary>
    /// <remarks>
    /// A <see cref="IMessageConnection"/> whose <see cref="IMessageConnection.Transport"/> is
    /// <see cref="MessageTransport.WebSocket"/>: what it has in common with other message connections -
    /// sockets, counters, custom data, sending and closing - is declared there.
    /// </remarks>
    public interface IWebSocketConnection : IMessageConnection
    {

        #region Metadata

        /// <summary>
        /// The optional HTTP request of this web socket connection. Can also be attached later.
        /// </summary>
        HTTPRequest?             HTTPRequest                   { get; }

        /// <summary>
        /// The optional HTTP response of this web socket connection. Can also be attached later.
        /// </summary>
        HTTPResponse?            HTTPResponse                  { get; }

        /// <summary>
        /// The last time data was sent.
        /// </summary>
        new DateTimeOffset?      LastSentTimestamp             { get; set; }

        /// <summary>
        /// The last time data was received.
        /// </summary>
        new DateTimeOffset?      LastReceivedTimestamp         { get; set; }

        /// <summary>
        /// Whether the connection is still assumed to be alive.
        /// </summary>
        new Boolean              IsAlive                       { get; set; }

        /// <summary>
        /// The number of WebSocket frames received.
        /// </summary>
        UInt64                   FramesReceivedCounter         { get; }

        /// <summary>
        /// The number of WebSocket frames sent.
        /// </summary>
        UInt64                   FramesSentCounter             { get; }

        UInt64?                  MaxTextMessageSizeIn          { get; set; }
        UInt64?                  MaxTextMessageSizeOut         { get; set; }
        UInt64?                  MaxTextFragmentLengthIn       { get; set; }
        UInt64?                  MaxTextFragmentLengthOut      { get; set; }

        UInt64?                  MaxBinaryMessageSizeIn        { get; set; }
        UInt64?                  MaxBinaryMessageSizeOut       { get; set; }
        UInt64?                  MaxBinaryFragmentLengthIn     { get; set; }
        UInt64?                  MaxBinaryFragmentLengthOut    { get; set; }


        /// <summary>
        /// For debugging reasons data can be send really really slow...
        /// </summary>
        TimeSpan?                SlowNetworkSimulationDelay    { get; }

        #endregion

        #region Sockets

        /// <summary>
        /// The amount of time a read operation blocks waiting for data.
        /// </summary>
        TimeSpan                 ReadTimeout                   { get; set; }

        /// <summary>
        /// The amount of time a write operation blocks before failing.
        /// </summary>
        TimeSpan                 WriteTimeout                  { get; set; }

        /// <summary>
        /// Whether data on the network stream is available for reading.
        /// </summary>
        Boolean                  DataAvailable                 { get; }

        /// <summary>
        /// The amount of data on the network stream available for reading.
        /// </summary>
        Int32                    Available                     { get; }

        #endregion


        #region Send               (Data,           CancellationToken = default)

        /// <summary>
        /// Send the given array of bytes.
        /// </summary>
        /// <param name="Data">The array of bytes to send.</param>
        /// <param name="CancellationToken">An optional cancellation token to cancel this request.</param>
        Task<SentStatus> Send(Byte[]             Data,
                              CancellationToken  CancellationToken   = default);

        #endregion

        #region SendWebSocketFrame (WebSocketFrame, CancellationToken = default)

        /// <summary>
        /// Send the given web socket frame.
        /// </summary>
        /// <param name="WebSocketFrame">A web socket frame.</param>
        /// <param name="CancellationToken">An optional cancellation token to cancel this request.</param>
        Task<SentStatus> SendWebSocketFrame(WebSocketFrame     WebSocketFrame,
                                            CancellationToken  CancellationToken   = default);

        #endregion


        #region IncMessagesReceivedCounter()

        /// <summary>
        /// Increment the number of WebSocket messages received.
        /// </summary>
        void IncMessagesReceivedCounter();

        #endregion

        #region IncFramesReceivedCounter()

        /// <summary>
        /// Increment the number of WebSocket frames received.
        /// </summary>
        void IncFramesReceivedCounter();

        #endregion

        #region Read(Buffer, Offset, Count)

        /// <summary>
        /// Tries to reads the given amount of data from the network stream.
        /// </summary>
        /// <param name="Buffer">The byte array for storing the received data.</param>
        /// <param name="Offset">An offset within the byte array.</param>
        /// <param name="Count">The maximum number of bytes to read and store.</param>
        /// <returns>Number of bytes read, or 0.</returns>
        UInt32 Read(Byte[]  Buffer,
                    UInt32  Offset,
                    UInt32  Count);

        /// <summary>
        /// Tries to reads the given amount of data from the network stream.
        /// </summary>
        /// <param name="Buffer">The byte array for storing the received data.</param>
        /// <param name="Offset">An offset within the byte array.</param>
        /// <param name="Count">The maximum number of bytes to read and store.</param>
        /// <returns>Number of bytes read, or 0.</returns>
        UInt32 Read(Byte[]  Buffer,
                    Int32   Offset,
                    Int32   Count);

        #endregion

        #region Close(StatusCode = NormalClosure, Reason = null, CancellationToken = default)

        /// <summary>
        /// Close this web socket connection.
        /// When a status code or reason is given, a HTTP WebSocket close frame will be sent.
        /// </summary>
        /// <param name="StatusCode">An optional closing status code for the HTTP WebSocket close frame.</param>
        /// <param name="Reason">An optional closing reason for the HTTP WebSocket close frame.</param>
        /// <param name="CancellationToken">An optional cancellation token to cancel this request.</param>
        Task Close(ClosingStatusCode? StatusCode          = ClosingStatusCode.NormalClosure,
                   String?            Reason              = null,
                   CancellationToken  CancellationToken   = default);

        #endregion


        #region ToJSON(CustomWebSocketConnectionSerializer = null)

        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        /// <param name="CustomWebSocketConnectionSerializer">A delegate to serialize custom HTTP WebSocket connections.</param>
        public JObject ToJSON(CustomJObjectSerializerDelegate<IWebSocketConnection>? CustomWebSocketConnectionSerializer = null);

        #endregion


    }

}
