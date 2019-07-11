/*
 * Copyright (c) 2010-2019, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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

using Newtonsoft.Json.Linq;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    public enum InfoStatus
    {

        Expand,
        ShowIdOnly,
        Hidden

    }


    public static class InfoStatusExtentions
    {

        public static JProperty Switch(this InfoStatus  Status,
                                       Func<JProperty>  WhenShowIdOnly,
                                       Func<JProperty>  WhenExpand)
        {

            switch (Status)
            {

                case InfoStatus.ShowIdOnly:
                    return WhenShowIdOnly();

                case InfoStatus.Expand:
                    return WhenExpand();

                default:
                    return null;

            }

        }


        public static JContainer Switch(this InfoStatus   Status,
                                        Func<JContainer>  WhenShowIdOnly,
                                        Func<JContainer>  WhenExpand)
        {

            switch (Status)
            {

                case InfoStatus.ShowIdOnly:
                    return WhenShowIdOnly();

                case InfoStatus.Expand:
                    return WhenExpand();

                default:
                    return null;

            }

        }

        public static JToken Switch(this InfoStatus  Status,
                                    Func<JToken>     WhenShowIdOnly,
                                    Func<JToken>     WhenExpand)
        {

            switch (Status)
            {

                case InfoStatus.ShowIdOnly:
                    return WhenShowIdOnly();

                case InfoStatus.Expand:
                    return WhenExpand();

                default:
                    return null;

            }

        }


        // -------------------------------------------------------------------------


        public static JToken Switch<T>(this InfoStatus  Status,
                                       T                Element,
                                       Func<T, JToken>  WhenShowIdOnly,
                                       Func<T, JToken>  WhenExpand)
        {

            switch (Status)
            {

                case InfoStatus.ShowIdOnly:
                    return WhenShowIdOnly(Element);

                case InfoStatus.Expand:
                    return WhenExpand(Element);

                default:
                    return null;

            }

        }

    }

}
