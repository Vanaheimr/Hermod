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

using System;
using System.Collections.Generic;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public class HTTPErrorAttribute : Attribute
    {

        #region Properties

        public HTTPMethod     HTTPMethod     { get; private set; }

        public String         UriTemplate    { get; private set; }

        public HTTPStatusCode HTTPStatusCode { get; private set; }

        #endregion

        #region Constructor(s)

        #region HTTPErrorAttribute(UriTemplate)

        public HTTPErrorAttribute(String UriTemplate)
        {
            this.HTTPMethod      = HTTPMethod.GET;
            this.UriTemplate     = UriTemplate;
            this.HTTPStatusCode  = null;
        }

        #endregion

        #region HTTPErrorAttribute(UriTemplate, myHTTPStatusCode)

        public HTTPErrorAttribute(String UriTemplate, HTTPStatusCode myHTTPStatusCode)
        {
            this.HTTPMethod      = HTTPMethod.GET;
            this.UriTemplate     = UriTemplate;
            this.HTTPStatusCode  = myHTTPStatusCode;
        }

        #endregion

        #region HTTPErrorAttribute(HTTPMethod, UriTemplate, myHTTPStatusCode)

        public HTTPErrorAttribute(HTTPMethod HTTPMethod, String UriTemplate, HTTPStatusCode myHTTPStatusCode)
        {
            this.HTTPMethod     = HTTPMethod;
            this.UriTemplate    = UriTemplate;
            this.HTTPStatusCode = myHTTPStatusCode;
        }

        #endregion

        #region HTTPErrorAttribute(HTTPMethod, UriTemplate, myHTTPStatusCode)

        public HTTPErrorAttribute(HTTPMethods HTTPMethod, String UriTemplate, HTTPStatusCode myHTTPStatusCode)
        {
            
            this.HTTPMethod     = org.GraphDefined.Vanaheimr.Hermod.HTTP.HTTPMethod.ParseEnum(HTTPMethod);

            if (this.HTTPMethod == null)
                throw new ArgumentNullException("Invalid HTTPMethod!");

            this.UriTemplate    = UriTemplate;
            this.HTTPStatusCode = myHTTPStatusCode;

        }

        #endregion

        #endregion

    }

}
