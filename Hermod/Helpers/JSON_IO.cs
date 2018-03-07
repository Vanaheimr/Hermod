/*
 * Copyright (c) 2010-2018, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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

#region Usings

using System;
using System.Linq;
using System.Collections.Generic;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// JSON I/O.
    /// </summary>
    public static class JSON_IO
    {

        #region ToJSON(this Id, JPropertyKey)

        /// <summary>
        /// Create a JSON representation of the given identificator.
        /// </summary>
        /// <param name="Id">An identificator.</param>
        /// <param name="JPropertyKey">The name of the JSON property key to use.</param>
        public static JProperty ToJSON(this IId Id, String JPropertyKey)

            => Id != null
                   ? new JProperty(JPropertyKey, Id.ToString())
                   : null;

        #endregion

        #region ToJSON(this Text, JPropertyKey)

        public static JProperty ToJSON(this String Text, String JPropertyKey)

            => Text.IsNotNullOrEmpty()
                   ? new JProperty(JPropertyKey, Text)
                   : null;

        #endregion


        #region ToJSON(this Address)

        public static JObject ToJSON(this Address _Address)

            => _Address != null
                   ? JSONObject.Create(
                         _Address.FloorLevel.   ToJSON("floorLevel"),
                         _Address.HouseNumber.  ToJSON("houseNumber"),
                         _Address.Street.       ToJSON("street"),
                         _Address.PostalCode.   ToJSON("postalCode"),
                         _Address.PostalCodeSub.ToJSON("postalCodeSub"),
                         _Address.City.         ToJSON("city"),
                         _Address.Country != null
                              ? _Address.Country.Alpha3Code.ToJSON("country")
                              : null,
                         _Address.Comment.      ToJSON("comment")
                     )
                   : null;

        #endregion

        #region ToJSON(this Address, JPropertyKey)

        public static JProperty ToJSON(this Address Address, String JPropertyKey)

            => Address != null
                   ? new JProperty(JPropertyKey,
                                   Address.ToJSON())
                   : null;

        #endregion

        #region ToJSON(this Addresses, JPropertyKey)

        public static JArray ToJSON(this IEnumerable<Address> Addresses)

            => Addresses != null && Addresses.Any()
                   ? new JArray(Addresses.SafeSelect(v => v.ToJSON()))
                   : null;

        #endregion

        #region ToJSON(this Addresses, JPropertyKey)

        public static JProperty ToJSON(this IEnumerable<Address> Addresses, String JPropertyKey)

            => Addresses != null
                   ? new JProperty(JPropertyKey,
                                   Addresses.ToJSON())
                   : null;

        #endregion

        public static Address ParseAddress(this JObject JSONObject, String PropertyKey)
        {

            try
            {

                if (JSONObject[PropertyKey] is JObject JSON)
                    return Address.Create(Country.Parse(JSON["country"    ]?.Value<String>()),
                                                        JSON["postalCode" ]?.Value<String>(),
                                                       (JSON["city"       ] as JObject)?.ParseI18NString(),
                                                        JSON["street"     ]?.Value<String>(),
                                                        JSON["houseNumber"]?.Value<String>(),
                                                        JSON["floorLevel" ]?.Value<String>(),
                                                       (JSON["comment"    ] as JObject)?.ParseI18NString());

            }
            catch (Exception)
            { }

            return null;

        }

        public static Boolean TryParseAddress(this JObject JSON, out Address Address)
        {

            try
            {

                Address = Address.Create(Country.Parse(JSON["country"]?.Value<String>()),
                                         JSON["postalCode" ]?.Value<String>(),
                                         (JSON["city"] as JObject)?.ParseI18NString(),
                                         JSON["street"     ]?.Value<String>(),
                                         JSON["houseNumber"]?.Value<String>(),
                                         JSON["floorLevel" ]?.Value<String>(),
                                        (JSON["comment"    ] as JObject)?.ParseI18NString());

                return true;

            }
            catch (Exception e)
            {
            }

            Address = null;
            return false;

        }

        public static Boolean TryParseAddress(this String Text, out Address Address)
            => TryParseAddress(JObject.Parse(Text), out Address);



        #region ToJSON(this DataLicenseIds)

        public static JArray ToJSON(this IEnumerable<DataLicense_Id> DataLicenseIds)

            => DataLicenseIds != null
                   ? new JArray(DataLicenseIds)
                   : null;

        #endregion

        #region ToJSON(this DataLicenseIds, JPropertyKey)

        public static JProperty ToJSON(this IEnumerable<DataLicense_Id> DataLicenseIds, String JPropertyKey)

            => DataLicenseIds != null
                   ? new JProperty(JPropertyKey, new JArray(DataLicenseIds))
                   : null;

        #endregion

        #region ToJSON(this DataLicense)

        public static JObject ToJSON(this DataLicense DataLicense)

            => DataLicense != null
                   ? JSONObject.Create(
                         new JProperty("@id",          DataLicense.Id.ToString()),
                         new JProperty("@context",     "https://open.charging.cloud/contexts/DataLicense"),
                         new JProperty("description",  DataLicense.Description),
                         new JProperty("uris",         new JArray(DataLicense.URIs))
                     )
                   : null;

        #endregion

        #region ToJSON(this DataLicenses)

        public static JArray ToJSON(this IEnumerable<DataLicense> DataLicenses)

            => DataLicenses != null
                   ? new JArray(DataLicenses.SafeSelect(license => license.ToJSON()))
                   : null;

        #endregion

        #region ToJSON(this DataLicenses, JPropertyKey)

        public static JProperty ToJSON(this IEnumerable<DataLicense> DataLicenses, String JPropertyKey)

            => DataLicenses != null
                   ? new JProperty(JPropertyKey,
                                   new JArray(DataLicenses.SafeSelect(license => license.ToJSON())))
                   : null;

        #endregion



        #region ToJSON(this IIPAddress, JPropertyKey)

        /// <summary>
        /// Create a JSON representation of the given IP address.
        /// </summary>
        /// <param name="IIPAddress">An identificator.</param>
        /// <param name="JPropertyKey">The name of the JSON property key to use.</param>
        public static JProperty ToJSON(this IIPAddress IIPAddress, String JPropertyKey)

            => IIPAddress != null
                   ? new JProperty(JPropertyKey, IIPAddress.ToString())
                   : null;

        #endregion

    }

}
