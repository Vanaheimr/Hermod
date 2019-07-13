/*
 * Copyright (c) 2010-2019, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <http://www.github.com/Vanaheimr/Hermod>
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

    public enum ContentTransferEncoding2
    {

        /// <summary>
        /// The default encoding (aka no encoding at all).
        /// </summary>
        Default,

        /// <summary>
        /// The 7bit content transfer encoding.
        /// </summary>
        /// <remarks>
        /// This encoding should be restricted to textual content
        /// in the US-ASCII range.
        /// </remarks>
        SevenBit,

        /// <summary>
        /// The 8bit content transfer encoding.
        /// </summary>
        /// <remarks>
        /// This encoding should be restricted to textual content
        /// outside of the US-ASCII range but may not be supported
        /// by all transport services such as older SMTP servers
        /// that do not support the 8BITMIME extension.
        /// </remarks>
        EightBit,

        /// <summary>
        /// The binary content transfer encoding.
        /// </summary>
        /// <remarks>
        /// This encoding is simply unencoded binary data. Typically not
        /// supported by standard message transport services such as SMTP.
        /// </remarks>
        Binary,

        /// <summary>
        /// The base64 content transfer encoding.
        /// <seealso cref="MimeKit.Encodings.Base64Encoder"/>.
        /// </summary>
        /// <remarks>
        /// This encoding is typically used for encoding binary data
        /// or textual content in a largely 8bit charset encoding and
        /// is supported by all message transport services.
        /// </remarks>
        Base64,

        /// <summary>
        /// The quoted printable content transfer encoding.
        /// <seealso cref="MimeKit.Encodings.QuotedPrintableEncoder"/>.
        /// </summary>
        /// <remarks>
        /// This encoding is used for textual content that is in a charset
        /// that has a minority of characters outside of the US-ASCII range
        /// (such as ISO-8859-1 and other single-byte charset encodings) and
        /// is supported by all message transport services.
        /// </remarks>
        QuotedPrintable,

        /// <summary>
        /// The uuencode content transfer encoding.
        /// <seealso cref="MimeKit.Encodings.UUEncoder"/>.
        /// </summary>
        /// <remarks>
        /// This is an obsolete encoding meant for encoding binary
        /// data and has largely been superceeded by <see cref="Base64"/>.
        /// </remarks>
        UUEncode

    }

}
