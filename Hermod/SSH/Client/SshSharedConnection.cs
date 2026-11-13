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

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Client
{

    /// <summary>
    /// One SSH connection to a peer that several users share, each with a channel of its own: messages over
    /// one subsystem, a key exchange over another, name resolution over a third. It is made when the first
    /// of them asks for it, whoever that is, and made again for whoever asks after it ended.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The connection belongs to this object, and to none of its users. A user closes the channel it
    /// opened and never the connection; <see cref="DisposeAsync"/> closes it for all of them.
    /// </para>
    /// <para>
    /// Whoever asks while it is being made waits for that one connection: two users never dial twice.
    /// A connection that could not be made is the failure of whoever asked, and the next one asks again.
    /// </para>
    /// </remarks>
    public sealed class SshSharedConnection : IAsyncDisposable
    {

        #region Data

        private readonly Func<CancellationToken, ValueTask<SshClient>>  dial;
        private readonly SemaphoreSlim                                  dialling  = new (1, 1);
        private volatile SshClient?                                     client;
        private          Int64                                          made;
        private          Int32                                          disposed;

        #endregion

        #region Properties

        /// <summary>
        /// The connection while it lasts; null where none was made yet, or the last one ended.
        /// </summary>
        public SshClient? Current
            => client is SshClient ssh && !ssh.Closed.IsCompleted
                   ? ssh
                   : null;

        /// <summary>
        /// How many connections were made so far: one, as long as the first one lasts.
        /// </summary>
        public Int64 Made
            => Interlocked.Read(ref made);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// A connection to the given server, made with the given login when it is first asked for.
        /// </summary>
        /// <param name="Host">The server's host name or address.</param>
        /// <param name="Port">The server's SSH port.</param>
        /// <param name="Options">The SSH login: user name, credentials, how the server's host key is checked, keepalive.</param>
        public SshSharedConnection(String            Host,
                                   UInt16            Port,
                                   SshClientOptions  Options)

            : this(cancellationToken => SshClient.ConnectAsync(Host, Port, Options, cancellationToken))

        { }

        /// <summary>
        /// A connection made by the given delegate when it is first asked for, and again after it ended.
        /// </summary>
        /// <param name="Dial">Makes a connection. What it returns becomes this object's, and is closed by it.</param>
        public SshSharedConnection(Func<CancellationToken, ValueTask<SshClient>> Dial)
        {
            this.dial = Dial;
        }

        #endregion


        #region GetAsync(CancellationToken = default)

        /// <summary>
        /// The connection: the one that lasts, or a new one where there is none.
        /// </summary>
        /// <exception cref="ObjectDisposedException">It was disposed of.</exception>
        public async ValueTask<SshClient> GetAsync(CancellationToken CancellationToken = default)
        {

            ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);

            if (Current is SshClient lasting)
                return lasting;

            await dialling.WaitAsync(CancellationToken).ConfigureAwait(false);

            try
            {

                // Made while this one waited.
                if (Current is SshClient madeMeanwhile)
                    return madeMeanwhile;

                ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);

                var before  = client;
                var ssh     = await dial(CancellationToken).ConfigureAwait(false);

                // Disposed of while it was dialling: nobody is left to close it but this.
                if (Volatile.Read(ref disposed) != 0)
                {
                    await ssh.DisposeAsync().ConfigureAwait(false);
                    throw new ObjectDisposedException(nameof(SshSharedConnection));
                }

                client = ssh;
                Interlocked.Increment(ref made);

                // The one before ended, or it would not be replaced; what it still holds goes.
                if (before is not null)
                    await before.DisposeAsync().ConfigureAwait(false);

                return ssh;

            }
            finally
            {
                dialling.Release();
            }

        }

        #endregion

        #region DisposeAsync()

        /// <summary>
        /// Close the connection, for every one of its users, and make no more.
        /// </summary>
        public async ValueTask DisposeAsync()
        {

            if (Interlocked.Exchange(ref disposed, 1) != 0)
                return;

            if (client is SshClient ssh)
                await ssh.DisposeAsync().ConfigureAwait(false);

        }

        #endregion

    }

}
