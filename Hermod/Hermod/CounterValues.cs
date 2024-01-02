/*
 * Copyright (c) 2010-2024 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A helper class to count API requests responses.
    /// (Note: Must be a class, otherwise the counters do not increment!)
    /// </summary>
    public class APICounterValues
    {

        #region Data

        private Int64 requests_OK;
        private Int64 requests_Error;

        private Int64 responses_OK;
        private Int64 responses_Error;

        #endregion

        #region Properties

        /// <summary>
        /// Number of valid API requests.
        /// </summary>
        public UInt64 Requests_OK      => unchecked((UInt64) requests_OK);

        /// <summary>
        /// Number of invalid API requests.
        /// </summary>
        public UInt64 Requests_Error   => unchecked((UInt64) requests_Error);


        /// <summary>
        /// Number of valid API responses.
        /// </summary>
        public UInt64 Responses_OK     => unchecked((UInt64) responses_OK);

        /// <summary>
        /// Number of valid API responses.
        /// </summary>
        public UInt64 Responses_Error  => unchecked((UInt64) responses_Error);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create new API counter values.
        /// </summary>
        /// <param name="Requests_OK">Number of valid API requests.</param>
        /// <param name="Requests_Error">Number of invalid API requests.</param>
        /// <param name="Responses_OK">Number of valid API responses.</param>
        /// <param name="Responses_Error">Number of valid API responses.</param>
        public APICounterValues(UInt64?  Requests_OK       = 0UL,
                                UInt64?  Requests_Error    = 0UL,

                                UInt64?  Responses_OK      = 0UL,
                                UInt64?  Responses_Error   = 0UL)
        {

            this.requests_OK      = (Int64) (Requests_OK     ?? 0UL);
            this.requests_Error   = (Int64) (Requests_Error  ?? 0UL);

            this.responses_OK     = (Int64) (Responses_OK    ?? 0UL);
            this.responses_Error  = (Int64) (Responses_Error ?? 0UL);

        }

        #endregion


        #region IncRequests_OK()

        /// <summary>
        /// Increment the number of valid API requests.
        /// </summary>
        public APICounterValues IncRequests_OK()
        {
            Interlocked.Increment(ref requests_OK);
            return this;
        }

        #endregion

        #region IncRequests_Error()

        /// <summary>
        /// Increment the number of invalid API requests.
        /// </summary>
        public APICounterValues IncRequests_Error()
        {
            Interlocked.Increment(ref requests_Error);
            return this;
        }

        #endregion


        #region IncResponses_OK()

        /// <summary>
        /// Increment the number of valid API responses.
        /// </summary>
        public APICounterValues IncResponses_OK()
        {
            Interlocked.Increment(ref responses_OK);
            return this;
        }

        #endregion

        #region IncResponses_Error()

        /// <summary>
        /// Increment the number of invalid API responses.
        /// </summary>
        public APICounterValues IncResponses_Error()
        {
            Interlocked.Increment(ref responses_Error);
            return this;
        }

        #endregion


        #region ToJSON()

        /// <summary>
        /// Return a JSON representation of this data structure.
        /// </summary>
        public JObject ToJSON()

                => JSONObject.Create(
                       new JProperty("requests",       requests_OK),
                       new JProperty("responsesOK",    responses_OK),
                       new JProperty("responsesError", responses_Error)
                   );

        #endregion

    }

}
