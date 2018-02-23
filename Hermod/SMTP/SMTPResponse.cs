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

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    /// <summary>
    /// A SMTP response.
    /// </summary>
    public class SMTPResponse
    {

        #region Properties

        #region StatusCode

        private readonly SMTPStatusCode _StatusCode;

        /// <summary>
        /// The SMTP status code.
        /// </summary>
        public SMTPStatusCode StatusCode
        {
            get
            {
                return _StatusCode;
            }
        }

        #endregion

        #region Response

        private readonly String _Response;

        /// <summary>
        /// The SMTP response text.
        /// </summary>
        public String Response
        {
            get
            {
                return _Response;
            }
        }

        #endregion

        #endregion

        #region SMTPResponse(StatusCode, Response = "")

        /// <summary>
        /// Create a new SMTP response.
        /// </summary>
        /// <param name="StatusCode">The SMTP status code.</param>
        /// <param name="Response">The SMTP response text.</param>
        public SMTPResponse(SMTPStatusCode  StatusCode,
                            String          Response  = "")
        {
            this._StatusCode  = StatusCode;
            this._Response    = Response;
        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override String ToString()
        {
            return StatusCode.ToString() + " --> " + Response;
        }

        #endregion

    }

}
