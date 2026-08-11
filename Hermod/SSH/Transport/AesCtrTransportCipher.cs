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

using System.Runtime.InteropServices;
using System.Security.Cryptography;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// The <c>aes256-ctr</c> / <c>aes192-ctr</c> / <c>aes128-ctr</c> stream cipher (RFC 4344), built on
    /// the BCL AES-ECB transform: each 16-byte counter block is AES-encrypted to produce keystream that
    /// is XORed with the data. Not authenticating on its own — always paired with a separate
    /// <see cref="ISshMac"/> in encrypt-then-MAC mode.
    /// </summary>
    /// <remarks>
    /// Directional and stateful: the 128-bit counter (seeded from the KDF IV) advances by one per block.
    /// </remarks>
    public sealed class AesCtrTransportCipher : SshTransportCipher
    {

        #region Data

        /// <summary>The AES block / counter size in bytes.</summary>
        public const Int32  CounterLength = 16;

        // How many counter blocks are encrypted per AES call. One block per call meant 2048 separate
        // EncryptEcb invocations for a 32 KiB record — the per-call overhead dominated everything, and
        // AES-NI never got a run of blocks long enough to pipeline. Batching amortises both.
        private const Int32 BatchBlocks = 64;               // 1 KiB of keystream per AES call

        private readonly Aes     aes;
        private readonly Byte[]  counter;
        private readonly Byte[]  counterBlocks;             // BatchBlocks successive counters
        private readonly Byte[]  keystream;                 // their encryption
        private          Int32   keystreamPosition;

        #endregion

        #region Properties

        public override Int32    BlockSize                          => 16;
        public override Int32    TagLength                          => 0;
        // Encrypt-then-MAC: the packet_length is sent in the clear, so it is excluded from the alignment.
        public override Boolean  LengthIncludedInPaddingAlignment   => false;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create an AES-CTR cipher for one direction.
        /// </summary>
        /// <param name="Key">The 16-, 24- or 32-byte AES key (from the KDF).</param>
        /// <param name="InitialCounter">The 16-byte initial counter / IV (from the KDF).</param>
        public AesCtrTransportCipher(ReadOnlySpan<Byte>  Key,
                                     ReadOnlySpan<Byte>  InitialCounter)
        {

            if (Key.Length is not 16 and not 24 and not 32)
                throw new ArgumentException("An AES key must be 16, 24 or 32 bytes!", nameof(Key));

            if (InitialCounter.Length != CounterLength)
                throw new ArgumentException($"An AES-CTR counter must be {CounterLength} bytes!", nameof(InitialCounter));

            this.aes                = Aes.Create();
            this.aes.Mode           = CipherMode.ECB;
            this.aes.Padding        = PaddingMode.None;
            this.aes.Key            = Key.ToArray();

            this.counter            = InitialCounter.ToArray();
            this.counterBlocks      = new Byte[BatchBlocks * CounterLength];
            this.keystream          = new Byte[BatchBlocks * CounterLength];
            this.keystreamPosition  = BatchBlocks * CounterLength;   // force a refill on first use

        }

        #endregion


        #region Encrypt / Decrypt (CTR is symmetric)

        public override void Encrypt(ReadOnlySpan<Byte>  LengthBytes,
                                     ReadOnlySpan<Byte>  Plaintext,
                                     Span<Byte>          Output)

            => Process(Plaintext, Output);

        public override Boolean Decrypt(ReadOnlySpan<Byte>  LengthBytes,
                                        ReadOnlySpan<Byte>  Input,
                                        Span<Byte>          Plaintext)
        {
            Process(Input, Plaintext);
            return true;   // authentication is done by the paired MAC, not here
        }

        #endregion

        #region (private) Process(Input, Output)

        private void Process(ReadOnlySpan<Byte> Input, Span<Byte> Output)
        {

            var offset = 0;

            while (offset < Input.Length)
            {

                if (keystreamPosition == keystream.Length)
                    Refill();

                // The keystream is one continuous stream whose position carries across packets, so
                // generating it in batches is exactly equivalent to generating it a block at a time —
                // any blocks left over at the end of a record are consumed by the next one.
                var take = Math.Min(Input.Length - offset, keystream.Length - keystreamPosition);

                Xor(Input.Slice(offset, take),
                    keystream.AsSpan(keystreamPosition, take),
                    Output.Slice(offset, take));

                offset             += take;
                keystreamPosition  += take;

            }

        }

        // Encrypt BatchBlocks successive counter values in a single AES call.
        private void Refill()
        {

            for (var block = 0; block < BatchBlocks; block++)
            {
                counter.CopyTo(counterBlocks.AsSpan(block * CounterLength, CounterLength));
                IncrementCounter();
            }

            aes.EncryptEcb(counterBlocks, keystream, PaddingMode.None);
            keystreamPosition = 0;

        }

        // XOR eight bytes at a time; the byte-wise loop was a measurable share of the remaining cost
        // once the AES calls were batched.
        private static void Xor(ReadOnlySpan<Byte> A, ReadOnlySpan<Byte> B, Span<Byte> Destination)
        {

            var a64 = MemoryMarshal.Cast<Byte, UInt64>(A);
            var b64 = MemoryMarshal.Cast<Byte, UInt64>(B);
            var d64 = MemoryMarshal.Cast<Byte, UInt64>(Destination);

            for (var i = 0; i < d64.Length; i++)
                d64[i] = a64[i] ^ b64[i];

            for (var i = d64.Length * sizeof(UInt64); i < A.Length; i++)
                Destination[i] = (Byte) (A[i] ^ B[i]);

        }

        #endregion

        #region (private) IncrementCounter()

        // Increment the 128-bit big-endian counter by one.
        private void IncrementCounter()
        {
            for (var i = CounterLength - 1; i >= 0; i--)
            {
                if (++counter[i] != 0)
                    break;
            }
        }

        #endregion

        #region Dispose()

        public override void Dispose()
        {
            aes.Dispose();
            CryptographicOperations.ZeroMemory(counter);
            CryptographicOperations.ZeroMemory(counterBlocks);
            CryptographicOperations.ZeroMemory(keystream);
            base.Dispose();
        }

        #endregion

    }

}
