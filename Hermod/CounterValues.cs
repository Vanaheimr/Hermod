/*
 * Copyright (c) 2010-2021, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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
using System.Threading;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    public struct CounterValues
    {

        private Int64 requests;
        private Int64 responses_OK;
        private Int64 responses_Error;

        public UInt64 Requests         => unchecked((UInt64) requests);
        public UInt64 Responses_OK     => unchecked((UInt64) responses_OK);
        public UInt64 Responses_Error  => unchecked((UInt64) responses_Error);

        public CounterValues(UInt64 Requests,
                             UInt64 Responses_OK,
                             UInt64 Responses_Error)
        {

            this.requests         = (Int64) Requests;
            this.responses_OK     = (Int64) Responses_OK;
            this.responses_Error  = (Int64) Responses_Error;

        }

        public CounterValues IncRequests()
        {
            Interlocked.Increment(ref requests);
            return this;
        }

        public CounterValues IncResponses_OK()
        {
            Interlocked.Increment(ref responses_OK);
            return this;
        }

        public CounterValues IncResponses_Error()
        {
            Interlocked.Increment(ref responses_Error);
            return this;
        }


        public JObject ToJSON()

                => JSONObject.Create(
                       new JProperty("requests",       requests),
                       new JProperty("responsesOK",    responses_OK),
                       new JProperty("responsesError", responses_Error)
                   );

    }

}
