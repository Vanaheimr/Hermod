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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// The outcome of a server's host-key advertisement, after every newly offered key has proven
    /// possession of its private half.
    /// </summary>
    /// <param name="AnnouncedKeys">Every key blob the server advertised, in the order it sent them.</param>
    /// <param name="ProvenKeys">The subset that produced a valid <c>hostkeys-prove</c> signature — the keys that may be trusted.</param>
    /// <param name="CurrentKeyAdvertised">Whether the host key of the running session was among the advertised keys.</param>
    public readonly record struct SshHostKeyUpdate(IReadOnlyList<Byte[]>  AnnouncedKeys,
                                                   IReadOnlyList<Byte[]>  ProvenKeys,
                                                   Boolean                CurrentKeyAdvertised);


    /// <summary>
    /// The OpenSSH host-key rotation extension (<c>hostkeys-00@openssh.com</c> /
    /// <c>hostkeys-prove-00@openssh.com</c>, draft-miller-sshm-hostkey-update): after user
    /// authentication a server advertises <b>all</b> of its host keys, so a client can learn the ones
    /// it does not know yet and drop retired ones — the mechanism behind OpenSSH's
    /// <c>UpdateHostKeys</c>, which lets an operator roll a host key without every client tripping
    /// the "REMOTE HOST IDENTIFICATION HAS CHANGED" alarm.
    ///
    /// <para>
    /// The advertisement alone is <b>not</b> evidence: it arrives over a session authenticated by only
    /// one host key. A client therefore challenges every key it does not already trust, and the server
    /// answers with a signature per key over
    /// <c>"hostkeys-prove-00@openssh.com" || session-id || hostkey</c>, proving it holds each private
    /// half. Only keys with a valid signature are returned as trustworthy.
    /// </para>
    /// </summary>
    public static class SshHostKeyRotation
    {

        #region Constants

        /// <summary>The global request a server sends to advertise its host keys.</summary>
        public const String AnnouncementRequestName  = "hostkeys-00@openssh.com";

        /// <summary>The global request a client sends to challenge advertised host keys.</summary>
        public const String ProveRequestName         = "hostkeys-prove-00@openssh.com";

        #endregion


        #region EncodeKeyList(Blobs) / DecodeKeyList(Data)

        /// <summary>
        /// Encode public-key blobs as the <c>string[] hostkeys</c> payload both requests use: the blobs
        /// concatenated, each as an SSH string.
        /// </summary>
        /// <param name="Blobs">The public-key blobs.</param>
        public static Byte[] EncodeKeyList(IEnumerable<Byte[]> Blobs)
        {

            var abw = new ArrayBufferWriter<Byte>();
            var w   = new SshPacketWriter(abw);

            foreach (var blob in Blobs)
                w.WriteBinaryString(blob);

            return abw.WrittenSpan.ToArray();

        }

        /// <summary>Whether a key blob appears in an advertised key set.</summary>
        /// <param name="AnnouncedKeys">The advertised key blobs.</param>
        /// <param name="KeyBlob">The key blob to look for; null is never advertised.</param>
        public static Boolean Advertises(IReadOnlyList<Byte[]> AnnouncedKeys, Byte[]? KeyBlob)
            => KeyBlob is not null &&
               AnnouncedKeys.Any(blob => blob.AsSpan().SequenceEqual(KeyBlob));

        /// <summary>Decode a <c>string[] hostkeys</c> payload back into the individual blobs.</summary>
        /// <param name="Data">The request payload following the request name and want-reply flag.</param>
        public static IReadOnlyList<Byte[]> DecodeKeyList(Byte[] Data)
        {

            var reader = new SshPacketReader(Data);
            var blobs  = new List<Byte[]>();

            while (reader.HasMoreData)
                blobs.Add(reader.ReadBinaryString());

            return blobs;

        }

        #endregion

        #region ProofData(SessionId, HostKeyBlob)

        /// <summary>
        /// Build the exact byte sequence a <c>hostkeys-prove</c> signature is computed over:
        /// <c>string "hostkeys-prove-00@openssh.com" || string session-id || string hostkey</c>.
        /// </summary>
        /// <param name="SessionId">The session identifier (the first exchange hash).</param>
        /// <param name="HostKeyBlob">The advertised public-key blob being proven.</param>
        public static Byte[] ProofData(ReadOnlySpan<Byte> SessionId, ReadOnlySpan<Byte> HostKeyBlob)
        {

            var abw = new ArrayBufferWriter<Byte>();
            var w   = new SshPacketWriter(abw);

            w.WriteString      (ProveRequestName);
            w.WriteBinaryString(SessionId);
            w.WriteBinaryString(HostKeyBlob);

            return abw.WrittenSpan.ToArray();

        }

        #endregion


        #region (server) Announce / Prove

        /// <summary>
        /// Advertise every host key this server holds (<c>hostkeys-00@openssh.com</c>, no reply expected).
        /// Send this once user authentication has completed.
        /// </summary>
        /// <param name="Multiplexer">The connection multiplexer.</param>
        /// <param name="HostKeys">All host keys the server serves.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public static async ValueTask AnnounceAsync(SshChannelMultiplexer       Multiplexer,
                                                    IReadOnlyList<ISshHostKey>  HostKeys,
                                                    CancellationToken           CancellationToken   = default)
        {

            if (HostKeys.Count == 0)
                return;

            await Multiplexer.SendGlobalRequestAsync(
                      AnnouncementRequestName,
                      false,
                      EncodeKeyList(HostKeys.Select(key => key.PublicKeyBlob)),
                      CancellationToken
                  ).ConfigureAwait(false);

        }

        /// <summary>
        /// Answer a <c>hostkeys-prove-00@openssh.com</c> challenge: sign the requested key blobs, one
        /// signature per key, in the order requested. Returns null if any requested key is not ours —
        /// the caller must then answer REQUEST_FAILURE rather than signing something else.
        /// </summary>
        /// <param name="RequestedBlobs">The key blobs the client wants proven.</param>
        /// <param name="HostKeys">The host keys this server holds.</param>
        /// <param name="SessionId">The session identifier.</param>
        public static Byte[]? SignProofs(IReadOnlyList<Byte[]>       RequestedBlobs,
                                         IReadOnlyList<ISshHostKey>  HostKeys,
                                         ReadOnlySpan<Byte>          SessionId)
        {

            if (RequestedBlobs.Count == 0)
                return null;

            var abw = new ArrayBufferWriter<Byte>();
            var w   = new SshPacketWriter(abw);

            foreach (var blob in RequestedBlobs)
            {

                // Only ever sign for a key we actually hold — never for an arbitrary blob a peer supplies.
                var key = HostKeys.FirstOrDefault(k => k.PublicKeyBlob.AsSpan().SequenceEqual(blob));
                if (key is null)
                    return null;

                w.WriteBinaryString(key.Sign(key.AlgorithmNames[0], ProofData(SessionId, blob)));

            }

            return abw.WrittenSpan.ToArray();

        }

        /// <summary>
        /// Wire the server side onto a multiplexer: answer <c>hostkeys-prove-00@openssh.com</c>
        /// challenges with signatures. Requests for keys we do not hold are refused. Other global
        /// requests fall through untouched, so this composes with e.g. remote forwarding.
        /// </summary>
        /// <param name="Multiplexer">The connection multiplexer.</param>
        /// <param name="HostKeys">The host keys this server holds.</param>
        public static void ServeHostKeyProofs(SshChannelMultiplexer       Multiplexer,
                                              IReadOnlyList<ISshHostKey>  HostKeys)
        {

            var sessionId = Multiplexer.SessionId;

            Multiplexer.GlobalRequestResponder = info =>
            {

                if (info.Name != ProveRequestName)
                    return ValueTask.FromResult<SshGlobalRequestReply?>(null);   // not ours

                try
                {
                    var signatures = SignProofs(DecodeKeyList(info.Data), HostKeys, sessionId);
                    return ValueTask.FromResult<SshGlobalRequestReply?>(
                               signatures is null
                                   ? SshGlobalRequestReply.Failed
                                   : SshGlobalRequestReply.WithData(signatures));
                }
                catch (SshWireException)
                {
                    return ValueTask.FromResult<SshGlobalRequestReply?>(SshGlobalRequestReply.Failed);
                }

            };

        }

        #endregion

        #region (client) VerifyProofs / RequestProofAsync / HandleAnnouncementAsync

        /// <summary>
        /// Verify a <c>hostkeys-prove</c> reply: every requested key must be answered by a valid
        /// signature, in the same order and in the same number. Returns the proven blobs, or an empty
        /// list if anything about the reply fails to check out.
        /// </summary>
        /// <param name="RequestedBlobs">The key blobs that were challenged.</param>
        /// <param name="SignaturePayload">The REQUEST_SUCCESS payload (<c>string[] signatures</c>).</param>
        /// <param name="SessionId">The session identifier.</param>
        public static IReadOnlyList<Byte[]> VerifyProofs(IReadOnlyList<Byte[]>  RequestedBlobs,
                                                         Byte[]                 SignaturePayload,
                                                         ReadOnlySpan<Byte>     SessionId)
        {

            List<Byte[]> signatures;
            try                        { signatures = [.. DecodeKeyList(SignaturePayload)]; }
            catch (SshWireException)    { return []; }

            // "The number of signatures present MUST be identical to the number of host keys
            //  requested and they MUST appear in identical order."
            if (signatures.Count != RequestedBlobs.Count)
                return [];

            var proven = new List<Byte[]>();

            for (var i = 0; i < RequestedBlobs.Count; i++)
            {
                if (!SshSignature.Verify(RequestedBlobs[i], ProofData(SessionId, RequestedBlobs[i]), signatures[i]))
                    return [];   // one bad signature invalidates the whole update
                proven.Add(RequestedBlobs[i]);
            }

            return proven;

        }

        /// <summary>
        /// Challenge the given advertised keys (<c>hostkeys-prove-00@openssh.com</c>) and return the
        /// ones that proved possession of their private half.
        /// </summary>
        /// <param name="Multiplexer">The connection multiplexer.</param>
        /// <param name="Blobs">The advertised key blobs to challenge.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public static async ValueTask<IReadOnlyList<Byte[]>> RequestProofAsync(SshChannelMultiplexer  Multiplexer,
                                                                               IReadOnlyList<Byte[]>  Blobs,
                                                                               CancellationToken      CancellationToken   = default)
        {

            if (Blobs.Count == 0)
                return [];

            var reply = await Multiplexer.SendGlobalRequestWithReplyAsync(
                                  ProveRequestName,
                                  true,
                                  EncodeKeyList(Blobs),
                                  CancellationToken
                              ).ConfigureAwait(false);

            return reply.Success
                       ? VerifyProofs(Blobs, reply.Data, Multiplexer.SessionId)
                       : [];

        }

        /// <summary>
        /// Handle a server's <c>hostkeys-00@openssh.com</c> advertisement: challenge every advertised
        /// key and report which ones proved themselves.
        ///
        /// <para>
        /// The host key that authenticated the running session must itself appear in the
        /// advertisement — OpenSSH enforces the same rule. A server that omits it is not describing the
        /// key set the client already trusts, so the update is rejected wholesale
        /// (<see cref="SshHostKeyUpdate.ProvenKeys"/> comes back empty) rather than risking a client
        /// replacing a good key with an attacker-chosen one.
        /// </para>
        /// </summary>
        /// <param name="Multiplexer">The connection multiplexer.</param>
        /// <param name="AnnouncementData">The advertisement payload.</param>
        /// <param name="CurrentHostKeyBlob">The host-key blob that was verified during the handshake.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public static async ValueTask<SshHostKeyUpdate> HandleAnnouncementAsync(SshChannelMultiplexer  Multiplexer,
                                                                                Byte[]                 AnnouncementData,
                                                                                Byte[]?                CurrentHostKeyBlob,
                                                                                CancellationToken      CancellationToken   = default)
        {

            IReadOnlyList<Byte[]> announced;
            try                       { announced = DecodeKeyList(AnnouncementData); }
            catch (SshWireException)   { return new SshHostKeyUpdate([], [], false); }

            if (!Advertises(announced, CurrentHostKeyBlob))
                return new SshHostKeyUpdate(announced, [], false);

            var proven = await RequestProofAsync(Multiplexer, announced, CancellationToken).ConfigureAwait(false);

            return new SshHostKeyUpdate(announced, proven, true);

        }

        #endregion

    }

}
