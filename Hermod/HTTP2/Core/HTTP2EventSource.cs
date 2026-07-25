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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP2
{

    using System.Diagnostics.Tracing;


    /// <summary>
    /// Everything this stack has to say about itself, as structured events rather
    /// than console text.
    ///
    /// <see cref="EventSource"/> is in the BCL, so a library that emits through it
    /// stays dependency-free — which matters here, since taking a logging
    /// abstraction would otherwise be the first thing to break this project's
    /// no-NuGet rule. Consumers attach an <see cref="EventListener"/>,
    /// <c>dotnet-counters</c>, or an OpenTelemetry exporter; nothing is written
    /// anywhere unless somebody is listening.
    ///
    /// The events worth having are not only the errors. The abuse defences —
    /// rapid-reset trips, CONTINUATION floods, timeout kills — previously detected
    /// their conditions and then told nobody but stdout, which made them
    /// unobservable in exactly the situations they exist for.
    /// </summary>
    [EventSource(Name = "Vanaheimr-Hermod-HTTP2")]
    public sealed class HTTP2EventSource : EventSource
    {

        #region Data

        /// <summary>The singleton every part of the stack writes to.</summary>
        public static readonly HTTP2EventSource Log = new();

        private IncrementingEventCounter? connectionsStarted;
        private IncrementingEventCounter? requestsHandled;
        private IncrementingEventCounter? streamsReset;
        private IncrementingEventCounter? connectionErrors;
        private IncrementingEventCounter? abuseDetected;

        private HTTP2EventSource() { }

        #endregion

        #region Connection lifecycle

        [Event(1, Level = EventLevel.Informational, Message = "Listening on {0}")]
        public void ServerListening(String Endpoint)
        {
            if (IsEnabled())
                WriteEvent(1, Endpoint);
        }

        [Event(2, Level = EventLevel.Verbose, Message = "Accepted {1} connection from {0}")]
        public void ConnectionAccepted(String RemoteEndPoint, String Transport)
        {
            if (IsEnabled())
                WriteEvent(2, RemoteEndPoint, Transport);
            connectionsStarted?.Increment();
        }

        /// <summary>
        /// The negotiated security parameters, which are otherwise invisible after
        /// the handshake — and are what an operator needs to answer "is anyone still
        /// reaching us over TLS 1.2, and with what?".
        /// </summary>
        [Event(3, Level = EventLevel.Informational, Message = "{0}: {1} over {2} ({3})")]
        public void ConnectionEstablished(String RemoteEndPoint, String Alpn, String TlsProtocol, String CipherSuite)
        {
            if (IsEnabled())
                WriteEvent(3, RemoteEndPoint, Alpn, TlsProtocol, CipherSuite);
        }

        [Event(4, Level = EventLevel.Verbose, Message = "Connection closed: {0}")]
        public void ConnectionClosed(String RemoteEndPoint)
        {
            if (IsEnabled())
                WriteEvent(4, RemoteEndPoint);
        }

        [Event(5, Level = EventLevel.Error, Message = "Connection failed with {0}: {1}")]
        public void ConnectionFailed(String RemoteEndPoint, String Reason)
        {
            if (IsEnabled())
                WriteEvent(5, RemoteEndPoint, Reason);
            connectionErrors?.Increment();
        }

        /// <summary>A connection turned away before HTTP/2 began — e.g. RFC 9113 §9.2.2.</summary>
        [Event(6, Level = EventLevel.Warning, Message = "Rejected {0}: {1}")]
        public void ConnectionRejected(String RemoteEndPoint, String Reason)
        {
            if (IsEnabled())
                WriteEvent(6, RemoteEndPoint, Reason);
            connectionErrors?.Increment();
        }

        [Event(7, Level = EventLevel.Informational, Message = "ALPN negotiated {1} with {0}")]
        public void AlpnNegotiated(String RemoteEndPoint, String Protocol)
        {
            if (IsEnabled())
                WriteEvent(7, RemoteEndPoint, Protocol);
        }

        #endregion

        #region Protocol errors

        [Event(8, Level = EventLevel.Error, Message = "Connection error {0}: {1}")]
        public void ConnectionError(String ErrorCode, String Message)
        {
            if (IsEnabled())
                WriteEvent(8, ErrorCode, Message);
            connectionErrors?.Increment();
        }

        [Event(9, Level = EventLevel.Warning, Message = "Stream {0} error {1}: {2}")]
        public void StreamError(Int32 StreamId, String ErrorCode, String Message)
        {
            if (IsEnabled())
                WriteEvent(9, StreamId, ErrorCode, Message);
            streamsReset?.Increment();
        }

        [Event(10, Level = EventLevel.Informational, Message = "Stream {0} reset by peer: {1}")]
        public void StreamResetByPeer(Int32 StreamId, String ErrorCode)
        {
            if (IsEnabled())
                WriteEvent(10, StreamId, ErrorCode);
            streamsReset?.Increment();
        }

        [Event(11, Level = EventLevel.Informational, Message = "GOAWAY received: lastStream={0} error={1} debug=\"{2}\"")]
        public void GoAwayReceived(Int32 LastStreamId, String ErrorCode, String DebugData)
        {
            if (IsEnabled())
                WriteEvent(11, LastStreamId, ErrorCode, DebugData);
        }

        /// <summary>
        /// An abuse defence fired: Rapid Reset, a CONTINUATION flood, an
        /// unproductive-frame flood, stream-ID exhaustion, a Slowloris timeout. The
        /// one class of event whose absence from any log at all was a real gap.
        /// </summary>
        [Event(12, Level = EventLevel.Warning, Message = "Abuse defence '{0}' fired: {1}")]
        public void AbuseDetected(String Defence, String Detail)
        {
            if (IsEnabled())
                WriteEvent(12, Defence, Detail);
            abuseDetected?.Increment();
        }

        #endregion

        #region Application handlers

        [Event(13, Level = EventLevel.Error, Message = "{1} handler failed on stream {0}: {2}")]
        public void HandlerFailed(Int32 StreamId, String Kind, String Message)
        {
            if (IsEnabled())
                WriteEvent(13, StreamId, Kind, Message);
        }

        [Event(14, Level = EventLevel.Verbose, Message = "{1} handler on stream {0} cancelled")]
        public void HandlerCancelled(Int32 StreamId, String Kind)
        {
            if (IsEnabled())
                WriteEvent(14, StreamId, Kind);
        }

        [Event(15, Level = EventLevel.Verbose, Message = "Stream {0}: {1} {2} -> {3}")]
        public void RequestHandled(Int32 StreamId, String Method, String Path, Int32 Status)
        {
            if (IsEnabled())
                WriteEvent(15, StreamId, Method, Path, Status);
            requestsHandled?.Increment();
        }

        #endregion

        #region Counters

        /// <summary>
        /// Counters are created on first subscription rather than at construction —
        /// an unobserved <see cref="EventCounter"/> still costs a timer, and a
        /// library should cost nothing when nobody is watching.
        /// </summary>
        protected override void OnEventCommand(EventCommandEventArgs Command)
        {

            if (Command.Command != EventCommand.Enable)
                return;

            connectionsStarted ??= new IncrementingEventCounter("connections-started", this) {
                                       DisplayName = "Connections started"
                                   };

            requestsHandled    ??= new IncrementingEventCounter("requests-handled", this) {
                                       DisplayName = "Requests handled"
                                   };

            streamsReset       ??= new IncrementingEventCounter("streams-reset", this) {
                                       DisplayName = "Streams reset"
                                   };

            connectionErrors   ??= new IncrementingEventCounter("connection-errors", this) {
                                       DisplayName = "Connection errors"
                                   };

            abuseDetected      ??= new IncrementingEventCounter("abuse-detected", this) {
                                       DisplayName = "Abuse defences fired"
                                   };

        }

        #endregion

    }

}
