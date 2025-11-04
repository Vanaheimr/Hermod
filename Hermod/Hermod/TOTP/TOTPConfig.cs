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

using System.Diagnostics.CodeAnalysis;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// A configuration for time-based one-time passwords (TOTPs).
    /// </summary>
    /// 
    public class TOTPConfig
    {

        #region Properties

        /// <summary>
        /// The shared secret of the Time-Based One-Time Password.
        /// </summary>
        public String     SharedSecret    { get; }

        /// <summary>
        /// The optional validity time of the Time-Based One-Time Password.
        /// </summary>
        public TimeSpan?  ValidityTime    { get; }

        /// <summary>
        /// The optional length of the Time-Based One-Time Password.
        /// </summary>
        public UInt32?    Length          { get; }

        /// <summary>
        /// The optional alphabet of the Time-Based One-Time Password.
        /// </summary>
        public String?    Alphabet        { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new TOTP config.
        /// </summary>
        /// <param name="SharedSecret">The shared secret.</param>
        /// <param name="ValidityTime">The optional validity time of the TOTP.</param>
        /// <param name="Length">The optional length of the TOTP.</param>
        /// <param name="Alphabet">The optional alphabet of the TOTP.</param>
        public TOTPConfig(String     SharedSecret,
                          TimeSpan?  ValidityTime   = null,
                          UInt32?    Length         = null,
                          String?    Alphabet       = null)
        {

            this.SharedSecret  = SharedSecret;
            this.ValidityTime  = ValidityTime;
            this.Length        = Length;
            this.Alphabet      = Alphabet;

            unchecked
            {

                this.hashCode = this.SharedSecret. GetHashCode()       * 7 ^
                               (this.ValidityTime?.GetHashCode() ?? 0) * 5 ^
                               (this.Length?.      GetHashCode() ?? 0) * 3 ^
                                this.Alphabet?.    GetHashCode() ?? 0;

            }

        }

        #endregion


        #region (static) Parse   (JSON, CustomTOTPConfigParser = null)

        /// <summary>
        /// Parse the given JSON representation of a TOTP config.
        /// </summary>
        /// <param name="JSON">The JSON to parse.</param>
        /// <param name="CustomTOTPConfigParser">A delegate to parse custom TOTP config JSON objects.</param>
        public static TOTPConfig Parse(JObject                                         JSON,
                                             CustomJObjectParserDelegate<TOTPConfig>?  CustomTOTPConfigParser   = null)
        {
            if (TryParse(JSON,
                         out var totpConfig,
                         out var errorResponse,
                         CustomTOTPConfigParser))
            {
                return totpConfig;
            }

            throw new ArgumentException("The given JSON representation of a TOTP config is invalid: " + errorResponse,
                                        nameof(JSON));

        }

        #endregion

        #region (static) TryParse(JSON, out TOTPConfig, out ErrorResponse, CustomTOTPConfigParser = null)

        // Note: The following is needed to satisfy pattern matching delegates! Do not refactor it!

        /// <summary>
        /// Try to parse the given JSON representation of a TOTP config.
        /// </summary>
        /// <param name="JSON">The JSON to parse.</param>
        /// <param name="TOTPConfig">The parsed TOTP config.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        public static Boolean TryParse(JObject                               JSON,
                                       [NotNullWhen(true)]  out TOTPConfig?  TOTPConfig,
                                       [NotNullWhen(false)] out String?      ErrorResponse)

            => TryParse(JSON,
                        out TOTPConfig,
                        out ErrorResponse,
                        null);


        /// <summary>
        /// Try to parse the given JSON representation of a TOTP config.
        /// </summary>
        /// <param name="JSON">The JSON to parse.</param>
        /// <param name="TOTPConfig">The parsed TOTP config.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        /// <param name="CustomTOTPConfigParser">A delegate to parse custom TOTP config JSON objects.</param>
        public static Boolean TryParse(JObject                                   JSON,
                                       [NotNullWhen(true)]  out TOTPConfig?      TOTPConfig,
                                       [NotNullWhen(false)] out String?          ErrorResponse,
                                       CustomJObjectParserDelegate<TOTPConfig>?  CustomTOTPConfigParser   = null)
        {

            try
            {

                TOTPConfig = default;

                if (JSON?.HasValues != true)
                {
                    ErrorResponse = "The given JSON object must not be null or empty!";
                    return false;
                }

                #region Parse SharedSecret     [mandatory]

                if (!JSON.ParseMandatoryText("sharedSecret",
                                             "shared secret time of the time-based one-time password",
                                             out String? sharedSecret,
                                             out ErrorResponse))
                {
                    return false;
                }

                #endregion

                #region Parse Validity Time    [optional]

                if (JSON.ParseOptional("validityTime",
                                       "validity time of the time-based one-time password",
                                       TimeSpanExtensions.TryParseSeconds,
                                       out TimeSpan? validityTime,
                                       out ErrorResponse))
                {
                    if (ErrorResponse is not null)
                        return false;
                }

                #endregion

                #region Parse Length           [optional]

                if (JSON.ParseOptional("totpLength",
                                       "length of the time-based one-time password",
                                       out Byte? totpLength,
                                       out ErrorResponse))
                {
                    if (ErrorResponse is not null)
                        return false;
                }

                #endregion

                #region Parse Alphabet         [optional]

                if (JSON.ParseOptional("alphabet",
                                       "alphabet of the time-based one-time password",
                                       out String? alphabet,
                                       out ErrorResponse))
                {
                    if (ErrorResponse is not null)
                        return false;
                }

                #endregion


                TOTPConfig = new TOTPConfig(
                                 sharedSecret,
                                 validityTime,
                                 totpLength,
                                 alphabet
                             );


                if (CustomTOTPConfigParser is not null)
                    TOTPConfig = CustomTOTPConfigParser(JSON,
                                                        TOTPConfig);

                return true;

            }
            catch (Exception e)
            {
                TOTPConfig     = default;
                ErrorResponse  = "The given JSON representation of a TOTP config is invalid: " + e.Message;
                return false;
            }

        }

        #endregion

        #region ToJSON(CustomTOTPConfigSerializerDelegate = null)

        public JObject ToJSON(CustomJObjectSerializerDelegate<TOTPConfig>? CustomTOTPConfigSerializerDelegate = null)
        {

            var json = JSONObject.Create(

                                 new JProperty("sharedSecret",   SharedSecret),

                           ValidityTime.HasValue
                               ? new JProperty("validityTime",   ValidityTime.Value.TotalSeconds)
                               : null,

                                 new JProperty("length",         Length),
                                 new JProperty("alphabet",       Alphabet)

                       );

            return CustomTOTPConfigSerializerDelegate is not null
                       ? CustomTOTPConfigSerializerDelegate(this, json)
                       : json;

        }

        #endregion

        #region Clone()

        /// <summary>
        /// Clone this TOTP config.
        /// </summary>
        public TOTPConfig Clone()

            => new (
                   SharedSecret.CloneString(),
                   ValidityTime,
                   Length,
                   Alphabet?.   CloneString()
               );

        #endregion


        #region Operator overloading

        #region Operator == (TOTPConfig1, TOTPConfig2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="TOTPConfig1">A TOTP config.</param>
        /// <param name="TOTPConfig2">Another TOTP config.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (TOTPConfig TOTPConfig1,
                                           TOTPConfig TOTPConfig2)
        {

            if (Object.ReferenceEquals(TOTPConfig1, TOTPConfig2))
                return true;

            if ((TOTPConfig1 is null) || (TOTPConfig2 is null))
                return false;

            return TOTPConfig1.Equals(TOTPConfig2);

        }

        #endregion

        #region Operator != (TOTPConfig1, TOTPConfig2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="TOTPConfig1">A TOTP config.</param>
        /// <param name="TOTPConfig2">Another TOTP config.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (TOTPConfig TOTPConfig1,
                                           TOTPConfig TOTPConfig2)

            => !(TOTPConfig1 == TOTPConfig2);

        #endregion

        #region Operator <  (TOTPConfig1, TOTPConfig2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="TOTPConfig1">A TOTP config.</param>
        /// <param name="TOTPConfig2">Another TOTP config.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (TOTPConfig TOTPConfig1,
                                          TOTPConfig TOTPConfig2)

            => TOTPConfig1 is null
                   ? throw new ArgumentNullException(nameof(TOTPConfig1), "The given TOTP config must not be null!")
                   : TOTPConfig1.CompareTo(TOTPConfig2) < 0;

        #endregion

        #region Operator <= (TOTPConfig1, TOTPConfig2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="TOTPConfig1">A TOTP config.</param>
        /// <param name="TOTPConfig2">Another TOTP config.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (TOTPConfig TOTPConfig1,
                                           TOTPConfig TOTPConfig2)

            => !(TOTPConfig1 > TOTPConfig2);

        #endregion

        #region Operator >  (TOTPConfig1, TOTPConfig2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="TOTPConfig1">A TOTP config.</param>
        /// <param name="TOTPConfig2">Another TOTP config.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (TOTPConfig TOTPConfig1,
                                          TOTPConfig TOTPConfig2)

            => TOTPConfig1 is null
                   ? throw new ArgumentNullException(nameof(TOTPConfig1), "The given TOTP config must not be null!")
                   : TOTPConfig1.CompareTo(TOTPConfig2) > 0;

        #endregion

        #region Operator >= (TOTPConfig1, TOTPConfig2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="TOTPConfig1">A TOTP config.</param>
        /// <param name="TOTPConfig2">Another TOTP config.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (TOTPConfig TOTPConfig1,
                                           TOTPConfig TOTPConfig2)

            => !(TOTPConfig1 < TOTPConfig2);

        #endregion

        #endregion

        #region IComparable<TOTPConfig> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two TOTP config.
        /// </summary>
        /// <param name="Object">A TOTP config to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is TOTPConfig totpConfig
                   ? CompareTo(totpConfig)
                   : throw new ArgumentException("The given object is not a TOTP config!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(TOTPConfig)

        /// <summary>
        /// Compares two TOTP config.
        /// </summary>
        /// <param name="TOTPConfig">A TOTP config to compare with.</param>
        public Int32 CompareTo(TOTPConfig? TOTPConfig)
        {

            if (TOTPConfig is null)
                throw new ArgumentNullException(nameof(TOTPConfig), "The given TOTP config must not be null!");

            var c = SharedSecret.      CompareTo(TOTPConfig.SharedSecret);

            if (c == 0 && ValidityTime.HasValue && TOTPConfig.ValidityTime.HasValue)
                c = ValidityTime.Value.CompareTo(TOTPConfig.ValidityTime.Value);

            if (c == 0 && Length.      HasValue && TOTPConfig.Length.      HasValue)
                c = Length.      Value.CompareTo(TOTPConfig.Length.      Value);

            if (c == 0 && Alphabet is not null && TOTPConfig.Alphabet is not null)
                c = Alphabet.          CompareTo(TOTPConfig.Alphabet);

            return c;

        }

        #endregion

        #endregion

        #region IEquatable<TOTPConfig> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two TOTP config for equality.
        /// </summary>
        /// <param name="Object">A TOTP config to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is TOTPConfig totpConfig &&
                   Equals(totpConfig);

        #endregion

        #region Equals(TOTPConfig)

        /// <summary>
        /// Compares two TOTP config for equality.
        /// </summary>
        /// <param name="TOTPConfig">A TOTP config to compare with.</param>
        public Boolean Equals(TOTPConfig? TOTPConfig)

            => TOTPConfig is not null &&

               SharedSecret.      Equals(TOTPConfig.SharedSecret) &&

            ((!ValidityTime.HasValue && !TOTPConfig.ValidityTime.HasValue) ||
              (ValidityTime.HasValue &&  TOTPConfig.ValidityTime.HasValue && ValidityTime.Value.Equals(TOTPConfig.ValidityTime.Value))) &&

            ((!Length.      HasValue && !TOTPConfig.Length.      HasValue) ||
              (Length.      HasValue &&  TOTPConfig.Length.      HasValue && Length.      Value.Equals(TOTPConfig.Length.      Value))) &&

             ((Alphabet is null      &&  TOTPConfig.Alphabet is null)      ||
              (Alphabet is not null  &&  TOTPConfig.Alphabet is not null  && Alphabet.          Equals(TOTPConfig.Alphabet          )));

        #endregion

        #endregion

        #region (override) GetHashCode()

        private readonly Int32 hashCode;

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        public override Int32 GetHashCode()
            => hashCode;

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"{SharedSecret}{(ValidityTime.HasValue ? $"{ValidityTime.Value.TotalSeconds} sec." : "")} {(Length.HasValue ? $"{Length.Value} characters" : "")} ({(Alphabet is not null ? Alphabet : "-")})";

        #endregion

    }

}
