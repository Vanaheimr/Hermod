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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// zstd frames read and built byte by byte (RFC 8878, Section 3.1.1), for
    /// tests about the window a frame needs: the encoder chooses it, so a frame
    /// that needs a given one is built here, and a frame the encoder wrote is
    /// read back here.
    /// </summary>
    internal static class ZstandardFrames
    {

        private static readonly Byte[] Magic = [ 0x28, 0xB5, 0x2F, 0xFD ];

        /// <summary>
        /// The window size a Window_Descriptor states (Section 3.1.1.1.2): an
        /// exponent of five bits over 2^10, and a mantissa of three, in eighths.
        /// </summary>
        public static UInt64 WindowSize(Byte WindowDescriptor)
        {

            var windowBase = 1UL << (10 + (WindowDescriptor >> 3));

            return windowBase + windowBase / 8 * (UInt64) (WindowDescriptor & 0x07);

        }

        /// <summary>
        /// A frame of one raw block that carries <paramref name="Content"/>, and
        /// states no content size but this Window_Descriptor: a decoder has to
        /// provide that window, whatever the content.
        /// </summary>
        public static Byte[] WithWindowDescriptor(Byte WindowDescriptor, Byte[] Content)
        {

            // Last_Block, Block_Type Raw_Block, Block_Size (Section 3.1.1.2).
            var blockHeader = 1 | Content.Length << 3;

            return [ .. Magic,
                     0x00,                  // Frame_Header_Descriptor: nothing but a Window_Descriptor
                     WindowDescriptor,
                     (Byte)  blockHeader,
                     (Byte) (blockHeader >>  8),
                     (Byte) (blockHeader >> 16),
                     .. Content ];

        }

        /// <summary>
        /// Whether the frame at the start of these octets is a single segment: it
        /// states its content size and no Window_Descriptor, and its window is
        /// its content.
        /// </summary>
        public static Boolean IsSingleSegment(Byte[] Frame)

            => (DescriptorOf(Frame) & 0x20) != 0;

        /// <summary>
        /// The window the frame at the start of these octets needs: what its
        /// Window_Descriptor states, or, for a single segment, its content size.
        /// </summary>
        public static UInt64 WindowOf(Byte[] Frame)
        {

            var descriptor = DescriptorOf(Frame);

            if (!IsSingleSegment(Frame))
                return WindowSize(Frame[5]);

            var offset = 5 + (descriptor & 0x03) switch { 0 => 0, 1 => 1, 2 => 2, _ => 4 };

            return (descriptor >> 6) switch {
                       0 => Frame[offset],
                       1 => (UInt64) (Frame[offset] | Frame[offset + 1] << 8) + 256,
                       2 => BinaryPrimitives.ReadUInt32LittleEndian(Frame.AsSpan(offset)),
                       _ => BinaryPrimitives.ReadUInt64LittleEndian(Frame.AsSpan(offset))
                   };

        }

        /// <summary>
        /// 24 MiB that compress well only with a window of more than 12 MiB: one
        /// block of 12 MiB of noise, twice.
        /// </summary>
        public static Byte[] RepeatedAfter12MiB()
        {

            var block = new Byte[12 << 20];
            new Random(9659).NextBytes(block);

            return [ .. block, .. block ];

        }

        private static Byte DescriptorOf(Byte[] Frame)
        {

            if (Frame.Length < 6 || !Frame.AsSpan(0, 4).SequenceEqual(Magic))
                throw new ArgumentException("Not a zstd frame", nameof(Frame));

            return Frame[4];

        }

    }

}
