/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

namespace org.GraphDefined.Vanaheimr.Hermod.NTP
{

    /// <summary>
    /// The NTS-KE Response.
    /// </summary>
    /// <param name="NTSKERecords">The enumeration of NTS-KE records.</param>
    /// <param name="C2SKey">The TLS client-to-server Key.</param>
    public class NTSKE_Response(IEnumerable<NTSKE_Record>  NTSKERecords,
                                Byte[]                     C2SKey)
    {

        #region Properties

        /// <summary>
        /// The enumeration of NTS-KE records.
        /// </summary>
        public IEnumerable<NTSKE_Record>  NTSKERecords    { get; } = NTSKERecords;

        /// <summary>
        /// The TLS client-to-server Key.
        /// </summary>
        public Byte[]                     C2SKey          { get; } = C2SKey;

        /// <summary>
        /// The NTS-KE cookies.
        /// </summary>
        public IEnumerable<Byte[]> Cookies

            => NTSKERecords.
                   Where (ntsKERecord => ntsKERecord.Type == 5).
                   Select(ntsKERecord => ntsKERecord.Value);

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => String.Concat(

                   $"{NTSKERecords.Count()} NTS-KE records",

                   C2SKey.Length > 0
                       ? $", C2S-Key: {BitConverter.ToString(C2SKey)}"
                       : ""

               );

        #endregion

    }

}
