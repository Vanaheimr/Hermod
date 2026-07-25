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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP2
{

    /// <summary>
    /// How big a header list counts as (RFC 9113, Section 6.5.2).
    ///
    /// <c>SETTINGS_MAX_HEADER_LIST_SIZE</c> is stated against the <b>uncompressed</b>
    /// list, not the HPACK block that carries it — which is the only sensible
    /// definition, since the compressed size depends on the dynamic-table state of
    /// whichever connection it happens to travel on, and the same headers would then
    /// be over the limit on one connection and under it on another.
    ///
    /// The accounting is one line, and precisely because it is one line it was on its
    /// way to being written twice: the server had it inline, and the client needed
    /// the same sum for request headers and for trailers. Two copies of a formula are
    /// two chances to disagree about the 32.
    /// </summary>
    public static class HTTP2HeaderList
    {

        #region UncompressedSize (Headers)

        /// <summary>
        /// The size a peer's <c>MAX_HEADER_LIST_SIZE</c> is measured against: for each
        /// field, its name and value lengths plus 32 bytes of assumed per-field
        /// overhead — the same accounting HPACK uses for its own dynamic table
        /// (RFC 7541, Section 4.1), so that a limit expressed in one is meaningful in
        /// the other.
        /// </summary>
        public static Int64 UncompressedSize(IEnumerable<(String Name, String Value)> Headers)
        {

            Int64 size = 0;

            foreach (var (name, value) in Headers)
                size += name.Length + value.Length + 32;

            return size;

        }

        #endregion

    }

}
