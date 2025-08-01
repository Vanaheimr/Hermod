﻿/*
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
    public class RouteNode
    {

        public String                                   FullPath    { get; }

        public String                                   Path        { get; }

        public ConcurrentDictionary<String, RouteNode>  Children    { get; } = [];

        public HTTPAPIX?                                HTTPAPI     { get; }


        public RouteNode(String FullPath, String Path)
        {
            this.FullPath  = FullPath;
            this.Path      = Path;
        }

        public RouteNode(String FullPath, String Path, HTTPAPIX HTTPAPI)
        {
            this.FullPath  = FullPath;
            this.Path      = Path;
            this.HTTPAPI   = HTTPAPI;
        }



        public RouteNode? ParamChild { get; set; } // Für {param}
        public string? ParamName { get; set; } // Name des Params
        public Dictionary<HTTPMethod, Dictionary<HTTPContentType, HTTPRequestHandle>> Handlers { get; } = []; // Method -> ContentType -> Handler

    }

}
