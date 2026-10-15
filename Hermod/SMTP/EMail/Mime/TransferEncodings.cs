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

namespace org.GraphDefined.Vanaheimr.Hermod.Mail
{

    /// <summary>
    /// Undoing a Content-Transfer-Encoding (RFC 2045 §6): base64 and
    /// quoted-printable; 7bit, 8bit and binary are the octets themselves.
    /// </summary>
    /// <remarks>
    /// Both decoders are lenient, as a reader has to be: base64 skips whatever is not
    /// of its alphabet - line breaks, but also the stray characters some encoders leave
    /// - and a final group that is incomplete; quoted-printable keeps a "=" that does
    /// not start an escape as it is, and takes both CR LF and a bare LF for the end of
    /// a soft line break.
    /// </remarks>
    public static class TransferEncodings
    {

        #region Decode(Body, ContentTransferEncoding)

        /// <summary>
        /// The octets the given body encodes.
        /// </summary>
        /// <param name="Body">The body as it arrived.</param>
        /// <param name="ContentTransferEncoding">The Content-Transfer-Encoding, or null.</param>
        public static Byte[] Decode(ReadOnlySpan<Byte>  Body,
                                    String?             ContentTransferEncoding)

            => ContentTransferEncoding?.Trim().ToLowerInvariant() switch {
                   "base64"            => DecodeBase64         (Body),
                   "quoted-printable"  => DecodeQuotedPrintable(Body),
                   _                   => Body.ToArray()
               };

        #endregion


        #region DecodeBase64(Body)

        public static Byte[] DecodeBase64(ReadOnlySpan<Byte> Body)
        {

            var output   = new Byte[Body.Length * 3 / 4 + 3];
            var length   = 0;
            var quantum  = 0;
            var bits     = 0;

            foreach (var octet in Body)
            {

                var value = octet switch {
                                >= (Byte) 'A' and <= (Byte) 'Z' => octet - 'A',
                                >= (Byte) 'a' and <= (Byte) 'z' => octet - 'a' + 26,
                                >= (Byte) '0' and <= (Byte) '9' => octet - '0' + 52,
                                (Byte) '+'                     => 62,
                                (Byte) '/'                     => 63,
                                _                              => -1
                            };

                // "=" ends the data; everything else not of the alphabet is skipped.
                if (octet == '=')
                    break;

                if (value < 0)
                    continue;

                quantum = (quantum << 6) | value;
                bits   += 6;

                if (bits >= 8)
                {
                    bits -= 8;
                    output[length++] = (Byte) (quantum >> bits);
                    quantum &= (1 << bits) - 1;
                }

            }

            return output[..length];

        }

        #endregion

        #region DecodeQuotedPrintable(Body)

        public static Byte[] DecodeQuotedPrintable(ReadOnlySpan<Byte> Body)
        {

            var output = new List<Byte>(Body.Length);

            for (var i = 0; i < Body.Length; i++)
            {

                var octet = Body[i];

                if (octet != '=')
                {
                    output.Add(octet);
                    continue;
                }

                // =XX
                if (i + 2 < Body.Length && IsHex(Body[i + 1]) && IsHex(Body[i + 2]))
                {
                    output.Add((Byte) (HexValue(Body[i + 1]) << 4 | HexValue(Body[i + 2])));
                    i += 2;
                    continue;
                }

                // A soft line break: "=" with nothing but white space to the end of the line.
                var j = i + 1;
                while (j < Body.Length && Body[j] is (Byte) ' ' or (Byte) '\t')
                    j++;

                if (j < Body.Length && Body[j] == '\r' && j + 1 < Body.Length && Body[j + 1] == '\n')
                {
                    i = j + 1;
                    continue;
                }

                if (j < Body.Length && Body[j] == '\n')
                {
                    i = j;
                    continue;
                }

                if (j >= Body.Length)
                    break;

                // Not an escape: keep the "=".
                output.Add(octet);

            }

            return [.. output];

        }

        private static Boolean IsHex(Byte Octet)
            => Octet is >= (Byte) '0' and <= (Byte) '9' or >= (Byte) 'A' and <= (Byte) 'F' or >= (Byte) 'a' and <= (Byte) 'f';

        private static Int32 HexValue(Byte Octet)
            => Octet switch {
                   >= (Byte) '0' and <= (Byte) '9' => Octet - '0',
                   >= (Byte) 'A' and <= (Byte) 'F' => Octet - 'A' + 10,
                   _                              => Octet - 'a' + 10
               };

        #endregion

    }

}
