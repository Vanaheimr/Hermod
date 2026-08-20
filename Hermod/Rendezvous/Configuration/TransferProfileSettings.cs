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

namespace org.GraphDefined.Vanaheimr.Hermod.Rendezvous
{

    /// <summary>
    /// The buffer sizes and TCP parameters used for all sockets of a rendezvous.
    /// Every value can be overridden via configuration, the defaults are derived
    /// from the requested <see cref="TransferProfile"/>.
    /// </summary>
    public sealed class TransferProfileSettings
    {

        #region Properties

        /// <summary>
        /// Whether to disable the Nagle algorithm (TCP_NODELAY).
        /// Small writes are then sent immediately instead of being coalesced,
        /// which removes up to 40 ms of latency per message.
        /// </summary>
        public Boolean    NoDelay                   { get; set; } = true;

        /// <summary>
        /// The size of the application level relay buffer in bytes.
        /// Larger buffers mean fewer system calls per transferred megabyte,
        /// smaller buffers mean that a single read/write pair adds less latency.
        /// </summary>
        public Int32      RelayBufferSize           { get; set; } = 64 * 1024;

        /// <summary>
        /// An optional explicit SO_RCVBUF size in bytes.
        /// A null value keeps the operating system default, which also keeps the
        /// receive window auto-tuning enabled - usually the better choice for
        /// bulk transfers over long fat networks.
        /// </summary>
        public Int32?     SocketReceiveBufferSize   { get; set; }

        /// <summary>
        /// An optional explicit SO_SNDBUF size in bytes.
        /// A null value keeps the operating system default (see above).
        /// </summary>
        public Int32?     SocketSendBufferSize      { get; set; }

        /// <summary>
        /// Whether to enable TCP keep-alive probes (SO_KEEPALIVE) on the relayed sockets.
        /// Keep-alive detects half-open connections, e.g. after a peer lost power,
        /// which the application level idle timeout alone can not distinguish from
        /// a connection that is merely quiet.
        /// </summary>
        public Boolean    TcpKeepAlive              { get; set; } = true;

        /// <summary>
        /// The idle time before the first TCP keep-alive probe is sent.
        /// </summary>
        public TimeSpan   KeepAliveTime             { get; set; } = TimeSpan.FromSeconds(60);

        /// <summary>
        /// The time between two TCP keep-alive probes.
        /// </summary>
        public TimeSpan   KeepAliveInterval         { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>
        /// The number of unacknowledged TCP keep-alive probes before the
        /// connection is considered dead.
        /// </summary>
        public Int32      KeepAliveRetryCount       { get; set; } = 5;

        /// <summary>
        /// The maximum number of queued chunks per participant of a chat rendezvous
        /// (three or more ports). A participant that exceeds this limit can not keep
        /// up with the others and is disconnected instead of stalling the whole chat.
        /// </summary>
        public Int32      BroadcastQueueLength      { get; set; } = 128;

        /// <summary>
        /// The maximum number of queued bytes per participant of a chat rendezvous.
        /// See <see cref="BroadcastQueueLength"/>.
        /// </summary>
        public Int32      BroadcastQueueBytes       { get; set; } = 4 * 1024 * 1024;

        /// <summary>
        /// The TCP listen backlog of the rendezvous listeners.
        /// </summary>
        public Int32      ListenBacklog             { get; set; } = 8;

        #endregion


        #region (static) Defaults(Profile)

        /// <summary>
        /// Return the built-in default settings for the given transfer profile.
        /// </summary>
        /// <param name="Profile">A transfer profile.</param>
        public static TransferProfileSettings Defaults(TransferProfile Profile)

            => Profile switch {

                   // Chat, SSH, remote control: every millisecond is visible to a human.
                   TransferProfile.Interactive => new TransferProfileSettings {
                                                      NoDelay                  = true,
                                                      RelayBufferSize          = 8 * 1024,
                                                      SocketReceiveBufferSize  = 32 * 1024,
                                                      SocketSendBufferSize     = 32 * 1024,
                                                      TcpKeepAlive             = true,
                                                      KeepAliveTime            = TimeSpan.FromSeconds(30),
                                                      KeepAliveInterval        = TimeSpan.FromSeconds(10),
                                                      KeepAliveRetryCount      = 3,
                                                      BroadcastQueueLength     = 512,
                                                      BroadcastQueueBytes      = 1 * 1024 * 1024,
                                                      ListenBacklog            = 8
                                                  },

                   // File transfers, backups: throughput per system call is what counts.
                   // The Nagle algorithm stays enabled and the socket buffers are left to
                   // the operating system, so that the receive window auto-tuning keeps
                   // working, which matters a lot on long fat networks.
                   TransferProfile.Bulk        => new TransferProfileSettings {
                                                      NoDelay                  = false,
                                                      RelayBufferSize          = 256 * 1024,
                                                      SocketReceiveBufferSize  = null,
                                                      SocketSendBufferSize     = null,
                                                      TcpKeepAlive             = true,
                                                      KeepAliveTime            = TimeSpan.FromSeconds(120),
                                                      KeepAliveInterval        = TimeSpan.FromSeconds(30),
                                                      KeepAliveRetryCount      = 5,
                                                      BroadcastQueueLength     = 32,
                                                      BroadcastQueueBytes      = 16 * 1024 * 1024,
                                                      ListenBacklog            = 8
                                                  },

                   _                           => new TransferProfileSettings {
                                                      NoDelay                  = true,
                                                      RelayBufferSize          = 64 * 1024,
                                                      SocketReceiveBufferSize  = null,
                                                      SocketSendBufferSize     = null,
                                                      TcpKeepAlive             = true,
                                                      KeepAliveTime            = TimeSpan.FromSeconds(60),
                                                      KeepAliveInterval        = TimeSpan.FromSeconds(15),
                                                      KeepAliveRetryCount      = 5,
                                                      BroadcastQueueLength     = 128,
                                                      BroadcastQueueBytes      = 4 * 1024 * 1024,
                                                      ListenBacklog            = 8
                                                  }

               };

        #endregion

        #region Validate(Path)

        /// <summary>
        /// Validate these settings and return a human readable error message
        /// for every invalid value.
        /// </summary>
        /// <param name="Path">The configuration path of these settings, used within the error messages.</param>
        public IEnumerable<String> Validate(String Path)
        {

            if (RelayBufferSize < 512 || RelayBufferSize > 8 * 1024 * 1024)
                yield return $"{Path}.{nameof(RelayBufferSize)} must be between 512 and 8388608 bytes, but is {RelayBufferSize}!";

            if (SocketReceiveBufferSize is Int32 receiveBufferSize && receiveBufferSize < 512)
                yield return $"{Path}.{nameof(SocketReceiveBufferSize)} must be at least 512 bytes, but is {receiveBufferSize}!";

            if (SocketSendBufferSize    is Int32 sendBufferSize    && sendBufferSize    < 512)
                yield return $"{Path}.{nameof(SocketSendBufferSize)} must be at least 512 bytes, but is {sendBufferSize}!";

            if (KeepAliveTime     <= TimeSpan.Zero)
                yield return $"{Path}.{nameof(KeepAliveTime)} must be positive, but is {KeepAliveTime}!";

            if (KeepAliveInterval <= TimeSpan.Zero)
                yield return $"{Path}.{nameof(KeepAliveInterval)} must be positive, but is {KeepAliveInterval}!";

            if (KeepAliveRetryCount  < 1)
                yield return $"{Path}.{nameof(KeepAliveRetryCount)} must be at least 1, but is {KeepAliveRetryCount}!";

            if (BroadcastQueueLength < 1)
                yield return $"{Path}.{nameof(BroadcastQueueLength)} must be at least 1, but is {BroadcastQueueLength}!";

            if (BroadcastQueueBytes  < RelayBufferSize)
                yield return $"{Path}.{nameof(BroadcastQueueBytes)} must be at least {nameof(RelayBufferSize)} ({RelayBufferSize} bytes), but is {BroadcastQueueBytes}!";

            if (ListenBacklog        < 1)
                yield return $"{Path}.{nameof(ListenBacklog)} must be at least 1, but is {ListenBacklog}!";

        }

        #endregion

    }

}
