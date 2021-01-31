/*
 * Copyright (c) 2010-2021, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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

        #region ToJSON(this IIPAddress, JPropertyKey)

        /// <summary>
        /// Return a JSON representation of the given IP address.
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
