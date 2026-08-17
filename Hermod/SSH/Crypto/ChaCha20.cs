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

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// The ChaCha20 stream cipher core (Bernstein's original 64-bit-nonce variant, as used by OpenSSH's
    /// <c>chacha20-poly1305@openssh.com</c>), vectorised with <see cref="Vector128{T}"/>.
    ///
    /// <para>
    /// The quarter-round is applied to the four state rows in parallel — the standard SSE/NEON layout —
    /// so one <c>Vector128&lt;UInt32&gt;</c> holds one row and a double round becomes eight vector
    /// operations plus two lane rotations. <see cref="Vector128"/> is hardware-agnostic: the JIT emits
    /// NEON on ARM and SSE2/AVX on x86, and falls back to a software path where neither exists, so this
    /// is a single implementation rather than per-architecture intrinsics.
    /// </para>
    ///
    /// <para>
    /// This replaces BouncyCastle's scalar <c>ChaChaEngine</c>, which benchmarking showed to be the
    /// throughput ceiling for the whole SSH transport (see <c>docs/BENCHMARKS.md</c>) — the relevant one
    /// on ARM targets, where ChaCha20 is rightly preferred over AES for lack of AES acceleration.
    /// </para>
    ///
    /// <para>
    /// Verified against the RFC 8439 §2.3.2 block-function vector and the §2.4.2 encryption vector; the
    /// wire construction on top is additionally checked against real OpenSSH by the interop suite.
    /// </para>
    /// </summary>
    public static class ChaCha20
    {

        #region Constants

        /// <summary>
        /// The keystream block size in bytes.
        /// </summary>
        public const Int32 BlockSize = 64;

        /// <summary>
        /// The key size in bytes.
        /// </summary>
        public const Int32 KeySize   = 32;

        /// <summary>
        /// The nonce size in bytes (Bernstein's original 64-bit nonce).
        /// </summary>
        public const Int32 NonceSize = 8;

        // "expand 32-byte k"
        private const UInt32 Sigma0 = 0x61707865;
        private const UInt32 Sigma1 = 0x3320646e;
        private const UInt32 Sigma2 = 0x79622d32;
        private const UInt32 Sigma3 = 0x6b206574;

        private static readonly Vector128<UInt32> RotateLeftLanes1  = Vector128.Create(1u, 2u, 3u, 0u);
        private static readonly Vector128<UInt32> RotateLeftLanes2  = Vector128.Create(2u, 3u, 0u, 1u);
        private static readonly Vector128<UInt32> RotateLeftLanes3  = Vector128.Create(3u, 0u, 1u, 2u);

        #endregion


        #region Block(State, Output)

        /// <summary>
        /// The raw ChaCha20 block function: 20 rounds over a 16-word state, added to the original state
        /// and serialised little-endian. Exposed so it can be checked directly against the published
        /// test vectors.
        /// </summary>
        /// <param name="State">The 16-word input state (constants, key, counter, nonce).</param>
        /// <param name="Output">Receives the 64-byte keystream block.</param>
        public static void Block(ReadOnlySpan<UInt32> State, Span<Byte> Output)
        {

            if (State.Length < 16)
                throw new ArgumentException("A ChaCha20 state is 16 words.", nameof(State));
            if (Output.Length < BlockSize)
                throw new ArgumentException($"A ChaCha20 block is {BlockSize} bytes.", nameof(Output));

            var a = Vector128.Create(State[ 0], State[ 1], State[ 2], State[ 3]);
            var b = Vector128.Create(State[ 4], State[ 5], State[ 6], State[ 7]);
            var c = Vector128.Create(State[ 8], State[ 9], State[10], State[11]);
            var d = Vector128.Create(State[12], State[13], State[14], State[15]);

            var (a0, b0, c0, d0) = (a, b, c, d);

            for (var i = 0; i < 10; i++)
            {

                // Column round.
                QuarterRound(ref a, ref b, ref c, ref d);

                // Diagonalise: rows b/c/d rotate by 1/2/3 lanes so the columns become the diagonals.
                b = Vector128.Shuffle(b, RotateLeftLanes1);
                c = Vector128.Shuffle(c, RotateLeftLanes2);
                d = Vector128.Shuffle(d, RotateLeftLanes3);

                // Diagonal round.
                QuarterRound(ref a, ref b, ref c, ref d);

                // Undiagonalise.
                b = Vector128.Shuffle(b, RotateLeftLanes3);
                c = Vector128.Shuffle(c, RotateLeftLanes2);
                d = Vector128.Shuffle(d, RotateLeftLanes1);

            }

            Serialize(a + a0, Output[  ..16]);
            Serialize(b + b0, Output[16..32]);
            Serialize(c + c0, Output[32..48]);
            Serialize(d + d0, Output[48..64]);

        }

        #endregion

        #region Xor(Key, Nonce, Counter, Input, Output)

        /// <summary>
        /// XOR <paramref name="Input"/> with the keystream for the given key, 64-bit nonce and starting
        /// 64-bit block counter. In-place is allowed (<paramref name="Output"/> may alias
        /// <paramref name="Input"/>).
        /// </summary>
        /// <param name="Key">The 32-byte key.</param>
        /// <param name="Nonce">
        /// The 8-byte nonce, read as two little-endian words into state words 14/15 — the same
        /// interpretation OpenSSH and BouncyCastle use. Deliberately bytes rather than a UInt64: the
        /// numeric form leaves the byte order of the caller's value implicit, and getting it wrong
        /// produces a perfectly self-consistent cipher that no other implementation can talk to.
        /// </param>
        /// <param name="Counter">The block counter to start at.</param>
        /// <param name="Input">The data to transform.</param>
        /// <param name="Output">Receives the transformed data; must be at least as long as the input.</param>
        public static void Xor(ReadOnlySpan<Byte>  Key,
                               ReadOnlySpan<Byte>  Nonce,
                               UInt64              Counter,
                               ReadOnlySpan<Byte>  Input,
                               Span<Byte>          Output)
        {

            if (Key.Length != KeySize)
                throw new ArgumentException($"A ChaCha20 key is {KeySize} bytes.", nameof(Key));
            if (Nonce.Length != NonceSize)
                throw new ArgumentException($"A ChaCha20 nonce is {NonceSize} bytes.", nameof(Nonce));
            if (Output.Length < Input.Length)
                throw new ArgumentException("The output is shorter than the input.", nameof(Output));

            Span<UInt32> state = stackalloc UInt32[16];
            InitState(state, Key, Nonce, Counter);

            Span<Byte> keystream = stackalloc Byte[BlockSize];

            var offset = 0;
            while (offset < Input.Length)
            {

                Block(state, keystream);

                var take = Math.Min(BlockSize, Input.Length - offset);
                for (var i = 0; i < take; i++)
                    Output[offset + i] = (Byte) (Input[offset + i] ^ keystream[i]);

                offset += take;

                // 64-bit little-endian counter in words 12/13.
                if (++state[12] == 0)
                    state[13]++;

            }

            keystream.Clear();
            state.Clear();

        }

        #endregion

        #region Keystream(Key, Nonce, Counter, Output)

        /// <summary>
        /// Fill <paramref name="Output"/> with raw keystream (used to derive the Poly1305 key).
        /// </summary>
        /// <param name="Key">The 32-byte key.</param>
        /// <param name="Nonce">The 8-byte nonce.</param>
        /// <param name="Counter">The block counter to start at.</param>
        /// <param name="Output">Receives the keystream.</param>
        public static void Keystream(ReadOnlySpan<Byte> Key, ReadOnlySpan<Byte> Nonce, UInt64 Counter, Span<Byte> Output)
        {
            Output.Clear();
            Xor(Key, Nonce, Counter, Output, Output);
        }

        #endregion


        #region (private) helpers

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void QuarterRound(ref Vector128<UInt32> a,
                                         ref Vector128<UInt32> b,
                                         ref Vector128<UInt32> c,
                                         ref Vector128<UInt32> d)
        {
            a += b;  d ^= a;  d = RotateLeft(d, 16);
            c += d;  b ^= c;  b = RotateLeft(b, 12);
            a += b;  d ^= a;  d = RotateLeft(d,  8);
            c += d;  b ^= c;  b = RotateLeft(b,  7);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<UInt32> RotateLeft(Vector128<UInt32> Value, Int32 Bits)
            => Vector128.ShiftLeft(Value, Bits) | Vector128.ShiftRightLogical(Value, 32 - Bits);

        private static void InitState(Span<UInt32> State, ReadOnlySpan<Byte> Key, ReadOnlySpan<Byte> Nonce, UInt64 Counter)
        {

            State[0] = Sigma0;  State[1] = Sigma1;  State[2] = Sigma2;  State[3] = Sigma3;

            for (var i = 0; i < 8; i++)
                State[4 + i] = BinaryPrimitives.ReadUInt32LittleEndian(Key.Slice(i * 4, 4));

            State[12] = (UInt32)  Counter;
            State[13] = (UInt32) (Counter >> 32);
            State[14] = BinaryPrimitives.ReadUInt32LittleEndian(Nonce[..4]);
            State[15] = BinaryPrimitives.ReadUInt32LittleEndian(Nonce[4..]);

        }

        private static void Serialize(Vector128<UInt32> Row, Span<Byte> Output)
        {
            for (var i = 0; i < 4; i++)
                BinaryPrimitives.WriteUInt32LittleEndian(Output.Slice(i * 4, 4), Row[i]);
        }

        #endregion

    }

}
