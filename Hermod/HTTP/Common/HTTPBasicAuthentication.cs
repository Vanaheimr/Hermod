/*
 * Copyright (c) 2010-2011, Achim 'ahzf' Friedland <code@ahzf.de>
 * This file is part of Hermod
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

using de.ahzf.Hermod;
using de.ahzf.Hermod.TCP.Common;

#endregion

namespace de.ahzf.Hermod.HTTP.Common
{
    
    public class HTTPBasicAuthentication
    {

        #region Properties

        #region Username

        private String _Username;

        public String Username
        {
            get
            {
                return _Username;
            }
        }

        #endregion

        #region Password

        private String _Password;

        public String Password
        {
            get
            {
                return _Password;
            }
        }

        #endregion

        #region HTTPCredentialType

        private HTTPAuthenticationTypes _HTTPCredentialType;

        public HTTPAuthenticationTypes HTTPCredentialType
        {
            get
            {
                return _HTTPCredentialType;
            }
        }

        #endregion

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create the credentials based on a base64 encoded string which comes from a HTTP header Authentication:
        /// </summary>
        /// <param name="myHTTPHeaderCredential"></param>
        public HTTPBasicAuthentication(String myHTTPHeaderCredential)
        {

            var splitted = myHTTPHeaderCredential.Split(new[] { ' ' });

            if (splitted.IsNullOrEmpty())
                throw new ArgumentException("invalid credentials " + myHTTPHeaderCredential);

            if (splitted[0].ToLower() == "basic")
            {

                _HTTPCredentialType = HTTPAuthenticationTypes.Basic;
                var usernamePassword = splitted[1].FromBase64().Split(new[] { ':' });

                if (usernamePassword.IsNullOrEmpty())
                    throw new ArgumentException("invalid username/password " + splitted[1].FromBase64());

                _Username = usernamePassword[0];
                _Password = usernamePassword[1];

            }
            
            else
                throw new ArgumentException("invalid credentialType " + splitted[0]);

        }

        #endregion

    }

}
