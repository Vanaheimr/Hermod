/*
 * Copyright (c) 2010-2015, Achim 'ahzf' Friedland <achim@graphdefined.org>
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

namespace org.GraphDefined.Vanaheimr.Hermod.Services.SMTP
{

    /// <summary>
    /// An extended SMTP responses.
    /// </summary>
    public class SMTPExtendedResponse : SMTPResponse
    {

        #region Properties

        #region MoreDataAvailable

        private readonly Boolean _MoreDataAvailable;

        /// <summary>
        /// True, if more result lines are available.
        /// </summary>
        public Boolean MoreDataAvailable
        {
            get
            {
                return _MoreDataAvailable;
            }
        }

        #endregion

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new SMTP response.
        /// </summary>
        /// <param name="StatusCode">The SMTP status code.</param>
        /// <param name="Response">The SMTP response text.</param>
        /// <param name="MoreDataAvailable">True, if more result lines are available.</param>
        public SMTPExtendedResponse(SMTPStatusCode  StatusCode,
                                    String          Response           = "",
                                    Boolean         MoreDataAvailable  = false)

            : base(StatusCode, Response)

        {
            this._MoreDataAvailable  = MoreDataAvailable;
        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        public override String ToString()
        {
            return StatusCode.ToString() + " -" + (MoreDataAvailable ? "more" : "") + "-> " + Response;
        }

        #endregion

    }

}
