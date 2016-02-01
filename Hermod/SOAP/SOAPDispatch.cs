/*
 * Copyright (c) 2010-2016, Achim 'ahzf' Friedland <achim@graphdefined.org>
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

namespace org.GraphDefined.Vanaheimr.Hermod.SOAP
{

    /// <summary>
    /// A SOAP dispach.
    /// </summary>
    public class SOAPDispatch
    {

        #region Properties

        #region Description

        private readonly String _Description;

        /// <summary>
        /// A description for this SOAP dispatch.
        /// </summary>
        public String Description
        {
            get
            {
                return _Description;
            }
        }

        #endregion

        #region Matcher

        private readonly SOAPMatch _Matcher;

        /// <summary>
        /// A delegate to check if this dispatch applies.
        /// </summary>
        public SOAPMatch Matcher
        {
            get
            {
                return _Matcher;
            }
        }

        #endregion

        #region Delegate

        private readonly SOAPDelegate _Delegate;

        /// <summary>
        /// A HTTP/SOAP delegate to invoke this dispatch.
        /// </summary>
        public SOAPDelegate Delegate
        {
            get
            {
                return _Delegate;
            }
        }

        #endregion

        #endregion

        #region SOAPDispatch(Description, Matcher, Delegate)

        /// <summary>
        /// Create a new SOAP dispatch.
        /// </summary>
        /// <param name="Description">A description for this SOAP dispatch.</param>
        /// <param name="Matcher">A delegate to check if this dispatch applies.</param>
        /// <param name="Delegate">A HTTP/SOAP delegate to invoke this dispatch.</param>
        public SOAPDispatch(String        Description,
                            SOAPMatch     Matcher,
                            SOAPDelegate  Delegate)
        {

            this._Description  = Description;
            this._Matcher      = Matcher;
            this._Delegate     = Delegate;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a string representation of this object.
        /// </summary>
        public override String ToString()
        {
            return Description;
        }

        #endregion

    }

}
