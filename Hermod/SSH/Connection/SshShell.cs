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

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    #region SshWindowSize

    /// <summary>
    /// The size of a terminal, as <c>pty-req</c> and <c>window-change</c> carry it (RFC 4254 §6.2, §6.7):
    /// characters, and pixels where the client knows them (0 where it does not).
    /// </summary>
    /// <param name="Columns">The width in characters.</param>
    /// <param name="Rows">The height in rows.</param>
    /// <param name="PixelWidth">The width in pixels, or 0.</param>
    /// <param name="PixelHeight">The height in pixels, or 0.</param>
    public readonly record struct SshWindowSize(UInt32  Columns,
                                                UInt32  Rows,
                                                UInt32  PixelWidth   = 0,
                                                UInt32  PixelHeight  = 0)
    {

        /// <summary>
        /// The payload of a <c>window-change</c> request for this size.
        /// </summary>
        public Byte[] ToWindowChangePayload()
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteUInt32(Columns); w.WriteUInt32(Rows); w.WriteUInt32(PixelWidth); w.WriteUInt32(PixelHeight);
            return abw.WrittenSpan.ToArray();
        }

        /// <summary>
        /// The size a <c>window-change</c> request carries.
        /// </summary>
        /// <param name="Payload">The request-specific bytes.</param>
        public static SshWindowSize ParseWindowChange(ReadOnlySpan<Byte> Payload)
        {
            var r = new SshPacketReader(Payload);
            return new SshWindowSize(r.ReadUInt32(), r.ReadUInt32(), r.ReadUInt32(), r.ReadUInt32());
        }

    }

    #endregion

    #region SshPty

    /// <summary>
    /// The pseudo-terminal a client asked for with <c>pty-req</c> (RFC 4254 §6.2): what its terminal is
    /// called, how big it is, and its terminal modes.
    /// </summary>
    /// <remarks>
    /// The modes are kept as the client sent them, opcode and value (RFC 4254 §8): there is no terminal
    /// driver here to apply them to, but a server that is its own line discipline may want to know, say,
    /// which character the client's Backspace sends (<see cref="VERASE"/>).
    /// </remarks>
    /// <param name="Term">The TERM environment variable value, e.g. "xterm".</param>
    /// <param name="Size">The size of the terminal.</param>
    /// <param name="Modes">The terminal modes, by opcode.</param>
    public sealed record SshPty(String                             Term,
                                SshWindowSize                      Size,
                                IReadOnlyDictionary<Byte, UInt32>  Modes)
    {

        #region Data

        /// <summary>
        /// The character that interrupts (Ctrl+C, usually 3).
        /// </summary>
        public const Byte VINTR   = 1;

        /// <summary>
        /// The character that erases the one to the left of the cursor (DEL, 127, or BS, 8).
        /// </summary>
        public const Byte VERASE  = 3;

        /// <summary>
        /// The character that ends the input (Ctrl+D, usually 4).
        /// </summary>
        public const Byte VEOF    = 4;

        #endregion


        #region (static) Parse(Payload)

        /// <summary>
        /// The pseudo-terminal a <c>pty-req</c> request asks for.
        /// </summary>
        /// <param name="Payload">The request-specific bytes.</param>
        public static SshPty Parse(ReadOnlySpan<Byte> Payload)
        {

            var r      = new SshPacketReader(Payload);
            var term   = r.ReadString();
            var size   = new SshWindowSize(r.ReadUInt32(), r.ReadUInt32(), r.ReadUInt32(), r.ReadUInt32());
            var modes  = ParseModes(r.ReadBinaryString());

            return new SshPty(term, size, modes);

        }

        #endregion

        #region (static) ParseModes(EncodedModes)

        /// <summary>
        /// The encoded terminal modes (RFC 4254 §8): an opcode and a uint32 each, up to TTY_OP_END (0).
        /// </summary>
        /// <remarks>
        /// Opcodes from 160 on have no defined argument, and nothing after one can be read with
        /// certainty - so reading stops there, with what came before it, as RFC 4254 says a server
        /// should rather than refusing the terminal.
        /// </remarks>
        /// <param name="EncodedModes">The encoded modes.</param>
        public static IReadOnlyDictionary<Byte, UInt32> ParseModes(ReadOnlySpan<Byte> EncodedModes)
        {

            var modes = new Dictionary<Byte, UInt32>();
            var i     = 0;

            while (i < EncodedModes.Length)
            {

                var opcode = EncodedModes[i++];

                if (opcode == 0 || opcode >= 160 || i + 4 > EncodedModes.Length)
                    break;

                modes[opcode] = (UInt32) (EncodedModes[i] << 24 | EncodedModes[i + 1] << 16 | EncodedModes[i + 2] << 8 | EncodedModes[i + 3]);
                i += 4;

            }

            return modes;

        }

        #endregion

        #region ToPayload()

        /// <summary>
        /// The payload of a <c>pty-req</c> request for this pseudo-terminal.
        /// </summary>
        public Byte[] ToPayload()
        {

            var modes = new List<Byte>();

            foreach (var (opcode, value) in Modes)
            {
                modes.Add(opcode);
                modes.Add((Byte) (value >> 24)); modes.Add((Byte) (value >> 16)); modes.Add((Byte) (value >> 8)); modes.Add((Byte) value);
            }

            modes.Add(0);

            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            w.WriteString(Term);
            w.WriteUInt32(Size.Columns); w.WriteUInt32(Size.Rows); w.WriteUInt32(Size.PixelWidth); w.WriteUInt32(Size.PixelHeight);
            w.WriteBinaryString(modes.ToArray());
            return abw.WrittenSpan.ToArray();

        }

        #endregion

    }

    #endregion

    #region SshSessionInfo

    /// <summary>
    /// Who a session is with, and how they got in: what a server says about a session in its log, and
    /// what decides what the session may do.
    /// </summary>
    /// <param name="ConnectionId">An id correlating the session with every audit event of its connection.</param>
    /// <param name="Username">The authenticated account.</param>
    /// <param name="Method">The authentication method that completed it.</param>
    /// <param name="PublicKeyBlob">The public key that authenticated, where one did.</param>
    /// <param name="Peer">Where the client connected from, where that could be told.</param>
    /// <param name="Local">Where it connected to.</param>
    /// <param name="ClientIdentification">What the client said it is in its identification string.</param>
    /// <param name="Restrictions">What the credential confines the session to.</param>
    public sealed record SshSessionInfo(String                    ConnectionId,
                                        String                    Username,
                                        String                    Method,
                                        Byte[]?                   PublicKeyBlob,
                                        IPSocket?                 Peer,
                                        IPSocket?                 Local,
                                        SshIdentificationString?  ClientIdentification,
                                        SshSessionRestrictions    Restrictions)
    {

        /// <summary>
        /// The SHA-256 fingerprint of the key that authenticated ("SHA256:..."), or null.
        /// </summary>
        public String? PublicKeyFingerprint
            => PublicKeyBlob is not null
                   ? SshFingerprint.Sha256(PublicKeyBlob)
                   : null;

        /// <summary>
        /// The type of the key that authenticated ("ssh-ed25519"), or null.
        /// </summary>
        public String? PublicKeyType
        {
            get
            {

                if (PublicKeyBlob is null)
                    return null;

                try
                {
                    return new SshPacketReader(PublicKeyBlob).ReadString();
                }
                catch (SshWireException)
                {
                    return null;
                }

            }
        }

    }

    #endregion

    #region SshShellContext / SshShellHandler

    /// <summary>
    /// What a server-side <c>shell</c> handler is given: who it is talking to, their terminal, what they
    /// type, a way to write to their screen - and word of what happens to the session while it runs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unlike an exec handler, which is given a command and answers it, a shell handler is a
    /// conversation for as long as it runs. So the channel's requests go on being read while it does:
    /// a terminal that is resized says so (<see cref="WindowChanged"/>), a client may send a signal or a
    /// break, and none of that waits for the handler to finish.
    /// </para>
    /// <para>
    /// There is no terminal driver between the two ends. What the client types arrives on
    /// <see cref="Input"/> as it was typed - Backspace as a byte, Enter as CR, Ctrl+C as 3 - and
    /// whatever the handler writes reaches the screen as it was written, so "\r\n" ends a line there.
    /// That is the handler's to take care of, being its own line discipline.
    /// </para>
    /// <para>
    /// The cancellation token the handler is called with fires when the session is over - the client
    /// closed the channel or went away, or the server is stopping. <see cref="Stopping"/> tells the last
    /// from the others: then the channel is still open, and there is a moment to say goodbye on it
    /// before returning.
    /// </para>
    /// </remarks>
    public sealed class SshShellContext
    {

        #region Data

        private readonly SshMuxChannel  channel;
        private          SshWindowSize? size;

        #endregion

        #region Properties

        /// <summary>
        /// Who the session is with, and how they got in.
        /// </summary>
        public SshSessionInfo                       Session      { get; }

        /// <summary>
        /// The pseudo-terminal the client asked for, or null where it asked for none - <c>ssh -T</c>, or a
        /// program feeding the session from a pipe, which types nothing and has no screen.
        /// </summary>
        public SshPty?                              Pty          { get; }

        /// <summary>
        /// The size of the terminal now: what <c>pty-req</c> said, and the last <c>window-change</c> after
        /// it. Null without a terminal.
        /// </summary>
        public SshWindowSize?                       Size
            => size;

        /// <summary>
        /// The environment variables the client asked for with <c>env</c> before the shell started.
        /// </summary>
        public IReadOnlyDictionary<String, String>  Environment  { get; }

        /// <summary>
        /// What the client types: the channel's data, until it sends EOF or closes.
        /// </summary>
        public Stream                               Input
            => channel.Input;

        /// <summary>
        /// Fires when the client closed the channel or the connection was lost.
        /// </summary>
        public CancellationToken                    Closed       { get; }

        /// <summary>
        /// Fires when the server is stopping, while the channel is still open.
        /// </summary>
        public CancellationToken                    Stopping     { get; }

        #endregion

        #region Events

        /// <summary>
        /// The client's terminal has a new size.
        /// </summary>
        public event Action<SshWindowSize>?  WindowChanged;

        /// <summary>
        /// The client sent a signal, named as RFC 4254 §6.9 names them: "INT", "TERM", "HUP", ...
        /// </summary>
        public event Action<String>?         Signalled;

        /// <summary>
        /// The client sent a break (RFC 4335), for as many milliseconds as it says.
        /// </summary>
        public event Action<UInt32>?         BreakRequested;

        #endregion

        #region Constructor(s)

        internal SshShellContext(SshMuxChannel                        Channel,
                                 SshSessionInfo                       Session,
                                 SshPty?                              Pty,
                                 IReadOnlyDictionary<String, String>  Environment,
                                 CancellationToken                    Closed,
                                 CancellationToken                    Stopping)
        {

            this.channel      = Channel;
            this.Session      = Session;
            this.Pty          = Pty;
            this.size         = Pty?.Size;
            this.Environment  = Environment;
            this.Closed       = Closed;
            this.Stopping     = Stopping;

        }

        #endregion


        #region WriteAsync(Data, CancellationToken)

        /// <summary>
        /// Write bytes to the client's screen. Waits where the client has not taken what it was sent before.
        /// </summary>
        public ValueTask WriteAsync(ReadOnlyMemory<Byte> Data, CancellationToken CancellationToken = default)
            => channel.SendDataAsync(Data, CancellationToken);

        /// <summary>
        /// Write UTF-8 text to the client's screen, exactly as given.
        /// </summary>
        public ValueTask WriteAsync(String Text, CancellationToken CancellationToken = default)
            => channel.SendDataAsync(Encoding.UTF8.GetBytes(Text), CancellationToken);

        #endregion

        #region WriteErrorAsync(Data, CancellationToken)

        /// <summary>
        /// Write bytes to the client's standard error. A terminal shows them as it shows the rest.
        /// </summary>
        public ValueTask WriteErrorAsync(ReadOnlyMemory<Byte> Data, CancellationToken CancellationToken = default)
            => channel.SendErrorAsync(Data, CancellationToken);

        #endregion


        #region (internal) OnWindowChange(Size) / OnSignal(Name) / OnBreak(Milliseconds)

        internal void OnWindowChange(SshWindowSize Size)
        {
            size = Size;
            WindowChanged?.Invoke(Size);
        }

        internal void OnSignal(String Name)
            => Signalled?.Invoke(Name);

        internal void OnBreak(UInt32 Milliseconds)
            => BreakRequested?.Invoke(Milliseconds);

        #endregion

    }


    /// <summary>
    /// A server-side handler for an interactive <c>shell</c> session: it talks with the client for as long
    /// as it runs, and returns the exit status the client is told.
    /// </summary>
    /// <param name="Context">The session: who, their terminal, their keys, their screen.</param>
    /// <param name="CancellationToken">Fires when the session is over, or the server is stopping.</param>
    public delegate ValueTask<Int32> SshShellHandler(SshShellContext Context, CancellationToken CancellationToken);

    #endregion

}
