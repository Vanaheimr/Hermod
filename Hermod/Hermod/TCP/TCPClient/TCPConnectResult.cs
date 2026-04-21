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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.TCP
{

    public readonly struct TCPConnectionResult
    {

        public Boolean                   IsSuccess    { get; }

        public Boolean                   IsFailure
            => !IsSuccess;

        public IEnumerable<Error>        Errors       { get; }
        public TCPClientConnectTimings?  Timings      { get; }


        public TCPConnectionResult(Boolean                   IsSuccess,
                                   IEnumerable<Error>?       Errors    = null,
                                   TCPClientConnectTimings?  Timings   = null)
        {

            this.IsSuccess  = IsSuccess;
            this.Errors     = Errors ?? [];
            this.Timings    = Timings;

        }


        public static TCPConnectionResult Success(TCPClientConnectTimings? Timings = null)

            => new (true,
                    [],
                    Timings);


        public static TCPConnectionResult Failed(IEnumerable<Error>?       Errors    = null,
                                                 TCPClientConnectTimings?  Timings   = null)

            => new (false,
                    Errors,
                    Timings);


        public static TCPConnectionResult Failed(IEnumerable<String>?      ErrorMessages   = null,
                                                 TCPClientConnectTimings?  Timings         = null)

            => new (false,
                    ErrorMessages?.Select(errorMessage => Error.Create(errorMessage)) ?? [],
                    Timings);


        public static TCPConnectionResult Failed(String                    ErrorMessage,
                                                 TCPClientConnectTimings?  Timings   = null)

            => new (false,
                    [ Error.Create(ErrorMessage) ],
                    Timings);


    }



    public enum TCPConnectResult
    {
        Ok,
        InvalidRemoteHost,
        InvalidDomainName,
        NoIPAddressFound,
        UnknownError
    }

}
