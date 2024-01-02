/*
 * Copyright (c) 2014-2024 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of HTTPExtAPI <https://www.github.com/Vanaheimr/HTTPExtAPI>
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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    public class SecurityToken
    {

        public User_Id   UserId        { get; }
        public DateTime  Expires       { get; }
        public User_Id?  SuperUserId   { get; }

        public SecurityToken(User_Id   UserId,
                             DateTime  Expires,
                             User_Id?  SuperUserId = null)
        {

            this.UserId       = UserId;
            this.Expires      = Expires;
            this.SuperUserId  = SuperUserId;

        }

        public String ToLogLine()
            => String.Concat(UserId, ";", Expires.ToIso8601(), SuperUserId.HasValue ? ";" + SuperUserId.Value : "");

    }

}
