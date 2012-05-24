/*
 * Copyright (c) 2010-2012, Achim 'ahzf' Friedland <achim@graph-database.org>
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

#endregion

namespace de.ahzf.Hermod.HTTP
{

    /// <summary>
    /// A structure to hold a result and an error of an operation.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    public struct HTTPResult<T>
    {

        #region Data

        /// <summary>
        /// The HTTPResponse when an error occured.
        /// </summary>
        public readonly HTTPResponse Error;

        /// <summary>
        /// The result of an operation.
        /// </summary>
        public readonly T Data;

        #endregion

        #region Properties

        #region HasErrors

        /// <summary>
        /// The HTTP result contains errors.
        /// </summary>
        public Boolean HasErrors
        {
            get
            {
                return (Error != null);
            }
        }

        #endregion

        #endregion

        #region Constructor(s)

        #region HTTPResult(Result)

        public HTTPResult(T Result)
        {
            this.Error = null;
            this.Data = Result;
        }

        #endregion

        #region HTTPResult(HTTPResponse)

        /// <summary>
        /// Create a new HTTPResult when an error occurred.
        /// </summary>
        /// <param name="Error">The HTTPResponse for this error.</param>
        public HTTPResult(HTTPResponse HTTPResponse)
        {
            this.Error  = HTTPResponse;
            this.Data = default(T);
        }

        #endregion

        #region HTTPResult(HTTPRequest, HTTPStatusCode, Reasons = null)

        /// <summary>
        /// Create a new HTTPResult when an error occurred.
        /// </summary>
        /// <param name="HTTPRequest"></param>
        /// <param name="HTTPStatusCode"></param>
        /// <param name="Error">The HTTPResponse for this error.</param>
        public HTTPResult(HTTPRequest HTTPRequest, HTTPStatusCode HTTPStatusCode, String Reasons = null)
        {
            this.Error  = HTTPErrors.HTTPErrorResponse(HTTPRequest, HTTPStatusCode, Reasons);
            this.Data = default(T);
        }

        #endregion

        #region HTTPResult(HTTPResponse, Data)

        public HTTPResult(HTTPResponse HTTPResponse, T Data)
        {
            this.Error = HTTPResponse;
            this.Data  = Data;
        }

        #endregion

        #endregion

    }

}
