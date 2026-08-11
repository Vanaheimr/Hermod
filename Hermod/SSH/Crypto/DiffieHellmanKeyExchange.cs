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

using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// The classic finite-field <c>diffie-hellman-group14-sha256</c> and
    /// <c>diffie-hellman-group16-sha512</c> key exchanges (RFC 4253 / RFC 8268) over the fixed MODP
    /// groups 14 (2048-bit) and 16 (4096-bit) from RFC 3526, with generator g = 2.
    /// </summary>
    /// <remarks>
    /// The public values e (client) and f (server) are SSH mpints on the wire. Their mpint content is
    /// the signed, big-endian two's-complement encoding of a positive integer — which is exactly what
    /// <see cref="SshPacketWriter.WriteBinaryString"/> emits for these content bytes — so this exchange
    /// reuses the ECDH message builders and the exchange-hash writer unchanged (both KEXDH and KEX_ECDH
    /// share the message numbers 30 and 31).
    /// </remarks>
    public sealed class DiffieHellmanKeyExchange : SshKeyExchange
    {

        #region Data

        private readonly BigInteger  p;
        private readonly BigInteger  g;
        private readonly BigInteger  x;   // our secret exponent
        private readonly Byte[]      publicKey;

        #endregion

        #region Properties

        public override String             Name           { get; }
        public override HashAlgorithmName  HashAlgorithm  { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a fresh classic DH key exchange over the given MODP prime.
        /// </summary>
        public DiffieHellmanKeyExchange(BigInteger Prime, HashAlgorithmName HashAlgorithm, String Name)
        {

            this.p              = Prime;
            this.g              = 2;
            this.Name           = Name;
            this.HashAlgorithm  = HashAlgorithm;

            // A secret exponent x with 2 <= x <= p-2, drawn from the full width of p.
            this.x              = GenerateExponent(p);

            // e = g^x mod p, encoded as mpint content (signed big-endian => positive integer).
            var e               = BigInteger.ModPow(g, x, p);
            this.publicKey      = e.ToByteArray(isUnsigned: false, isBigEndian: true);

        }

        #endregion


        #region (private static) GenerateExponent(P)

        private static BigInteger GenerateExponent(BigInteger P)
        {

            // Draw a random value across the full byte-width of p, then map it into [2, p-2].
            var bytes  = new Byte[P.GetByteCount(isUnsigned: true)];
            var span   = P - 3;   // range size for [0, p-4] -> shifted to [2, p-2]

            while (true)
            {
                RandomNumberGenerator.Fill(bytes);
                var r = new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
                var x = (r % span) + 2;
                if (x >= 2 && x <= P - 2)
                    return x;
            }

        }

        #endregion

        #region StartClient / ServerRespond / ClientFinish

        // The DH public value (e for the client, f for the server) is computed identically on both sides.
        public override Byte[] StartClient()
            => publicKey;

        public override (Byte[] ServerPublic, Byte[] SharedSecret) ServerRespond(ReadOnlySpan<Byte> ClientPublic)
            => (publicKey, Agree(ClientPublic));

        public override Byte[] ClientFinish(ReadOnlySpan<Byte> ServerPublic)
            => Agree(ServerPublic);

        private Byte[] Agree(ReadOnlySpan<Byte> PeerPublicKey)
        {

            // The peer value arrives as mpint content bytes; interpret it as an unsigned integer.
            var peer = new BigInteger(PeerPublicKey, isUnsigned: true, isBigEndian: true);

            // RFC 4253 §8: reject values outside 1 < peer < p-1 (small-subgroup / degenerate values).
            if (peer <= BigInteger.One || peer >= p - BigInteger.One)
                throw new SshWireException($"Invalid Diffie-Hellman public value for {Name} (must satisfy 1 < value < p-1).");

            var secret = BigInteger.ModPow(peer, x, p);

            // Return the raw unsigned big-endian magnitude; the caller mpint-encodes it as K.
            return secret.ToByteArray(isUnsigned: true, isBigEndian: true);

        }

        #endregion


        #region (static) MODP groups (RFC 3526)

        /// <summary>RFC 3526 §3 — the 2048-bit MODP Group (id 14) prime, generator g = 2.</summary>
        public static readonly BigInteger Group14Prime = ParseHex(
            "FFFFFFFFFFFFFFFFC90FDAA22168C234C4C6628B80DC1CD1" +
            "29024E088A67CC74020BBEA63B139B22514A08798E3404DD" +
            "EF9519B3CD3A431B302B0A6DF25F14374FE1356D6D51C245" +
            "E485B576625E7EC6F44C42E9A637ED6B0BFF5CB6F406B7ED" +
            "EE386BFB5A899FA5AE9F24117C4B1FE649286651ECE45B3D" +
            "C2007CB8A163BF0598DA48361C55D39A69163FA8FD24CF5F" +
            "83655D23DCA3AD961C62F356208552BB9ED529077096966D" +
            "670C354E4ABC9804F1746C08CA18217C32905E462E36CE3B" +
            "E39E772C180E86039B2783A2EC07A28FB5C55DF06F4C52C9" +
            "DE2BCBF6955817183995497CEA956AE515D2261898FA0510" +
            "15728E5A8AACAA68FFFFFFFFFFFFFFFF");

        /// <summary>RFC 3526 §5 — the 4096-bit MODP Group (id 16) prime, generator g = 2.</summary>
        public static readonly BigInteger Group16Prime = ParseHex(
            "FFFFFFFFFFFFFFFFC90FDAA22168C234C4C6628B80DC1CD1" +
            "29024E088A67CC74020BBEA63B139B22514A08798E3404DD" +
            "EF9519B3CD3A431B302B0A6DF25F14374FE1356D6D51C245" +
            "E485B576625E7EC6F44C42E9A637ED6B0BFF5CB6F406B7ED" +
            "EE386BFB5A899FA5AE9F24117C4B1FE649286651ECE45B3D" +
            "C2007CB8A163BF0598DA48361C55D39A69163FA8FD24CF5F" +
            "83655D23DCA3AD961C62F356208552BB9ED529077096966D" +
            "670C354E4ABC9804F1746C08CA18217C32905E462E36CE3B" +
            "E39E772C180E86039B2783A2EC07A28FB5C55DF06F4C52C9" +
            "DE2BCBF6955817183995497CEA956AE515D2261898FA0510" +
            "15728E5A8AAAC42DAD33170D04507A33A85521ABDF1CBA64" +
            "ECFB850458DBEF0A8AEA71575D060C7DB3970F85A6E1E4C7" +
            "ABF5AE8CDB0933D71E8C94E04A25619DCEE3D2261AD2EE6B" +
            "F12FFA06D98A0864D87602733EC86A64521F2B18177B200C" +
            "BBE117577A615D6C770988C0BAD946E208E24FA074E5AB31" +
            "43DB5BFCE0FD108E4B82D120A92108011A723C12A787E6D7" +
            "88719A10BDBA5B2699C327186AF4E23C1A946834B6150BDA" +
            "2583E9CA2AD44CE8DBBBC2DB04DE8EF92E8EFC141FBECAA6" +
            "287C59474E6BC05D99B2964FA090C3A2233BA186515BE7ED" +
            "1F612970CEE2D7AFB81BDD762170481CD0069127D5B05AA9" +
            "93B4EA988D8FDDC186FFB7DC90A6C08F4DF435C934063199" +
            "FFFFFFFFFFFFFFFF");

        private static BigInteger ParseHex(String Hex)
            // Prefix a zero nibble so the leading 0xFF is never read as a negative sign bit.
            => BigInteger.Parse("0" + Hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        #endregion

    }

}
