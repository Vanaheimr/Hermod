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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Passkeys
{

    public class VerificationResult
    {

        public Boolean  IsSuccess       { get; }
        public String?  ErrorMessage    { get; }


        private VerificationResult(Boolean  IsSuccess,
                                   String?  ErrorMessage   = null)
        {
            this.IsSuccess     = IsSuccess;
            this.ErrorMessage  = ErrorMessage;
        }


        public static VerificationResult Success()
            => new (IsSuccess: true);

        public static VerificationResult Failed(String ErrorMessage)
            => new (IsSuccess:     false,
                    ErrorMessage:  ErrorMessage);

        public JObject ToJSON()

            => JSONObject.Create(

                         new JProperty("isSuccess",     IsSuccess),

                   ErrorMessage.IsNotNullOrEmpty()
                       ? new JProperty("errorMessage",  ErrorMessage)
                       : null

               );

    }

}
