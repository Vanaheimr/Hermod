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

using System.ServiceModel;
using System;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    public class HTTPSecurity
    {

        #region Properties

        /// <summary>
        /// A username + password verifier.
        /// </summary>
        public Func<String, String, Boolean> Verify { get; private set; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTTP security object.
        /// </summary>
        /// <param name="Verify">A username + password verifier.</param>
        public HTTPSecurity(Func<String, String, Boolean> Verify)
        {

            #region Initial checks

            if (Verify == null)
                throw new ArgumentNullException("Verify", "Verify must not be null!");

            #endregion

            this.Verify = Verify;

        }

        #endregion

    }

}
