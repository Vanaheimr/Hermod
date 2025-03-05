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

#region Usings

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// A configuration for time-based one-time passwords (TOTPs).
    /// </summary>
    /// <param name="SharedSecret">The shared secret.</param>
    /// <param name="ValidityTime">The optional validity time of the TOTP.</param>
    /// <param name="TOTPLength">The optional length of the TOTP.</param>
    /// <param name="Alphabet">The optional alphabet of the TOTP.</param>
    public class TOTPConfig(String     SharedSecret,
                            TimeSpan?  ValidityTime,
                            UInt32?    TOTPLength,
                            String?    Alphabet)
    {

        #region Properties

        public String     SharedSecret    { get; set; } = SharedSecret;
        public TimeSpan?  ValidityTime    { get; set; } = ValidityTime;
        public UInt32?    TOTPLength      { get; set; } = TOTPLength;
        public String?    Alphabet        { get; set; } = Alphabet;

        #endregion


        #region ToJSON(CustomTOTPConfigSerializerDelegate = null)

        public JObject ToJSON(CustomJObjectSerializerDelegate<TOTPConfig>? CustomTOTPConfigSerializerDelegate = null)
        {

            var json = JSONObject.Create(

                                 new JProperty("sharedSecret",   SharedSecret),

                           ValidityTime.HasValue
                               ? new JProperty("validityTime",   ValidityTime.Value.TotalSeconds)
                               : null,

                                 new JProperty("totpLength",     TOTPLength),
                                 new JProperty("alphabet",       Alphabet)

                       );

            return CustomTOTPConfigSerializerDelegate is not null
                       ? CustomTOTPConfigSerializerDelegate(this, json)
                       : json;

        }

        #endregion


    }

}
