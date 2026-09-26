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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// The bare item types of RFC 9651, Section 3.3. Date and DisplayString are
    /// the two that RFC 8941 did not have, which is the practical difference
    /// between the two revisions.
    /// </summary>
    /// <seealso cref="https://www.rfc-editor.org/rfc/rfc9651.html#section-3.3"/>
    public enum SFType
    {

        /// <summary>An integer, -999,999,999,999,999 to 999,999,999,999,999.</summary>
        Integer,

        /// <summary>A decimal with at most twelve integer and three fractional digits.</summary>
        Decimal,

        /// <summary>A string of printable ASCII.</summary>
        String,

        /// <summary>A token, which is a bare word rather than a quoted string.</summary>
        Token,

        /// <summary>A byte sequence, written base64 between colons.</summary>
        ByteSequence,

        /// <summary>A boolean, written ?1 or ?0.</summary>
        Boolean,

        /// <summary>A moment, written "@" and seconds since the Unix epoch.</summary>
        Date,

        /// <summary>A string of Unicode, written percent-encoded UTF-8.</summary>
        DisplayString

    }

}
