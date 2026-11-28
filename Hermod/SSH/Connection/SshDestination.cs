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

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// Serves one <c>direct-tcpip</c> channel to a destination the application knows, rather than to a
    /// TCP connection: the bytes of the channel go wherever the handler takes them - a tunnel to a
    /// charging station, say. The server closes the channel once the handler returns.
    /// </summary>
    /// <param name="Context">Who asked, for which destination, and on which channel.</param>
    /// <param name="CancellationToken">Fires when the channel is closed, the server stops, or the connection ends.</param>
    public delegate ValueTask SshDestinationHandler(SshDestinationContext  Context,
                                                    CancellationToken      CancellationToken);


    /// <summary>
    /// Asked when a session opens a <c>direct-tcpip</c> channel - what <c>ssh -J</c> and <c>ssh -W</c> open -
    /// before any port forwarding: how to serve the destination for this session, or null where it is
    /// none of the application's. Then the channel is what it always was: a port forwarding, refused or
    /// allowed by the forwarding policy and the session's restrictions.
    /// </summary>
    /// <remarks>
    /// A destination answered for is no port forwarding: nothing on the network is connected to, and
    /// neither the forwarding policy nor <see cref="SshSessionRestrictions.AllowPortForwarding"/> is asked.
    /// Who may reach it is the factory's to decide - with the session, its login, key and restrictions.
    /// What it throws refuses the channel.
    /// </remarks>
    /// <param name="Session">Who signed in: login, method, key, restrictions, peer.</param>
    /// <param name="Host">The host the client asked for, as it named it: "CP001".</param>
    /// <param name="Port">The port the client asked for.</param>
    /// <param name="CancellationToken">Fires after five seconds, or when the connection ends while the answer is being made.</param>
    public delegate ValueTask<SshDestinationHandler?> SshDestinationFactory(SshSessionInfo     Session,
                                                                            String             Host,
                                                                            IPPort             Port,
                                                                            CancellationToken  CancellationToken);


    /// <summary>
    /// What a <see cref="SshDestinationHandler"/> is given: the session, the destination it asked for, and
    /// the channel to serve it on.
    /// </summary>
    /// <param name="Session">Who signed in: login, method, key, restrictions, peer.</param>
    /// <param name="Host">The host the client asked for.</param>
    /// <param name="Port">The port the client asked for.</param>
    /// <param name="Channel">The channel: read the client's bytes from <see cref="SshMuxChannel.Input"/>, send with
    /// <see cref="SshMuxChannel.SendDataAsync"/> - or both at once through <see cref="SshMuxChannel.AsStream"/>.</param>
    /// <param name="Closed">Fires when the client closed the channel, or the connection went.</param>
    /// <param name="Stopping">Fires when the server is stopping: the handler is asked to end.</param>
    public sealed record SshDestinationContext(SshSessionInfo     Session,
                                               String             Host,
                                               IPPort             Port,
                                               SshMuxChannel      Channel,
                                               CancellationToken  Closed,
                                               CancellationToken  Stopping)
    {

        #region RelayAsync(Pipe, CancellationToken = default)

        /// <summary>
        /// Relay the bytes of the channel to the given pipe and those of the pipe to the channel, until
        /// either way ends or the token fires; then the pipe is completed both ways.
        /// </summary>
        /// <param name="Pipe">Where the destination is: the pipe of a tunnel, say.</param>
        /// <param name="CancellationToken">Ends the relay.</param>
        public async Task RelayAsync(System.IO.Pipelines.IDuplexPipe  Pipe,
                                     CancellationToken                CancellationToken   = default)
        {

            using var ending   = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken, Closed);

            var       channel  = Channel.AsStream();
            var       toPipe   = CopyAsync(channel,                  Pipe.Output.AsStream(), ending.Token);
            var       fromPipe = CopyAsync(Pipe.Input.AsStream(),    channel,                ending.Token);

            await Task.WhenAny(toPipe, fromPipe).ConfigureAwait(false);

            try { ending.Cancel(); } catch (ObjectDisposedException) { }

            try { await Task.WhenAll(toPipe, fromPipe).ConfigureAwait(false); } catch { }

            await Pipe.Output.CompleteAsync().ConfigureAwait(false);
            await Pipe.Input. CompleteAsync().ConfigureAwait(false);

        }

        private static async Task CopyAsync(Stream From, Stream To, CancellationToken CancellationToken)
        {
            try
            {
                await From.CopyToAsync(To, CancellationToken).ConfigureAwait(false);
                await To.FlushAsync(CancellationToken).ConfigureAwait(false);
            }
            catch (Exception) when (CancellationToken.IsCancellationRequested)
            { }
            catch (IOException)
            { }
            catch (ObjectDisposedException)
            { }
        }

        #endregion

    }

}
