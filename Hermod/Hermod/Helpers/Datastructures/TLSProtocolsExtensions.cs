/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Security.Authentication;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// TLS protocol extensions.
    /// </summary>
    public static class TLSProtocolsExtensions
    {

        public static JArray ToJSON(this SslProtocols TLSProtocols)
        {

            var jsonArray = new JArray();

            //// Summary:
            ////     Allows the operating system to choose the best protocol to use, and to block
            ////     protocols that are not secure. Unless your app has a specific reason not to,
            ////     you should use this field.
            //None = 0,

            //// Summary:
            ////     Specifies the SSL 2.0 protocol. SSL 2.0 has been superseded by the TLS protocol
            ////     and is provided for backward compatibility only.
            //Ssl2 = 12,

            //// Summary:
            ////     Specifies the SSL 3.0 protocol. SSL 3.0 has been superseded by the TLS protocol
            ////     and is provided for backward compatibility only.
            //Ssl3 = 48,

            //// Summary:
            ////     Specifies the TLS 1.0 security protocol. TLS 1.0 is provided for backward compatibility
            ////     only. The TLS protocol is defined in IETF RFC 2246. This member is obsolete starting
            ////     in .NET 7.
            //Tls = 192,

            //// Summary:
            ////     Use None instead of Default. Default permits only the Secure Sockets Layer (SSL)
            ////     3.0 or Transport Layer Security (TLS) 1.0 protocols to be negotiated, and those
            ////     options are now considered obsolete. Consequently, Default is not allowed in
            ////     many organizations. Despite the name of this field, System.Net.Security.SslStream
            ////     does not use it as a default except under special circumstances.
            //Default = 240,

            //// Summary:
            ////     Specifies the TLS 1.1 security protocol. The TLS protocol is defined in IETF
            ////     RFC 4346. This member is obsolete starting in .NET 7.
            //Tls11 = 768,

            //// Summary:
            ////     Specifies the TLS 1.2 security protocol. The TLS protocol is defined in IETF
            ////     RFC 5246.
            //Tls12 = 3072,

            //// Summary:
            ////     Specifies the TLS 1.3 security protocol. The TLS protocol is defined in IETF
            ////     RFC 8446.
            //Tls13 = 12288

            foreach (var sslProtocol in Enum.GetValues<SslProtocols>())
            {
#pragma warning disable SYSLIB0039 // Type or member is obsolete
#pragma warning disable CS0618     // Type or member is obsolete
                if (TLSProtocols.HasFlag(sslProtocol)   &&
                    sslProtocol != SslProtocols.None    &&
                    sslProtocol != SslProtocols.Ssl2    &&
                    sslProtocol != SslProtocols.Ssl3    &&
                    sslProtocol != SslProtocols.Tls     &&
                    sslProtocol != SslProtocols.Default &&
                    sslProtocol != SslProtocols.Tls11)
                {
                    switch (sslProtocol)
                    {

                        case SslProtocols.Tls12:
                            jsonArray.Add("TLSv1.2");
                            break;

                        case SslProtocols.Tls13:
                            jsonArray.Add("TLSv1.3");
                            break;

                        default:
                            throw new NotImplementedException($"Could not serialize unknown TLS protocol '{sslProtocol}'!");

                    }
                }
#pragma warning restore CS0618     // Type or member is obsolete
#pragma warning restore SYSLIB0039 // Type or member is obsolete
            }

            return jsonArray;

        }


        public static Boolean TryParseJSON(JArray                            JSONArray,
                                           out SslProtocols                  TLSProtocols,
                                           [NotNullWhen(false)] out String?  ErrorString)
        {

            TLSProtocols  = SslProtocols.None;
            ErrorString   = null;

            foreach (var token in JSONArray)
            {
                if (token.Type == JTokenType.String)
                {
                    switch (token.Value<String>())
                    {

                        case "TLSv1.2":
                            TLSProtocols |= SslProtocols.Tls12;
                            break;

                        case "TLSv1.3":
                            TLSProtocols |= SslProtocols.Tls13;
                            break;

                        default:
                            ErrorString = $"Could not parse unknown TLS protocol '{token.Value<String>()}'!";
                            return false;

                    }
                }
            }

            return true;

        }


    }

}
