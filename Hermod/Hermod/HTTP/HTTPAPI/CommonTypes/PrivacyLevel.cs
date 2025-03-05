/*
 * Copyright (c) 2014-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of UsersAPI <https://www.github.com/Vanaheimr/UsersAPI>
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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// JSON content representation.
    /// </summary>
    public static class PrivacyLevelExtension
    {

        #region ToJSON(this Privacylevel)

        public static JProperty ToJSON(this PrivacyLevel Privacylevel,
                                       String PropertyKey = "privacyLevel")
        {

            if (PropertyKey != null)
                PropertyKey = PropertyKey.Trim();

            if (PropertyKey.IsNullOrEmpty())
                PropertyKey = "privacyLevel";

            switch (Privacylevel)
            {

                case PrivacyLevel.Private:
                    return new JProperty(PropertyKey, "private");

                case PrivacyLevel.Internal:
                    return new JProperty(PropertyKey, "internal");

                case PrivacyLevel.Public:
                    return new JProperty(PropertyKey, "public");

                case PrivacyLevel.Friends:
                    return new JProperty(PropertyKey, "friends");

                case PrivacyLevel.City:
                    return new JProperty(PropertyKey, "city");

                case PrivacyLevel.Country:
                    return new JProperty(PropertyKey, "country");

                case PrivacyLevel.GDPR:
                    return new JProperty(PropertyKey, "GDPR");

                default:
                    return new JProperty(PropertyKey, "world");

            }

        }

        #endregion

        #region ParsePrivacyLevel(this Text)

        public static PrivacyLevel ParsePrivacyLevel(this String Text)
        {

            switch (Text)
            {

                case "world":
                    return PrivacyLevel.World;

                case "public":
                    return PrivacyLevel.Public;

                case "internal":
                    return PrivacyLevel.Internal;

                case "friends":
                    return PrivacyLevel.Friends;

                case "city":
                    return PrivacyLevel.City;

                case "country":
                    return PrivacyLevel.Country;

                case "GDPR":
                    return PrivacyLevel.GDPR;

                default:
                    return PrivacyLevel.Private;

            }

        }

        public static PrivacyLevel ParseMandatory_PrivacyLevel(this JObject JSON)
        {

            var Value = JSON["privacyLevel"]?.Value<String>();

            if (Value.IsNullOrEmpty())
                throw new Exception("Missing JSON property 'privacyLevel'!");

            switch (Value.Trim().ToLower())
            {

                case "world":
                    return PrivacyLevel.World;

                case "public":
                    return PrivacyLevel.Public;

                case "internal":
                    return PrivacyLevel.Internal;

                case "friends":
                    return PrivacyLevel.Friends;

                case "city":
                    return PrivacyLevel.City;

                case "country":
                    return PrivacyLevel.Country;

                case "GDPR":
                    return PrivacyLevel.GDPR;

                default:
                    return PrivacyLevel.Private;

            }

        }

        public static Boolean TryParseMandatory_PrivacyLevel(this JObject JSON,
                                                             out PrivacyLevel PrivacyLevel,
                                                             out String ErrorResponse)
        {

            var Value = JSON["privacyLevel"]?.Value<String>();

            if (Value.IsNullOrEmpty())
            {
                PrivacyLevel = PrivacyLevel.Private;
                ErrorResponse = "Missing JSON property 'privacyLevel'!";
                return false;
            }

            switch (Value.Trim().ToLower())
            {

                case "world":
                    PrivacyLevel = PrivacyLevel.World;
                    ErrorResponse = String.Empty;
                    return true;

                case "public":
                    PrivacyLevel = PrivacyLevel.Public;
                    ErrorResponse = String.Empty;
                    return true;

                case "gdpr":
                    PrivacyLevel = PrivacyLevel.GDPR;
                    ErrorResponse = String.Empty;
                    return true;

                case "country":
                    PrivacyLevel = PrivacyLevel.Country;
                    ErrorResponse = String.Empty;
                    return true;

                case "city":
                    PrivacyLevel = PrivacyLevel.City;
                    ErrorResponse = String.Empty;
                    return true;

                case "friends":
                    PrivacyLevel = PrivacyLevel.Friends;
                    ErrorResponse = String.Empty;
                    return true;

                case "internal":
                    PrivacyLevel = PrivacyLevel.Internal;
                    ErrorResponse = String.Empty;
                    return true;

                case "private":
                    PrivacyLevel = PrivacyLevel.Private;
                    ErrorResponse = String.Empty;
                    return true;

                default:
                    PrivacyLevel = PrivacyLevel.Private;
                    ErrorResponse = "Invalid value '" + Value + "' for JSON property 'privacyLevel'!";
                    return false;

            }

        }

        public static Boolean TryParsePrivacyLevel(String Value,
                                                   out PrivacyLevel PrivacyLevel)
        {

            if (Value.IsNullOrEmpty())
            {
                PrivacyLevel = PrivacyLevel.Private;
                return false;
            }

            switch (Value.Trim().ToLower())
            {

                case "world":
                    PrivacyLevel = PrivacyLevel.World;
                    return true;

                case "public":
                    PrivacyLevel = PrivacyLevel.Public;
                    return true;

                case "gdpr":
                    PrivacyLevel = PrivacyLevel.GDPR;
                    return true;

                case "country":
                    PrivacyLevel = PrivacyLevel.Country;
                    return true;

                case "city":
                    PrivacyLevel = PrivacyLevel.City;
                    return true;

                case "friends":
                    PrivacyLevel = PrivacyLevel.Friends;
                    return true;

                case "internal":
                    PrivacyLevel = PrivacyLevel.Internal;
                    return true;

                case "private":
                    PrivacyLevel = PrivacyLevel.Private;
                    return true;

                default:
                    PrivacyLevel = PrivacyLevel.Private;
                    return false;

            }

        }

        public static PrivacyLevel? ParseOptional_PrivacyLevel(this JObject JSON)
        {

            var Value = JSON["privacyLevel"]?.Value<String>();

            if (Value.IsNullOrEmpty())
                return new PrivacyLevel?();

            switch (Value)
            {

                case "world":
                    return PrivacyLevel.World;

                case "public":
                    return PrivacyLevel.Public;

                case "gdpr":
                    return PrivacyLevel.GDPR;

                case "country":
                    return PrivacyLevel.Country;

                case "city":
                    return PrivacyLevel.City;

                case "friends":
                    return PrivacyLevel.Friends;

                case "internal":
                    return PrivacyLevel.Internal;

                default:
                    return PrivacyLevel.Private;

            }

        }

        #endregion

    }

    public enum PrivacyLevel
    {
        Private,
        Public,
        Internal,
        Friends,
        Plattform,
        City,
        Country,
        GDPR,
        World
    }

}
