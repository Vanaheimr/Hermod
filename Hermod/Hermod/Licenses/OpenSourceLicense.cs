﻿/*
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
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// Extension methods for Open Source licenses.
    /// </summary>
    public static class OpenSourceLicenseExtensions
    {

        #region ToJSON(this OpenSourceLicense)

        /// <summary>
        /// Return a JSON representation for the given enumeration of Open Source licenses.
        /// </summary>
        /// <param name="OpenSourceLicense">An enumeration of Open Source licenses.</param>
        /// <param name="Skip">The optional number of Open Source licenses to skip.</param>
        /// <param name="Take">The optional number of Open Source licenses to return.</param>
        /// <param name="Embedded">Whether this data structure is embedded into another data structure.</param>
        /// <param name="CustomSourceLicenseSerializer">A delegate to serialize custom data license JSON elements.</param>
        public static JArray ToJSON(this IEnumerable<OpenSourceLicense>                  OpenSourceLicense,
                                    UInt64?                                              Skip                            = null,
                                    UInt64?                                              Take                            = null,
                                    Boolean                                              Embedded                        = false,
                                    CustomJObjectSerializerDelegate<OpenSourceLicense>?  CustomSourceLicenseSerializer   = null)

            => OpenSourceLicense is null || !OpenSourceLicense.Any()

                   ? new JArray()

                   : new JArray(OpenSourceLicense.
                                    Where         (openSourceLicense => openSourceLicense is not null).
                                    OrderBy       (openSourceLicense => openSourceLicense.Id).
                                    SkipTakeFilter(Skip, Take).
                                    Select        (openSourceLicense => openSourceLicense.ToJSON(Embedded,
                                                                                                 CustomSourceLicenseSerializer)));

        #endregion

    }


    /// <summary>
    /// An Open Source license.
    /// </summary>
    public class OpenSourceLicense : IEquatable<OpenSourceLicense>,
                                     IComparable<OpenSourceLicense>,
                                     IComparable
    {

        #region Source

        /// <summary>
        /// The JSON-LD context of the object.
        /// </summary>
        public const String JSONLDContext = "https://opendata.social/contexts/wwcp+json/dataLicenses";

        #endregion

        #region Properties

        /// <summary>
        /// The unique identification of the Open Source license.
        /// </summary>
        public OpenSourceLicense_Id  Id             { get; }

        /// <summary>
        /// The description of the Open Source license.
        /// </summary>
        public I18NString            Description    { get; }

        /// <summary>
        /// Optional URLs for more information on the Open Source license.
        /// </summary>
        public IEnumerable<URL>      URLs           { get; }

        #endregion

        #region Constructor(s)

        #region OpenSourceLicense(Id,              params URLs)

        /// <summary>
        /// Create a new Open Source license.
        /// </summary>
        /// <param name="Id">The unique identification of the Open Source license.</param>
        /// <param name="URLs">Optional URLs for more information on the Open Source license.</param>
        public OpenSourceLicense(OpenSourceLicense_Id  Id,
                                 params URL[]          URLs)

            : this(Id,
                   I18NString.Empty,
                   URLs)

        { }

        #endregion

        #region OpenSourceLicense(Id, Description, params URLs)

        /// <summary>
        /// Create a new Open Source license.
        /// </summary>
        /// <param name="Id">The unique identification of the Open Source license.</param>
        /// <param name="Description">The description of the Open Source license.</param>
        /// <param name="URLs">Optional URLs for more information on the Open Source license.</param>
        public OpenSourceLicense(OpenSourceLicense_Id  Id,
                                 I18NString            Description,
                                 params URL[]          URLs)
        {

            this.Id           = Id;
            this.Description  = Description      ?? I18NString.Empty;
            this.URLs         = URLs?.Distinct() ?? [];

            unchecked
            {

                hashCode = this.Id.         GetHashCode() * 5 ^
                           this.Description.GetHashCode() * 3 ^
                           this.URLs.       CalcHashCode();

            }

        }

        #endregion

        #endregion


        #region (static) Parse   (JSON, CustomOpenSourceLicenseParser = null)

        /// <summary>
        /// Parse the given JSON representation of an Open Source license.
        /// </summary>
        /// <param name="JSON">The JSON to parse.</param>
        /// <param name="CustomOpenSourceLicenseParser">A delegate to parse custom Open Source license JSON objects.</param>
        public static OpenSourceLicense Parse(JObject                                          JSON,
                                              CustomJObjectParserDelegate<OpenSourceLicense>?  CustomOpenSourceLicenseParser   = null)
        {

            if (TryParse(JSON,
                         out var openSourceLicense,
                         out var errorResponse,
                         CustomOpenSourceLicenseParser))
            {
                return openSourceLicense;
            }

            throw new ArgumentException("The given JSON representation of an Open Source license is invalid: " + errorResponse,
                                        nameof(JSON));

        }

        #endregion

        #region (static) TryParse(JSON, out OpenSourceLicense, out ErrorResponse, CustomOpenSourceLicenseParser = null)

        // Note: The following is needed to satisfy pattern matching delegates! Do not refactor it!

        /// <summary>
        /// Try to parse the given JSON representation of an Open Source license.
        /// </summary>
        /// <param name="JSON">The JSON to parse.</param>
        /// <param name="OpenSourceLicense">The parsed Open Source license.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        public static Boolean TryParse(JObject                                      JSON,
                                       [NotNullWhen(true)]  out OpenSourceLicense?  OpenSourceLicense,
                                       [NotNullWhen(false)] out String?             ErrorResponse)

            => TryParse(JSON,
                        out OpenSourceLicense,
                        out ErrorResponse,
                        null);


        /// <summary>
        /// Try to parse the given JSON representation of an Open Source license.
        /// </summary>
        /// <param name="JSON">The JSON to parse.</param>
        /// <param name="OpenSourceLicense">The parsed Open Source license.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        /// <param name="CustomOpenSourceLicenseParser">A delegate to parse custom Open Source license JSON objects.</param>
        public static Boolean TryParse(JObject                                          JSON,
                                       [NotNullWhen(true)]  out OpenSourceLicense?      OpenSourceLicense,
                                       [NotNullWhen(false)] out String?                 ErrorResponse,
                                       CustomJObjectParserDelegate<OpenSourceLicense>?  CustomOpenSourceLicenseParser   = null)
        {

            try
            {

                OpenSourceLicense = default;

                if (JSON?.HasValues != true)
                {
                    ErrorResponse = "The given JSON object must not be null or empty!";
                    return false;
                }

                #region Parse Id             [mandatory]

                if (!JSON.ParseMandatory("id",
                                         "Open Source license identification",
                                         OpenSourceLicense_Id.TryParse,
                                         out OpenSourceLicense_Id Id,
                                         out ErrorResponse))
                {
                    return false;
                }

                #endregion

                #region Parse Description    [optional]

                if (JSON.ParseOptional("description",
                                       "Open Source license description",
                                       I18NString.TryParse,
                                       out I18NString? Description,
                                       out ErrorResponse))
                {
                    if (ErrorResponse is not null)
                        return false;
                }


                #endregion

                #region Parse URLs           [optional]

                if (JSON.ParseOptionalHashSet("URLs",
                                              "Open Source license URLs",
                                              URL.TryParse,
                                              out HashSet<URL> URLs,
                                              out ErrorResponse))
                {
                    if (ErrorResponse is not null)
                        return false;
                }

                #endregion


                OpenSourceLicense = new OpenSourceLicense(
                                        Id,
                                        Description ?? I18NString.Empty,
                                        [.. URLs]
                                    );

                if (CustomOpenSourceLicenseParser is not null)
                    OpenSourceLicense = CustomOpenSourceLicenseParser(JSON,
                                                                      OpenSourceLicense);

                return true;

            }
            catch (Exception e)
            {
                OpenSourceLicense  = default;
                ErrorResponse      = "The given JSON representation of an Open Source license is invalid: " + e.Message;
                return false;
            }

        }

        #endregion

        #region ToJSON(this Embedded = false, CustomSourceLicenseSerializer = null)

        /// <summary>
        /// Return a JSON representation of the given data license.
        /// </summary>
        /// <param name="Embedded">Whether this data structure is embedded into another data structure.</param>
        /// <param name="CustomSourceLicenseSerializer">A delegate to serialize custom data license JSON elements.</param>
        public JObject ToJSON(Boolean                                              Embedded                        = false,
                              CustomJObjectSerializerDelegate<OpenSourceLicense>?  CustomSourceLicenseSerializer   = null)
        {

            var json = JSONObject.Create(

                                 new JProperty("@id",           Id.ToString()),

                           !Embedded
                               ? new JProperty("@context",      JSONLDContext)
                               : null,

                           Description.IsNotNullOrEmpty()
                               ? new JProperty("description",   Description.ToJSON())
                               : null,

                           new JProperty("URLs",                new JArray(URLs.Select(url => url.ToString())))

                       );

            return CustomSourceLicenseSerializer is not null
                       ? CustomSourceLicenseSerializer(this, json)
                       : json;

        }

        #endregion

        #region Clone()

        /// <summary>
        /// Clone this object.
        /// </summary>
        public OpenSourceLicense Clone()

            => new (
                   Id.         Clone(),
                   Description.Clone(),
                   URLs.Select(url => url.Clone()).ToArray()
               );

        #endregion


        #region Static Definitions

        /// <summary>
        /// No license.
        /// </summary>
        public static readonly OpenSourceLicense None               = new (OpenSourceLicense_Id.Parse("None"),
                                                                           I18NString.Create("None - do not use!"));

        /// <summary>
        /// The software etc.pp is not Open Source.
        /// </summary>
        public static readonly OpenSourceLicense ClosedSource       = new (OpenSourceLicense_Id.Parse("ClosedSource"),
                                                                           I18NString.Create("Closed Source - do not use!"));


        /// <summary>
        /// Apache License v2.0
        /// </summary>
        public static readonly OpenSourceLicense Apache2_0          = new (OpenSourceLicense_Id.Parse("Apache-2.0"),
                                                                           I18NString.Create("Apache License v2.0"),
                                                                           URL.Parse("https://www.apache.org/licenses/LICENSE-2.0.txt"),
                                                                           URL.Parse("https://www.apache.org/licenses/LICENSE-2.0"),
                                                                           URL.Parse("https://opensource.org/license/apache-2-0/"));

        /// <summary>
        /// The MIT License
        /// </summary>
        public static readonly OpenSourceLicense MIT                = new (OpenSourceLicense_Id.Parse("MIT"),
                                                                           I18NString.Create("The MIT License"),
                                                                           URL.Parse("https://opensource.org/license/mit/"));

        /// <summary>
        /// The 2-Clause BSD License
        /// </summary>
        public static readonly OpenSourceLicense BSD2               = new (OpenSourceLicense_Id.Parse("BSD-2-Clause"),
                                                                           I18NString.Create("The 2-Clause BSD License"),
                                                                           URL.Parse("https://opensource.org/license/bsd-2-clause/"));

        /// <summary>
        /// The 3-Clause BSD License
        /// </summary>
        public static readonly OpenSourceLicense BSD3               = new (OpenSourceLicense_Id.Parse("BSD-3-Clause"),
                                                                           I18NString.Create("The 3-Clause BSD License"),
                                                                           URL.Parse("https://opensource.org/license/bsd-3-clause/"));


        /// <summary>
        /// GNU General Public License version 2
        /// </summary>
        public static readonly OpenSourceLicense GPL2               = new (OpenSourceLicense_Id.Parse("GPL-2.0"),
                                                                           I18NString.Create("GNU General Public License version 2"),
                                                                           URL.Parse("https://www.gnu.org/licenses/gpl-2.0.html"),
                                                                           URL.Parse("https://opensource.org/license/gpl-2-0/"));

        /// <summary>
        /// GNU General Public License version 3
        /// </summary>
        public static readonly OpenSourceLicense GPL3               = new (OpenSourceLicense_Id.Parse("GPL-3.0"),
                                                                           I18NString.Create("GNU General Public License version 3"),
                                                                           URL.Parse("https://www.gnu.org/licenses/gpl-3.0.html"),
                                                                           URL.Parse("https://opensource.org/license/gpl-3-0/"));

        /// <summary>
        /// GNU Affero General Public License version 3
        /// </summary>
        public static readonly OpenSourceLicense AGPL3              = new (OpenSourceLicense_Id.Parse("AGPL-3.0"),
                                                                           I18NString.Create("GNU Affero General Public License version 3"),
                                                                           URL.Parse("https://www.gnu.org/licenses/agpl-3.0.html"),
                                                                           URL.Parse("https://opensource.org/license/agpl-v3/"));

        /// <summary>
        /// GNU Lesser General Public License version 3
        /// </summary>
        public static readonly OpenSourceLicense LGPL3              = new (OpenSourceLicense_Id.Parse("LGPL-3.0"),
                                                                           I18NString.Create("GNU Lesser General Public License version 3"),
                                                                           URL.Parse("https://www.gnu.org/licenses/lgpl-3.0.html"),
                                                                           URL.Parse("https://opensource.org/license/lgpl-v3/"));


        /// <summary>
        /// European Union Public License, version 1.2 (EUPL-1.2)
        /// </summary>
        public static readonly OpenSourceLicense EUPLv1_2           = new (OpenSourceLicense_Id.Parse("EUPL-1.2"),
                                                                           I18NString.Create("European Union Public License, version 1.2 (EUPL-1.2)"),
                                                                           URL.Parse("https://joinup.ec.europa.eu/collection/eupl/news/understanding-eupl-v12"),
                                                                           URL.Parse("https://opensource.org/license/eupl-1-2/"));

        #endregion


        #region Operator overloading

        #region Operator == (OpenSourceLicense1, OpenSourceLicense2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="OpenSourceLicense1">An Open Source license.</param>
        /// <param name="OpenSourceLicense2">Another Open Source license.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (OpenSourceLicense OpenSourceLicense1,
                                           OpenSourceLicense OpenSourceLicense2)

            => OpenSourceLicense1.Equals(OpenSourceLicense2);

        #endregion

        #region Operator != (OpenSourceLicense1, OpenSourceLicense2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="OpenSourceLicense1">An Open Source license.</param>
        /// <param name="OpenSourceLicense2">Another Open Source license.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (OpenSourceLicense OpenSourceLicense1,
                                           OpenSourceLicense OpenSourceLicense2)

            => !(OpenSourceLicense1 == OpenSourceLicense2);

        #endregion

        #region Operator <  (OpenSourceLicense1, OpenSourceLicense2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="OpenSourceLicense1">An Open Source license.</param>
        /// <param name="OpenSourceLicense2">Another Open Source license.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (OpenSourceLicense OpenSourceLicense1,
                                          OpenSourceLicense OpenSourceLicense2)

            => OpenSourceLicense1.CompareTo(OpenSourceLicense2) < 0;

        #endregion

        #region Operator <= (OpenSourceLicense1, OpenSourceLicense2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="OpenSourceLicense1">An Open Source license.</param>
        /// <param name="OpenSourceLicense2">Another Open Source license.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (OpenSourceLicense OpenSourceLicense1,
                                           OpenSourceLicense OpenSourceLicense2)

            => OpenSourceLicense1.CompareTo(OpenSourceLicense2) <= 0;

        #endregion

        #region Operator >  (OpenSourceLicense1, OpenSourceLicense2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="OpenSourceLicense1">An Open Source license.</param>
        /// <param name="OpenSourceLicense2">Another Open Source license.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (OpenSourceLicense OpenSourceLicense1,
                                          OpenSourceLicense OpenSourceLicense2)

            => OpenSourceLicense1.CompareTo(OpenSourceLicense2) > 0;

        #endregion

        #region Operator >= (OpenSourceLicense1, OpenSourceLicense2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="OpenSourceLicense1">An Open Source license.</param>
        /// <param name="OpenSourceLicense2">Another Open Source license.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (OpenSourceLicense OpenSourceLicense1,
                                           OpenSourceLicense OpenSourceLicense2)

            => OpenSourceLicense1.CompareTo(OpenSourceLicense2) >= 0;

        #endregion

        #endregion

        #region IComparable<OpenSourceLicense> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two Open Source licenses.
        /// </summary>
        /// <param name="Object">An Open Source license to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is OpenSourceLicense openSourceLicense
                   ? CompareTo(openSourceLicense)
                   : throw new ArgumentException("The given object is not an Open Source license!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(OpenSourceLicense)

        /// <summary>
        /// Compares two Open Source licenses.
        /// </summary>
        /// <param name="OpenSourceLicense">An Open Source license to compare with.</param>
        public Int32 CompareTo(OpenSourceLicense? OpenSourceLicense)

            => OpenSourceLicense is not null
                   ? Id.CompareTo(OpenSourceLicense.Id)
                   : throw new ArgumentNullException(nameof(OpenSourceLicense),
                                                     "The given Open Source license must not be null!");

        #endregion

        #endregion

        #region IEquatable<OpenSourceLicense> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two Open Source licenses for equality.
        /// </summary>
        /// <param name="Object">An Open Source license to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is OpenSourceLicense openSourceLicense &&
                   Equals(openSourceLicense);

        #endregion

        #region Equals(OpenSourceLicense)

        /// <summary>
        /// Compares two Open Source licenses for equality.
        /// </summary>
        /// <param name="OpenSourceLicense">An Open Source license to compare with.</param>
        public Boolean Equals(OpenSourceLicense? OpenSourceLicense)

            => OpenSourceLicense is not null &&

               Id.Equals(OpenSourceLicense.Id) &&

             ((Description is null     && OpenSourceLicense.Description is null    ) ||
              (Description is not null && OpenSourceLicense.Description is not null && Description.Equals(OpenSourceLicense.Description))) &&

               URLs.Count().Equals(OpenSourceLicense.URLs.Count()) &&
               URLs.All(url => OpenSourceLicense.URLs.Contains(url));

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

            => String.Concat(

                   Id.ToString(),

                   Description.IsNotNullOrEmpty()
                       ? $": {Description}"
                       : String.Empty

               );

        #endregion

    }

}
