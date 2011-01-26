/*
 * Copyright (c) 2010-2011, Achim 'ahzf' Friedland <code@ahzf.de>
 * This file is part of Hermod
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

using de.ahzf.Hermod.HTTP.Common;

#endregion

namespace de.ahzf.Hermod.HTTP
{

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public class HTTPErrorAttribute : Attribute
    {

        #region Properties

        #region HTTPMethod

        private HTTPMethod _HTTPMethod;

        public HTTPMethod HTTPMethod
        {
            get
            {
                return _HTTPMethod;
            }
        }

        #endregion

        #region UriTemplate

        private String _UriTemplate;

        public String UriTemplate
        {
            get
            {
                return _UriTemplate;
            }
        }

        #endregion

        #region HTTPStatusCodes

        private HTTPStatusCode _HTTPStatusCode;

        public HTTPStatusCode HTTPStatusCode
        {
            get
            {
                return _HTTPStatusCode;
            }
        }

        #endregion

        #endregion

        #region Constructor(s)

        #region HTTPErrorAttribute(UriTemplate)

        public HTTPErrorAttribute(String UriTemplate)
        {
            _HTTPMethod      = HTTPMethod.GET;
            _UriTemplate     = UriTemplate;
            _HTTPStatusCode  = null;
        }

        #endregion

        #region HTTPErrorAttribute(UriTemplate, myHTTPStatusCode)

        public HTTPErrorAttribute(String UriTemplate, HTTPStatusCode myHTTPStatusCode)
        {
            _HTTPMethod      = HTTPMethod.GET;
            _UriTemplate     = UriTemplate;
            _HTTPStatusCode  = myHTTPStatusCode;
        }

        #endregion

        #region HTTPErrorAttribute(HTTPMethod, UriTemplate, myHTTPStatusCode)

        public HTTPErrorAttribute(HTTPMethod HTTPMethod, String UriTemplate, HTTPStatusCode myHTTPStatusCode)
        {
            _HTTPMethod     = HTTPMethod;
            _UriTemplate    = UriTemplate;
            _HTTPStatusCode = myHTTPStatusCode;
        }

        #endregion

        #region HTTPErrorAttribute(HTTPMethod, UriTemplate, myHTTPStatusCode)

        public HTTPErrorAttribute(HTTPMethods HTTPMethod, String UriTemplate, HTTPStatusCode myHTTPStatusCode)
        {
            
            _HTTPMethod     = de.ahzf.Hermod.HTTP.Common.HTTPMethod.ParseEnum(HTTPMethod);

            if (_HTTPMethod == null)
                throw new ArgumentNullException("Invalid HTTPMethod!");

            _UriTemplate    = UriTemplate;
            _HTTPStatusCode = myHTTPStatusCode;

        }

        #endregion

        #endregion

    }

}
