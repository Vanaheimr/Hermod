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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Mapps a HTTP request onto embedded resources.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
    public class HTTPResourcesMappingAttribute : Attribute
    {

        #region Properties

        /// <summary>
        /// The URI template.
        /// </summary>  
        public String   UriTemplate             { get; private set; }

        /// <summary>
        /// The embedded resources path.
        /// </summary>
        public String   ResourcesPath           { get; private set; }

        /// <summary>
        /// The embedded resources path.
        /// </summary>
        public String   DefaultFile             { get; private set; }

        /// <summary>
        /// The embedded resources path.
        /// </summary>
        public Boolean  AllowDirectoryListing   { get; private set; }

        #endregion

        #region Constructor(s)

        #region HTTPResourcesMappingAttribute(UriTemplate, ResourcesPath, DefaultFile = "index.hmtl", AllowDirectoryListing = true)

        /// <summary>
        /// Generates a new HTTP mapping.
        /// </summary>
        /// <param name="UriTemplate">The URI template.</param>
        /// <param name="ResourcesPath">The embedded resources path.</param>
        /// <param name="DefaultFile">The default file to load if a path was requested.</param>
        /// <param name="AllowDirectoryListing">Allow a directory listing if a path was requested.</param>
        public HTTPResourcesMappingAttribute(String   UriTemplate,
                                             String   ResourcesPath,
                                             String   DefaultFile            = "index.html",
                                             Boolean  AllowDirectoryListing  = true)
        {

            this.ResourcesPath          = ResourcesPath;
            this.UriTemplate            = UriTemplate;
            this.DefaultFile            = DefaultFile;
            this.AllowDirectoryListing  = AllowDirectoryListing;

        }

        #endregion

        #endregion

    }

}
