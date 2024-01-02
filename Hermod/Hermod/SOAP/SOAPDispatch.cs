/*
 * Copyright (c) 2010-2024 GraphDefined GmbH <achim.friedland@graphdefined.com> <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
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

        /// <summary>
        /// A description for this SOAP dispatch.
        /// </summary>
        public String                     Description              { get; }

        /// <summary>
        /// A delegate to check if this dispatch applies.
        /// </summary>
        public SOAPMatch                  Matcher                  { get; }

        /// <summary>
        /// A HTTP/SOAP delegate to invoke this dispatch.
        /// </summary>
        public SOAPBodyDelegate           BodyDelegate             { get; }

        /// <summary>
        /// A HTTP/SOAP delegate to invoke this dispatch.
        /// </summary>
        public SOAPHeaderAndBodyDelegate  HeaderAndBodyDelegate    { get; }

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

            this.Description   = Description;
            this.Matcher       = Matcher;
            this.BodyDelegate  = BodyDelegate;

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

            this.Description            = Description;
            this.Matcher                = Matcher;
            this.HeaderAndBodyDelegate  = HeaderAndBodyDelegate;

        }

        #endregion

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()
            => Description;

        #endregion

    }

}
