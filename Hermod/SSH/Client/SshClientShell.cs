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

using System.Buffers;
using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Client
{

    /// <summary>
    /// An interactive shell on a server, as a terminal program has one: what it writes, what is typed at
    /// it, the size of the terminal, signals - and its exit status once it ends.
    /// </summary>
    /// <remarks>
    /// The client half of what <see cref="SshShellHandler"/> is the server half of: what PuTTY or
    /// <c>ssh</c> does after "login as", for a program - or a test - that wants to be the person at the
    /// terminal.
    /// </remarks>
    public sealed class SshClientShell : IAsyncDisposable
    {

        #region Data

        private readonly SshMuxChannel               channel;
        private readonly TaskCompletionSource<Int32?> exit = new (TaskCreationOptions.RunContinuationsAsynchronously);

        #endregion

        #region Properties

        /// <summary>
        /// What the shell writes to the terminal, until it ends.
        /// </summary>
        public Stream        Output      => channel.Input;

        /// <summary>
        /// Whether the server granted the pseudo-terminal that was asked for; false where none was.
        /// </summary>
        public Boolean       HasPty      { get; }

        /// <summary>
        /// The exit status the server said the shell ended with - null where it ended without saying one,
        /// or with a signal.
        /// </summary>
        public Task<Int32?>  ExitStatus  => exit.Task;

        /// <summary>
        /// Completes when the channel is closed.
        /// </summary>
        public Task          Closed      => channel.Closed;

        #endregion

        #region Constructor(s)

        private SshClientShell(SshMuxChannel Channel, Boolean HasPty)
        {

            this.channel  = Channel;
            this.HasPty   = HasPty;

            _ = ReadRequestsAsync();

        }

        #endregion


        #region (static) OpenAsync(Mux, Pty = null, Environment = null, CancellationToken = default)

        /// <summary>
        /// Open a session channel, ask for the given pseudo-terminal and environment, and start a shell.
        /// Throws where the server refuses the shell.
        /// </summary>
        /// <param name="Mux">The connection.</param>
        /// <param name="Pty">The terminal to ask for; none for a shell without one, as <c>ssh -T</c> has it.</param>
        /// <param name="Environment">Environment variables to ask for, as <c>SendEnv</c> does.</param>
        /// <param name="CancellationToken">An optional token to cancel the request.</param>
        public static async ValueTask<SshClientShell> OpenAsync(SshChannelMultiplexer                  Mux,
                                                                SshPty?                                Pty                = null,
                                                                IReadOnlyDictionary<String, String>?   Environment        = null,
                                                                CancellationToken                      CancellationToken  = default)
        {

            var channel = await Mux.OpenChannelAsync("session", CancellationToken: CancellationToken).ConfigureAwait(false);

            foreach (var (name, value) in Environment ?? new Dictionary<String, String>())
            {
                var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
                w.WriteString(name); w.WriteString(value);
                await channel.SendRequestAsync("env", true, abw.WrittenSpan.ToArray(), CancellationToken).ConfigureAwait(false);
            }

            var hasPty = Pty is not null &&
                         await channel.SendRequestAsync("pty-req", true, Pty.ToPayload(), CancellationToken).ConfigureAwait(false);

            if (!await channel.SendRequestAsync("shell", true, [], CancellationToken).ConfigureAwait(false))
            {
                try { await channel.CloseAsync(CancellationToken).ConfigureAwait(false); } catch { }
                throw new InvalidOperationException("The server refused the shell.");
            }

            return new SshClientShell(channel, hasPty);

        }

        #endregion


        #region WriteAsync(Data, CancellationToken)

        /// <summary>
        /// Type the given bytes, as a terminal sends them: Enter as "\r", Backspace as 127, Ctrl+C as 3.
        /// </summary>
        public ValueTask WriteAsync(ReadOnlyMemory<Byte> Data, CancellationToken CancellationToken = default)
            => channel.SendDataAsync(Data, CancellationToken);

        /// <summary>
        /// Type the given text, as UTF-8.
        /// </summary>
        public ValueTask WriteAsync(String Text, CancellationToken CancellationToken = default)
            => channel.SendDataAsync(Encoding.UTF8.GetBytes(Text), CancellationToken);

        #endregion

        #region ResizeAsync(Size, CancellationToken)

        /// <summary>
        /// Say that the terminal has a new size (<c>window-change</c>).
        /// </summary>
        public async ValueTask ResizeAsync(SshWindowSize Size, CancellationToken CancellationToken = default)
            => await channel.SendRequestAsync("window-change", false, Size.ToWindowChangePayload(), CancellationToken).ConfigureAwait(false);

        #endregion

        #region SignalAsync(Name, CancellationToken)

        /// <summary>
        /// Send a signal, named without "SIG": "INT", "TERM", ...
        /// </summary>
        public async ValueTask SignalAsync(String Name, CancellationToken CancellationToken = default)
            => await channel.SendRequestAsync("signal", false, SshSessionChannel.EncodeString(Name), CancellationToken).ConfigureAwait(false);

        #endregion

        #region SendEofAsync(CancellationToken)

        /// <summary>
        /// Say that nothing more will be typed.
        /// </summary>
        public ValueTask SendEofAsync(CancellationToken CancellationToken = default)
            => channel.SendEofAsync(CancellationToken);

        #endregion

        #region CloseAsync(CancellationToken)

        /// <summary>
        /// Close the channel, as a terminal window that is closed does.
        /// </summary>
        public ValueTask CloseAsync(CancellationToken CancellationToken = default)
            => channel.CloseAsync(CancellationToken);

        #endregion


        #region (private) ReadRequestsAsync()

        private async Task ReadRequestsAsync()
        {

            Int32? status = null;

            try
            {

                SshChannelRequest? request;

                while ((request = await channel.ReadRequestAsync().ConfigureAwait(false)) is not null)
                {

                    if (request.Value.Type == "exit-status")
                        status = (Int32) new SshPacketReader(request.Value.Data).ReadUInt32();

                    else if (request.Value.WantReply)
                        await channel.ReplyAsync(false).ConfigureAwait(false);

                }

            }
            catch
            { }
            finally
            {
                exit.TrySetResult(status);
            }

        }

        #endregion

        #region DisposeAsync()

        /// <summary>
        /// Close the channel.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            try { await channel.CloseAsync().ConfigureAwait(false); } catch { }
        }

        #endregion

    }

}
