/*
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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Illias;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using static org.GraphDefined.Vanaheimr.Hermod.HTTP.HTTPServer;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTPTest
{

    /// <summary>
    /// A node which stores information for maintaining multiple http hostnames.
    /// </summary>
    public class RouteNode2
    {

        public String                                    FullPath           { get; }

        public String                                    Path               { get; }

        public String?                                   ParamName          { get; }

        public Boolean                                   CatchRestOfPath    { get; } = false;

        public ConcurrentDictionary<String, RouteNode2>  Children           { get; } = [];


        private RouteNode2(String              FullPath,
                           String              Path,
                           String?             ParamName,
                           Boolean             CatchRestOfPath,
                           HTTPRequestHandleX  RequestHandle)
        {

            this.FullPath         = FullPath;
            this.Path             = Path;
            this.ParamName        = ParamName;
            this.CatchRestOfPath  = CatchRestOfPath;
            this.RequestHandle    = RequestHandle;

        }

        public static RouteNode2 FromPath(String FullPath, String Path, HTTPRequestHandleX RequestHandle)
            => new (FullPath, Path, null, false, RequestHandle);


        public static RouteNode2 ForParameter(String FullPath, String ParamName, HTTPRequestHandleX RequestHandle)
            => new (FullPath, "", ParamName, false, RequestHandle);

        public static RouteNode2 ForCatchRestOfPath(String FullPath, String ParamName, HTTPRequestHandleX RequestHandle)
            => new (FullPath, "", ParamName, true, RequestHandle);


        //public RouteNode2? ParamChild { get; set; } // Für {param}

        public HTTPRequestHandleX RequestHandle { get; set; }

        public Dictionary<HTTPMethod, Dictionary<HTTPContentType, HTTPRequestHandleX>> Handlers { get; } = []; // Method -> ContentType -> Handler

    }

}
