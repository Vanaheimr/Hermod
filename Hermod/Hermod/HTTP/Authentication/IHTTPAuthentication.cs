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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    public static class HTTPAuthenticationExtensions
    {

        public static Boolean TryParse(String authorizationString, out IHTTPAuthentication? HTTPAuthentication)
        {

            if (authorizationString is not null)
            {

                if (HTTPBasicAuthentication.TryParseHTTPHeader(authorizationString, out var basicAuthentication))
                {
                    HTTPAuthentication = basicAuthentication;
                    return true;
                }

                if (HTTPBearerAuthentication.TryParseHTTPHeader(authorizationString, out var bearerAuthentication))
                {
                    HTTPAuthentication = bearerAuthentication;
                    return true;
                }

                if (HTTPTokenAuthentication.TryParseHTTPHeader(authorizationString, out var tokenAuthentication))
                {
                    HTTPAuthentication = tokenAuthentication;
                    return true;
                }

                if (HTTPTOTPAuthentication.TryParseHTTPHeader(authorizationString, out var totpAuthentication))
                {
                    HTTPAuthentication = totpAuthentication;
                    return true;
                }

            }

            HTTPAuthentication = null;
            return false;

        }

    }


    /// <summary>
    /// The common interface for all HTTP authentications.
    /// </summary>
    public interface IHTTPAuthentication
    {

        /// <summary>
        /// The HTTP request header representation.
        /// </summary>
        String  HTTPText    { get; }

    }

}
