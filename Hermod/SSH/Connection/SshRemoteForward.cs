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

using System.Collections.Concurrent;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// A remote (<c>ssh -R</c>) forward the server has agreed to: it listens on <see cref="BindHost"/>:
    /// <see cref="BoundPort"/>, and every connection there comes out at <see cref="LocalHost"/>:
    /// <see cref="LocalPort"/> on this side. Cancelling or disposing it asks the server to stop listening
    /// (<c>cancel-tcpip-forward</c>) and ends the connections it carries; it lives until then, or until
    /// the connection ends.
    /// </summary>
    public sealed class SshRemoteForward : IAsyncDisposable
    {

        #region Data

        private readonly SshChannelMultiplexer                                                   mux;
        private readonly ConcurrentDictionary<(String Address, UInt16 Port), SshRemoteForward>  registry;
        private readonly CancellationTokenSource                                                 cts = new ();
        private          Int32                                                                   disposed;

        #endregion

        #region Properties

        /// <summary>
        /// The address the server listens on, exactly as it was asked for (e.g. <c>"localhost"</c>).
        /// </summary>
        public String  BindHost     { get; }

        /// <summary>
        /// The port the server listens on: the one asked for, or the one the server chose when asked for 0.
        /// </summary>
        public UInt16  BoundPort    { get; }

        /// <summary>
        /// The host each forwarded connection is relayed to on this side.
        /// </summary>
        public String  LocalHost    { get; }

        /// <summary>
        /// The port each forwarded connection is relayed to on this side.
        /// </summary>
        public UInt16  LocalPort    { get; }

        #endregion

        #region Constructor(s)

        internal SshRemoteForward(SshChannelMultiplexer                                                   Mux,
                                  ConcurrentDictionary<(String Address, UInt16 Port), SshRemoteForward>  Registry,
                                  String                                                                  BindHost,
                                  UInt16                                                                  BoundPort,
                                  String                                                                  LocalHost,
                                  UInt16                                                                  LocalPort)
        {

            this.mux        = Mux;
            this.registry   = Registry;

            this.BindHost   = BindHost;
            this.BoundPort  = BoundPort;
            this.LocalHost  = LocalHost;
            this.LocalPort  = LocalPort;

        }

        #endregion


        #region (internal) RelayAsync(Channel)

        /// <summary>
        /// Relay one of this forward's channels to its local target, until either side ends or the forward
        /// is disposed.
        /// </summary>
        internal Task RelayAsync(SshMuxChannel Channel)
        {

            CancellationToken token;

            try
            {
                token = cts.Token;
            }
            catch (ObjectDisposedException)
            {
                // Disposed between the channel's arrival and now.
                return Channel.CloseAsync().AsTask();
            }

            return SshRemoteForwarding.ConnectAndRelayAsync(Channel, LocalHost, LocalPort, token);

        }

        #endregion

        #region (internal) Forget()

        /// <summary>
        /// Withdraw a forward the server never agreed to: nothing to cancel on its side.
        /// </summary>
        internal void Forget()
        {
            if (Withdraw())
                cts.Dispose();
        }

        #endregion

        #region CancelAsync(CancellationToken)

        /// <summary>
        /// Stop taking connections, end the ones under way, ask the server to stop listening, and wait
        /// for its answer: true when it stopped, false when it refused or the forward was already cancelled.
        /// </summary>
        public async ValueTask<Boolean> CancelAsync(CancellationToken CancellationToken = default)
        {

            if (!Withdraw())
                return false;

            try
            {
                await cts.CancelAsync().ConfigureAwait(false);
                return await mux.SendGlobalRequestAsync("cancel-tcpip-forward",
                                                        true,
                                                        SshRemoteForwarding.AddressAndPort(BindHost, BoundPort),
                                                        CancellationToken).ConfigureAwait(false);
            }
            finally
            {
                cts.Dispose();
            }

        }

        #endregion

        #region DisposeAsync()

        /// <summary>
        /// Stop taking connections, end the ones under way, and ask the server to stop listening - without
        /// waiting for its answer, so that disposing never hangs on a peer that has stopped answering
        /// (<see cref="CancelAsync"/> waits for it). A connection that has already ended took the server's
        /// listener with it; that is not an error.
        /// </summary>
        public async ValueTask DisposeAsync()
        {

            if (!Withdraw())
                return;

            await cts.CancelAsync().ConfigureAwait(false);

            try
            {
                await mux.SendGlobalRequestAsync("cancel-tcpip-forward",
                                                 false,
                                                 SshRemoteForwarding.AddressAndPort(BindHost, BoundPort)).ConfigureAwait(false);
            }
            catch
            { }

            cts.Dispose();

        }

        #endregion

        #region (private) Withdraw()

        /// <summary>
        /// Take the forward out of the connection's registry, so that no further channel reaches it; true
        /// the first time only.
        /// </summary>
        private Boolean Withdraw()
        {

            if (Interlocked.Exchange(ref disposed, 1) != 0)
                return false;

            registry.TryRemove(new KeyValuePair<(String, UInt16), SshRemoteForward>((BindHost, BoundPort), this));
            return true;

        }

        #endregion


        #region (override) ToString()

        /// <inheritdoc/>
        public override String ToString()
            => $"-R {BindHost}:{BoundPort}:{LocalHost}:{LocalPort}";

        #endregion

    }

}
