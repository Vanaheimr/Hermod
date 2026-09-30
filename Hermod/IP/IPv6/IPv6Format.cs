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

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// How an IPv6 address is written out: see <see cref="IPv6Address.ToString(IPv6Format)"/>.
    /// </summary>
    /// <remarks>
    /// Both forms are bare - no brackets, which belong to the authority of an
    /// URL (see <see cref="IIPAddressExtensions.ToIPLiteral"/>) - and both end in
    /// "%" and the interface where an address has one. Either one is read back
    /// by <see cref="IPv6Address.Parse(String)"/> as the same address.
    /// </remarks>
    public enum IPv6Format
    {

        /// <summary>
        /// Every one of the eight groups with all four of its hexadecimal
        /// digits, in lower case: "2001:0db8:0000:0000:0000:0000:0000:0001".
        /// Always 39 characters before an interface, which lines addresses up
        /// in a column - and "::1" is "0000:0000:0000:0000:0000:0000:0000:0001"
        /// here, not the "[::1]" of <see cref="IPv6Address.ToString()"/>.
        /// </summary>
        Long,

        /// <summary>
        /// The text RFC 5952 recommends, which is what people read and write:
        /// "2001:db8::1". No group has a leading zero (section 4.1); the
        /// longest run of zero groups is written "::", the first of two equally
        /// long ones, and never a single zero group (4.2); the digits are in
        /// lower case (4.3); and an IPv4-mapped address ends in its IPv4
        /// address in dotted decimal, "::ffff:192.0.2.128" (5).
        /// </summary>
        Short

    }

}
