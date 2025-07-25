﻿/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System.Xml.Linq;
using System.Threading.Tasks;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SOAP
{

    /// <summary>
    /// An HTTP delegate.
    /// </summary>
    /// <param name="Request">The HTTP request.</param>
    /// <param name="SOAPBody">The parsed SOAP/XML request body.</param>
    /// <returns>An HTTP response task.</returns>
    public delegate Task<HTTPResponse> SOAPBodyDelegate(HTTPRequest  Request,
                                                        XElement     SOAPBody);

    /// <summary>
    /// An HTTP delegate.
    /// </summary>
    /// <param name="Request">The HTTP request.</param>
    /// <param name="SOAPHeader">The parsed SOAP/XML request header.</param>
    /// <param name="SOAPBody">The parsed SOAP/XML request body.</param>
    /// <returns>An HTTP response task.</returns>
    public delegate Task<HTTPResponse> SOAPHeaderAndBodyDelegate(HTTPRequest  Request,
                                                                 XElement     SOAPHeader,
                                                                 XElement     SOAPBody);

    /// <summary>
    /// A delegate for checking if a given XML matches.
    /// </summary>
    /// <param name="SOAPXML">A XML.</param>
    /// <returns>The matching XML (sub-)element.</returns>
    public delegate XElement SOAPMatch(XElement  SOAPXML);

}
