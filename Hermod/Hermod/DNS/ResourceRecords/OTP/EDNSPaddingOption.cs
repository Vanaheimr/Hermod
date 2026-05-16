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

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// EDNS Padding option (RFC 7830).
    /// Adds zero-filled padding bytes to DNS-over-TLS / DNS-over-HTTPS messages
    /// to prevent traffic analysis based on message length.
    ///
    /// Wire format: N bytes, all set to 0x00.
    ///
    /// RFC 8467 recommends padding to a block size of 128 bytes.
    /// </summary>
    public class EDNSPaddingOption : EDNSOption
    {

        #region Data

        /// <summary>
        /// The RFC 8467 recommended block size for padding DNS-over-TLS messages.
        /// </summary>
        public const UInt16 RecommendedBlockSize = 128;

        #endregion

        #region Properties

        /// <summary>
        /// The number of padding bytes.
        /// </summary>
        public UInt16  PaddingLength    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new EDNS Padding option with the given number of zero bytes.
        /// </summary>
        /// <param name="PaddingLength">The number of padding bytes.</param>
        public EDNSPaddingOption(UInt16 PaddingLength)

            : base(EDNSOptionCode.Padding,
                   new Byte[PaddingLength])

        {
            this.PaddingLength = PaddingLength;
        }

        #endregion


        #region (static) Create(CurrentMessageLength, BlockSize = 128)

        /// <summary>
        /// Create a padding option that pads the DNS message up to the next
        /// multiple of the given block size (RFC 8467 recommends 128).
        /// </summary>
        /// <param name="CurrentMessageLength">The current DNS message length in bytes (before adding this option).</param>
        /// <param name="BlockSize">The block size to pad to. Default: 128 (RFC 8467).</param>
        public static EDNSPaddingOption Create(UInt32  CurrentMessageLength,
                                               UInt16  BlockSize = RecommendedBlockSize)
        {

            // The padding option itself adds 4 bytes overhead (2 code + 2 length)
            // plus the padding data bytes.
            var totalWithOverhead  = CurrentMessageLength + 4;
            var remainder          = totalWithOverhead % BlockSize;

            var paddingLength      = remainder == 0
                                         ? (UInt16) 0
                                         : (UInt16) (BlockSize - remainder);

            return new EDNSPaddingOption(paddingLength);

        }

        #endregion

        #region (static) Parse(Data)

        /// <summary>
        /// Parse an EDNS Padding option from raw data bytes.
        /// </summary>
        /// <param name="Data">The raw option data.</param>
        public new static EDNSPaddingOption Parse(Byte[] Data)

            => new ((UInt16) Data.Length);

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this EDNS Padding option.
        /// </summary>
        public override String ToString()

            => $"Padding ({PaddingLength} bytes)";

        #endregion

    }

}
