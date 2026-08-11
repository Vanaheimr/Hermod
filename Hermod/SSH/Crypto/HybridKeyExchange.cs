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
using System.Security.Cryptography;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// A post-quantum hybrid key exchange (<c>mlkem768x25519-sha256</c>, <c>sntrup761x25519-sha512</c>):
    /// a KEM (ML-KEM-768 or sntrup761) combined with X25519. The client sends
    /// <c>kem_public || x25519_public</c>; the server answers with <c>kem_ciphertext || x25519_public</c>;
    /// the shared secret is <c>HASH(K_kem || K_x25519)</c> (OpenSSH PROTOCOL, draft-kampanakis-curdle-ssh-pq-ke).
    /// </summary>
    /// <remarks>
    /// The concatenation order everywhere is PQ-first: the KEM part precedes the X25519 part both on the
    /// wire and inside the hash. Unlike the classical methods, the shared secret K (the hash output) is
    /// encoded as an SSH <b>string</b>, not an mpint — see <see cref="EncodeSharedSecret"/>.
    /// </remarks>
    public sealed class HybridKeyExchange : SshKeyExchange
    {

        #region Data

        private const Int32 X25519Length = 32;

        private readonly SshKem         kem;
        private readonly X25519KeyPair  x25519 = X25519KeyPair.Generate();
        private SshKemKeyPair?          clientKemKeyPair;

        #endregion

        #region Properties

        public override String             Name           { get; }
        public override HashAlgorithmName  HashAlgorithm  { get; }

        #endregion

        #region Constructor(s)

        /// <summary>Create a fresh hybrid key exchange over the given KEM and hash.</summary>
        public HybridKeyExchange(SshKem Kem, HashAlgorithmName HashAlgorithm, String Name)
        {
            this.kem            = Kem;
            this.HashAlgorithm  = HashAlgorithm;
            this.Name           = Name;
        }

        #endregion


        #region StartClient()

        public override Byte[] StartClient()
        {
            clientKemKeyPair = kem.GenerateKeyPair();
            return Concat(clientKemKeyPair.PublicKey, x25519.PublicKey);
        }

        #endregion

        #region ServerRespond(ClientPublic)

        public override (Byte[] ServerPublic, Byte[] SharedSecret) ServerRespond(ReadOnlySpan<Byte> ClientPublic)
        {

            if (ClientPublic.Length != kem.PublicKeyLength + X25519Length)
                throw new SshWireException($"Invalid {Name} client public value (expected {kem.PublicKeyLength + X25519Length} bytes, got {ClientPublic.Length}).");

            var kemPublic       = ClientPublic[..kem.PublicKeyLength];
            var x25519Public    = ClientPublic[kem.PublicKeyLength..];

            var (ciphertext, kKem)  = kem.Encapsulate(kemPublic);
            var kX25519             = x25519.Agree(x25519Public);

            var serverPublic    = Concat(ciphertext, x25519.PublicKey);
            var sharedSecret    = HashConcat(kKem, kX25519);

            return (serverPublic, sharedSecret);

        }

        #endregion

        #region ClientFinish(ServerPublic)

        public override Byte[] ClientFinish(ReadOnlySpan<Byte> ServerPublic)
        {

            if (clientKemKeyPair is null)
                throw new InvalidOperationException("ClientFinish was called before StartClient.");

            if (ServerPublic.Length != kem.CiphertextLength + X25519Length)
                throw new SshWireException($"Invalid {Name} server public value (expected {kem.CiphertextLength + X25519Length} bytes, got {ServerPublic.Length}).");

            var ciphertext      = ServerPublic[..kem.CiphertextLength];
            var x25519Public    = ServerPublic[kem.CiphertextLength..];

            var kKem            = clientKemKeyPair.Decapsulate(ciphertext);
            var kX25519         = x25519.Agree(x25519Public);

            return HashConcat(kKem, kX25519);

        }

        #endregion

        #region EncodeSharedSecret(RawSharedSecret)

        // The PQ hybrids encode the hash output K as an SSH string (length-prefixed bytes), NOT an mpint.
        public override Byte[] EncodeSharedSecret(ReadOnlySpan<Byte> RawSharedSecret)
        {
            var abw     = new ArrayBufferWriter<Byte>();
            var writer  = new SshPacketWriter(abw);
            writer.WriteBinaryString(RawSharedSecret);
            return abw.WrittenSpan.ToArray();
        }

        #endregion


        #region (private) HashConcat / Concat

        // K = HASH(K_kem || K_x25519).
        private Byte[] HashConcat(ReadOnlySpan<Byte> Pq, ReadOnlySpan<Byte> Classical)
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithm);
            hash.AppendData(Pq);
            hash.AppendData(Classical);
            return hash.GetHashAndReset();
        }

        private static Byte[] Concat(ReadOnlySpan<Byte> First, ReadOnlySpan<Byte> Second)
        {
            var result = new Byte[First.Length + Second.Length];
            First. CopyTo(result);
            Second.CopyTo(result.AsSpan(First.Length));
            return result;
        }

        #endregion

        #region Dispose()

        public override void Dispose()
        {
            clientKemKeyPair?.Dispose();
            base.Dispose();
        }

        #endregion

    }

}
