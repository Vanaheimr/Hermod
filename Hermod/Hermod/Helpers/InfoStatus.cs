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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// The info status of embedded data structures.
    /// </summary>
    public enum InfoStatus
    {

        /// <summary>
        /// Expand the embedded data structures.
        /// </summary>
        Expanded,

        /// <summary>
        /// Show a short summary of the embedded data structures.
        /// </summary>
        Short,

        /// <summary>
        /// Show only the identifications of the embedded data structures.
        /// </summary>
        ShowIdOnly,

        /// <summary>
        /// Hide everything.
        /// </summary>
        Hidden

    }


    /// <summary>
    /// Extension methods for the info status of embedded data structures.
    /// </summary>
    public static class InfoStatusExtensions
    {

        #region Parse   (Text)

        /// <summary>
        /// Parse the given string as an info status.
        /// </summary>
        /// <param name="Text">A text representation of an info status.</param>
        public static InfoStatus Parse(String Text)
        {

            if (TryParse(Text, out InfoStatus infoStatus))
                return infoStatus;

            throw new ArgumentException($"Invalid text representation of an info status: '" + Text + "'!",
                                        nameof(Text));

        }

        #endregion

        #region TryParse(Text)

        /// <summary>
        /// Parse the given string as an info status.
        /// </summary>
        /// <param name="Text">A text representation of an info status.</param>
        public static InfoStatus? TryParse(String Text)
        {

            if (TryParse(Text, out InfoStatus infoStatus))
                return infoStatus;

            return null;

        }

        #endregion

        #region TryParse(Text, out InfoStatus)

        /// <summary>
        /// Try to parse the given string as an info status.
        /// </summary>
        /// <param name="Text">A text representation of an info status.</param>
        /// <param name="ProcessId">The parsed info status.</param>
        public static Boolean TryParse(String Text, out InfoStatus InfoStatus)
        {
            switch (Text?.ToLower())
            {

                case "expanded":
                    InfoStatus = InfoStatus.Expanded;
                    return true;

                case "short":
                    InfoStatus = InfoStatus.Short;
                    return true;

                case "hidden":
                    InfoStatus = InfoStatus.Hidden;
                    return true;

                default:
                    InfoStatus = InfoStatus.ShowIdOnly;
                    return true;

            }
        }

        #endregion


        #region Switch(this Status, WhenShowIdOnly, WhenExpanded)  [JProperty]

        public static JProperty? Switch(this InfoStatus  Status,
                                        Func<JProperty>  WhenShowIdOnly,
                                        Func<JProperty>  WhenExpanded)

            => Status switch {
                   InfoStatus.ShowIdOnly  => WhenShowIdOnly(),
                   InfoStatus.Expanded    => WhenExpanded(),
                   _                      => null
               };

        public static JProperty? Switch(this InfoStatus?  Status,
                                        Func<JProperty>   WhenShowIdOnly,
                                        Func<JProperty>   WhenExpanded)

            => Status.HasValue
                   ? Status.Value.Switch(WhenShowIdOnly,
                                         WhenExpanded)
                   : null;

        #endregion

        #region Switch(this Status, WhenShowIdOnly, WhenExpanded)  [JContainer]

        public static JContainer? Switch(this InfoStatus   Status,
                                         Func<JContainer>  WhenShowIdOnly,
                                         Func<JContainer>  WhenExpanded)

            => Status switch {
                   InfoStatus.ShowIdOnly  => WhenShowIdOnly(),
                   InfoStatus.Expanded    => WhenExpanded(),
                   _                      => null
               };

        public static JContainer? Switch(this InfoStatus?  Status,
                                         Func<JContainer>  WhenShowIdOnly,
                                         Func<JContainer>  WhenExpanded)

            => Status.HasValue
                   ? Status.Value.Switch(WhenShowIdOnly,
                                         WhenExpanded)
                   : null;

        #endregion

        #region Switch(this Status, WhenShowIdOnly, WhenExpanded)  [JToken]

        public static JToken? Switch(this InfoStatus  Status,
                                     Func<JToken>     WhenShowIdOnly,
                                     Func<JToken>     WhenExpanded)

            => Status switch {
                InfoStatus.ShowIdOnly  => WhenShowIdOnly(),
                InfoStatus.Expanded    => WhenExpanded(),
                _                      => null
            };

        public static JToken? Switch(this InfoStatus?  Status,
                                     Func<JToken>      WhenShowIdOnly,
                                     Func<JToken>      WhenExpanded)

            => Status.HasValue
                   ? Status.Value.Switch(WhenShowIdOnly,
                                         WhenExpanded)
                   : null;

        #endregion


        public static JToken? Switch<T>(this InfoStatus  Status,
                                        T                Element,
                                        Func<T, JToken>  WhenShowIdOnly,
                                        Func<T, JToken>  WhenExpanded)

            => Status switch {
                   InfoStatus.ShowIdOnly  => WhenShowIdOnly(Element),
                   InfoStatus.Expanded    => WhenExpanded(Element),
                   _                      => null
               };


    }

}
