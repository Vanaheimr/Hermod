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

using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Messaging
{

    /// <summary>
    /// What carries the messages of a <see cref="IMessageConnection"/>.
    /// </summary>
    public enum MessageTransport
    {

        /// <summary>
        /// An HTTP WebSocket (RFC 6455): see <see cref="IWebSocketConnection"/>.
        /// </summary>
        WebSocket,

        /// <summary>
        /// A message channel within an SSH session (a subsystem): see <c>ISshMessageConnection</c>.
        /// </summary>
        SSH

    }


    /// <summary>
    /// Which end of a <see cref="IMessageConnection"/> this one is.
    /// </summary>
    public enum ConnectionRole
    {

        /// <summary>
        /// This end opened the connection: a client of the other.
        /// </summary>
        Client,

        /// <summary>
        /// The other end opened the connection, and this end accepted it: a server.
        /// </summary>
        Server

    }


    /// <summary>
    /// Why a <see cref="IMessageConnection"/> is closed. The numbers are those of RFC 6455 (section 7.4.1),
    /// so that the same reason reads the same over any transport.
    /// </summary>
    public enum MessageCloseCode : UInt16
    {

        /// <summary>
        /// The purpose of the connection has been fulfilled.
        /// </summary>
        NormalClosure       = 1000,

        /// <summary>
        /// This end goes away: the server is stopping, or the client leaves.
        /// </summary>
        GoingAway           = 1001,

        /// <summary>
        /// The other end broke the protocol.
        /// </summary>
        ProtocolError       = 1002,

        /// <summary>
        /// The other end sent a kind of message this end does not accept.
        /// </summary>
        UnsupportedData     = 1003,

        /// <summary>
        /// A message's content did not match its kind, e.g. a text message that is not UTF-8.
        /// </summary>
        InvalidPayloadData  = 1007,

        /// <summary>
        /// The other end did something this end's policy does not allow.
        /// </summary>
        PolicyViolation     = 1008,

        /// <summary>
        /// A message was larger than this end accepts.
        /// </summary>
        MessageTooBig       = 1009,

        /// <summary>
        /// This end failed while serving the connection.
        /// </summary>
        InternalServerError = 1011

    }


    /// <summary>
    /// A connection that carries whole messages, text or binary, in both directions - whatever carries
    /// them. The common part of an HTTP WebSocket (<see cref="IWebSocketConnection"/>) and a message
    /// channel within an SSH session (<c>ISshMessageConnection</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Transport"/> and <see cref="Role"/> tell which kind of connection this is without a
    /// type test. To get at what only one kind has - the HTTP request of a WebSocket, the SSH login of a
    /// channel - test for the kind's interface, or use <see cref="MessageConnectionExtensions.TryGetWebSocket"/>
    /// and <see cref="MessageConnectionExtensions.TryGetSSH"/>.
    /// </para>
    /// <para>
    /// A message sent here is sent as the server or client the connection belongs to sends it: with
    /// its events, its logging and its limits.
    /// </para>
    /// </remarks>
    public interface IMessageConnection
    {

        #region Kind

        /// <summary>
        /// What carries the messages: a WebSocket, or an SSH channel.
        /// </summary>
        MessageTransport  Transport                  { get; }

        /// <summary>
        /// Whether this end opened the connection (client) or accepted it (server).
        /// </summary>
        ConnectionRole    Role                       { get; }

        /// <summary>
        /// The subprotocol both ends agreed on, e.g. "ocpp2.1" - or null when none was asked for, or
        /// none has been agreed yet.
        /// </summary>
        String?           Subprotocol                { get; }

        #endregion

        #region Sockets and state

        /// <summary>
        /// Where this end of the connection is.
        /// </summary>
        IPSocket          LocalSocket                { get; }

        /// <summary>
        /// Where the other end of the connection is.
        /// </summary>
        IPSocket          RemoteSocket               { get; }

        /// <summary>
        /// When the connection was made.
        /// </summary>
        DateTimeOffset    ConnectedSince             { get; }

        /// <summary>
        /// When a message was last sent, if one was.
        /// </summary>
        DateTimeOffset?   LastSentTimestamp          { get; }

        /// <summary>
        /// When a message was last received, if one was.
        /// </summary>
        DateTimeOffset?   LastReceivedTimestamp      { get; }

        /// <summary>
        /// Whether the connection is still thought to be alive.
        /// </summary>
        Boolean           IsAlive                    { get; }

        /// <summary>
        /// The number of messages received.
        /// </summary>
        UInt64            MessagesReceivedCounter    { get; }

        /// <summary>
        /// The number of messages sent.
        /// </summary>
        UInt64            MessagesSentCounter        { get; }

        #endregion

        #region Send and close

        /// <summary>
        /// Send a text message.
        /// </summary>
        /// <param name="Text">The text.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        Task<SentStatus> SendTextMessage  (String             Text,
                                           CancellationToken  CancellationToken   = default);

        /// <summary>
        /// Send a binary message.
        /// </summary>
        /// <param name="Bytes">The bytes.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        Task<SentStatus> SendBinaryMessage(Byte[]             Bytes,
                                           CancellationToken  CancellationToken   = default);

        /// <summary>
        /// Close the connection, telling the other end why.
        /// </summary>
        /// <param name="Code">Why the connection is closed.</param>
        /// <param name="Reason">An optional text for the other end.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        Task Close(MessageCloseCode   Code,
                   String?            Reason              = null,
                   CancellationToken  CancellationToken   = default);

        #endregion

        #region Custom data

        /// <summary>
        /// Add a value under the given key, unless the key has one already.
        /// </summary>
        Boolean TryAddCustomData(String   Key,
                                 Object?  Value);

        /// <summary>
        /// Replace the value under the given key, where it is the given comparison value.
        /// </summary>
        Boolean TryAddCustomData(String   Key,
                                 Object?  NewValue,
                                 Object?  ComparisonValue);

        /// <summary>
        /// Whether there is a value under the given key.
        /// </summary>
        Boolean HasCustomData(String Key);

        /// <summary>
        /// The value under the given key, or null.
        /// </summary>
        Object? TryGetCustomData(String Key);

        /// <summary>
        /// The value under the given key as the given value type, or null.
        /// </summary>
        T? TryGetCustomDataAs<T>(String Key)
            where T : struct;

        /// <summary>
        /// The value under the given key, if there is one.
        /// </summary>
        Boolean TryGetCustomData(String       Key,
                                 out Object?  Value);

        /// <summary>
        /// The value under the given key as the given type, if there is one of that type.
        /// </summary>
        Boolean TryGetCustomDataAs<T>(String  Key,
                                      out T?  Value);

        #endregion

    }

}
