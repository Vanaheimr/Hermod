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

        #region BodyDelegate

        private readonly SOAPBodyDelegate _BodyDelegate;

        /// <summary>
        /// A HTTP/SOAP delegate to invoke this dispatch.
        /// </summary>
        public SOAPBodyDelegate BodyDelegate
        {
            get
            {
                return _BodyDelegate;
            }
        }

        #endregion

        #region HeaderAndBodyDelegate

        private readonly SOAPHeaderAndBodyDelegate _HeaderAndBodyDelegate;

        /// <summary>
        /// A HTTP/SOAP delegate to invoke this dispatch.
        /// </summary>
        public SOAPHeaderAndBodyDelegate HeaderAndBodyDelegate
        {
            get
            {
                return _HeaderAndBodyDelegate;
            }
        }

        #endregion

        #endregion

        #region Constructor(s)

        #region SOAPDispatch(Description, Matcher, BodyDelegate)

        /// <summary>
        /// Create a new SOAP dispatch.
        /// </summary>
        /// <param name="Description">A description for this SOAP dispatch.</param>
        /// <param name="Matcher">A delegate to check if this dispatch applies.</param>
        /// <param name="BodyDelegate">A HTTP/SOAP delegate to invoke this dispatch.</param>
        public SOAPDispatch(String            Description,
                            SOAPMatch         Matcher,
                            SOAPBodyDelegate  BodyDelegate)
        {

            this._Description   = Description;
            this._Matcher       = Matcher;
            this._BodyDelegate  = BodyDelegate;

        }

        #endregion

        #region SOAPDispatch(Description, Matcher, HeaderAndBodyDelegate)

        /// <summary>
        /// Create a new SOAP dispatch.
        /// </summary>
        /// <param name="Description">A description for this SOAP dispatch.</param>
        /// <param name="Matcher">A delegate to check if this dispatch applies.</param>
        /// <param name="HeaderAndBodyDelegate">A HTTP/SOAP delegate to invoke this dispatch.</param>
        public SOAPDispatch(String                     Description,
                            SOAPMatch                  Matcher,
                            SOAPHeaderAndBodyDelegate  HeaderAndBodyDelegate)
        {

            this._Description            = Description;
            this._Matcher                = Matcher;
            this._HeaderAndBodyDelegate  = HeaderAndBodyDelegate;

        }

        #endregion

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()
        {
            return Description;
        }

        #endregion

    }

}
