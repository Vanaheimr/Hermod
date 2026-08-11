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
using System.Buffers.Binary;
using System.IO.Pipes;
using System.Net.Sockets;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>One identity held by an ssh-agent: its public-key wire blob and comment.</summary>
    /// <param name="PublicKeyBlob">The public-key blob (as used everywhere else for fingerprints/auth).</param>
    /// <param name="Comment">The agent's comment for the key.</param>
    public sealed record SshAgentIdentity(Byte[] PublicKeyBlob, String Comment);


    /// <summary>
    /// A client for the SSH agent protocol (draft-miller-ssh-agent): list identities and request signatures
    /// from keys held by the agent (the private key never leaves the agent). Talks over a byte stream —
    /// <see cref="ConnectAsync"/> opens the platform agent (the Windows <c>openssh-ssh-agent</c> named pipe or
    /// the <c>SSH_AUTH_SOCK</c> Unix socket), and the ctor accepts any stream (e.g. a fake agent for tests).
    /// </summary>
    public sealed class SshAgentClient : IAsyncDisposable
    {

        #region Constants

        private const Byte SSH_AGENT_FAILURE               = 5;
        private const Byte SSH_AGENTC_REQUEST_IDENTITIES   = 11;
        private const Byte SSH_AGENT_IDENTITIES_ANSWER     = 12;
        private const Byte SSH_AGENTC_SIGN_REQUEST         = 13;
        private const Byte SSH_AGENT_SIGN_RESPONSE         = 14;

        /// <summary>The <c>SSH_AGENT_RSA_SHA2_256</c> signing flag.</summary>
        public const UInt32 FlagRsaSha2_256 = 2;
        /// <summary>The <c>SSH_AGENT_RSA_SHA2_512</c> signing flag.</summary>
        public const UInt32 FlagRsaSha2_512 = 4;

        #endregion

        #region Data

        private readonly Stream         stream;
        private readonly SemaphoreSlim  gate = new (1, 1);

        #endregion

        #region Constructor(s)

        /// <summary>Create an agent client over an already-connected stream.</summary>
        public SshAgentClient(Stream Stream)
        {
            this.stream = Stream;
        }

        #endregion

        #region (static) ConnectAsync(CancellationToken)

        /// <summary>Connect to the platform ssh-agent (Windows named pipe or <c>SSH_AUTH_SOCK</c>).</summary>
        public static async ValueTask<SshAgentClient> ConnectAsync(CancellationToken CancellationToken = default)
        {

            if (OperatingSystem.IsWindows())
            {
                var pipe = new NamedPipeClientStream(".", "openssh-ssh-agent", PipeDirection.InOut, PipeOptions.Asynchronous);
                await pipe.ConnectAsync(CancellationToken).ConfigureAwait(false);
                return new SshAgentClient(pipe);
            }

            var sock = Environment.GetEnvironmentVariable("SSH_AUTH_SOCK")
                       ?? throw new InvalidOperationException("SSH_AUTH_SOCK is not set — no ssh-agent available.");
            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(sock), CancellationToken).ConfigureAwait(false);
            return new SshAgentClient(new NetworkStream(socket, ownsSocket: true));

        }

        #endregion


        #region ListIdentitiesAsync(CancellationToken)

        /// <summary>List the identities the agent holds.</summary>
        public async ValueTask<IReadOnlyList<SshAgentIdentity>> ListIdentitiesAsync(CancellationToken CancellationToken = default)
        {

            await gate.WaitAsync(CancellationToken).ConfigureAwait(false);
            try
            {
                await SendAsync(SSH_AGENTC_REQUEST_IDENTITIES, ReadOnlyMemory<Byte>.Empty, CancellationToken).ConfigureAwait(false);
                var (type, body) = await ReceiveAsync(CancellationToken).ConfigureAwait(false);
                if (type != SSH_AGENT_IDENTITIES_ANSWER)
                    throw new SshAgentException($"Unexpected agent reply {type} to REQUEST_IDENTITIES.");

                var reader  = new SshPacketReader(body);
                var count   = reader.ReadUInt32();
                var result  = new List<SshAgentIdentity>((Int32) count);
                for (var i = 0U; i < count; i++)
                {
                    var blob    = reader.ReadBinaryString();
                    var comment = reader.ReadString();
                    result.Add(new SshAgentIdentity(blob, comment));
                }
                return result;
            }
            finally { gate.Release(); }

        }

        #endregion

        #region SignAsync(PublicKeyBlob, Data, Flags, CancellationToken)

        /// <summary>Ask the agent to sign <paramref name="Data"/> with the identity <paramref name="PublicKeyBlob"/>.</summary>
        /// <returns>The SSH signature blob (<c>string algorithm || string signature</c>).</returns>
        public async ValueTask<Byte[]> SignAsync(Byte[] PublicKeyBlob, ReadOnlyMemory<Byte> Data, UInt32 Flags = 0, CancellationToken CancellationToken = default)
        {

            var abw = new ArrayBufferWriter<Byte>();
            var w   = new SshPacketWriter(abw);
            w.WriteBinaryString(PublicKeyBlob);
            w.WriteBinaryString(Data.Span);
            w.WriteUInt32(Flags);

            await gate.WaitAsync(CancellationToken).ConfigureAwait(false);
            try
            {
                await SendAsync(SSH_AGENTC_SIGN_REQUEST, abw.WrittenMemory, CancellationToken).ConfigureAwait(false);
                var (type, body) = await ReceiveAsync(CancellationToken).ConfigureAwait(false);
                if (type == SSH_AGENT_FAILURE)
                    throw new SshAgentException("The agent refused to sign (SSH_AGENT_FAILURE).");
                if (type != SSH_AGENT_SIGN_RESPONSE)
                    throw new SshAgentException($"Unexpected agent reply {type} to SIGN_REQUEST.");

                var reader = new SshPacketReader(body);
                return reader.ReadBinaryString();
            }
            finally { gate.Release(); }

        }

        #endregion

        #region GetKey(PublicKeyBlob)

        /// <summary>Wrap one agent-held identity as an <see cref="ISshHostKey"/> signer for public-key auth.</summary>
        public SshAgentKey GetKey(Byte[] PublicKeyBlob)
            => new (this, PublicKeyBlob);

        #endregion


        #region (private) framing

        private async ValueTask SendAsync(Byte Type, ReadOnlyMemory<Byte> Body, CancellationToken CancellationToken)
        {
            var frame = new Byte[4 + 1 + Body.Length];
            BinaryPrimitives.WriteUInt32BigEndian(frame, (UInt32) (1 + Body.Length));
            frame[4] = Type;
            Body.Span.CopyTo(frame.AsSpan(5));
            await stream.WriteAsync(frame, CancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(CancellationToken).ConfigureAwait(false);
        }

        private async ValueTask<(Byte Type, Byte[] Body)> ReceiveAsync(CancellationToken CancellationToken)
        {
            var lengthBytes = new Byte[4];
            await stream.ReadExactlyAsync(lengthBytes, CancellationToken).ConfigureAwait(false);
            var length = BinaryPrimitives.ReadUInt32BigEndian(lengthBytes);
            if (length == 0)
                throw new SshAgentException("Empty agent message.");

            var message = new Byte[length];
            await stream.ReadExactlyAsync(message, CancellationToken).ConfigureAwait(false);
            return (message[0], message[1..]);
        }

        #endregion

        #region DisposeAsync()

        /// <summary>Close the agent connection.</summary>
        public async ValueTask DisposeAsync()
        {
            gate.Dispose();
            await stream.DisposeAsync().ConfigureAwait(false);
        }

        #endregion

    }


    /// <summary>
    /// An <see cref="ISshHostKey"/> backed by an ssh-agent identity: the public key is known, and signing is
    /// delegated to the agent (the private key never leaves it). Usable anywhere a key signer is expected,
    /// e.g. public-key user authentication.
    /// </summary>
    public sealed class SshAgentKey : ISshHostKey
    {

        private readonly SshAgentClient  agent;
        private readonly Byte[]          blob;
        private readonly String[]        algorithms;

        internal SshAgentKey(SshAgentClient Agent, Byte[] PublicKeyBlob)
        {
            this.agent       = Agent;
            this.blob        = PublicKeyBlob;
            this.algorithms  = AlgorithmsFor(PublicKeyBlob);
        }

        /// <inheritdoc />
        public IReadOnlyList<String> AlgorithmNames => algorithms;

        /// <inheritdoc />
        public Byte[] PublicKeyBlob => blob;

        /// <inheritdoc />
        public Byte[] Sign(String AlgorithmName, ReadOnlySpan<Byte> Data)
        {
            var flags = AlgorithmName switch {
                            "rsa-sha2-256" => SshAgentClient.FlagRsaSha2_256,
                            "rsa-sha2-512" => SshAgentClient.FlagRsaSha2_512,
                            _              => 0u
                        };
            return agent.SignAsync(blob, Data.ToArray(), flags).AsTask().GetAwaiter().GetResult();
        }

        private static String[] AlgorithmsFor(Byte[] Blob)
        {
            var reader  = new SshPacketReader(Blob);
            var keyType = reader.ReadString();
            return keyType switch {
                       "ssh-rsa"  => [ "rsa-sha2-512", "rsa-sha2-256" ],   // prefer the SHA-2 variants
                       _          => [ keyType ]
                   };
        }

    }


    /// <summary>Thrown on an ssh-agent protocol error.</summary>
    public sealed class SshAgentException : Exception
    {
        /// <summary>Create a new agent exception.</summary>
        public SshAgentException(String Message) : base(Message) { }
    }

}
