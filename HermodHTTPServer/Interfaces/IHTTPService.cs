/*
 * Copyright (c) 2010-2014, Achim 'ahzf' Friedland <achim@graphdefined.org>
 * This file is part of Hermod <http://www.github.com/Vanaheimr/Hermod>
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

using System.Collections.Generic;
using System;
using System.Reflection;

#endregion

namespace eu.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// The minimal HTTP service interface.
    /// </summary>
    public interface IHTTPService
    {

        /// <summary>
        /// The HTTP connection.
        /// </summary>
        IHTTPConnection                 IHTTPConnection     { get; }

        /// <summary>
        /// A list of supported HTTP content types.
        /// </summary>
        IEnumerable<HTTPContentType>    HTTPContentTypes    { get; }

        /// <summary>
        /// All embedded ressources.
        /// </summary>
        IDictionary<String, Assembly>   AllResources        { get; set; }

    }

}
