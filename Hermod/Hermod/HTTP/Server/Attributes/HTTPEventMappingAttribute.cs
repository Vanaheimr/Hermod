///*
// * Copyright (c) 2010-2024 GraphDefined GmbH <achim.friedland@graphdefined.com>
// * This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
// *
// * Licensed under the Apache License, Version 2.0 (the "License");
// * you may not use this file except in compliance with the License.
// * You may obtain a copy of the License at
// *
// *     http://www.apache.org/licenses/LICENSE-2.0
// *
// * Unless required by applicable law or agreed to in writing, software
// * distributed under the License is distributed on an "AS IS" BASIS,
// * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// * See the License for the specific language governing permissions and
// * limitations under the License.
// */

//#region Usings

//using System;

//#endregion

//namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
//{

//    /// <summary>
//    /// Mapps a HTTP event request onto a .NET method.
//    /// </summary>
//    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
//    public class HTTPEventMappingAttribute : Attribute
//    {

//        #region Properties

//        /// <summary>
//        /// The internal identification of the HTTP event.
//        /// </summary>
//        public String      EventIdentification      { get; private set; }

//        /// <summary>
//        /// The URI template of this HTTP event mapping.
//        /// </summary>
//        public String      UriTemplate              { get; private set; }

//        /// <summary>
//        /// The HTTP method to use.
//        /// </summary>
//        public HTTPMethod  HTTPMethod               { get; private set; }

//        /// <summary>
//        /// Maximum number of cached events.
//        /// Zero means infinite.
//        /// </summary>
//        public UInt32      MaxNumberOfCachedEvents  { get; private set; }

//        /// <summary>
//        /// Retry intervall.
//        /// </summary>
//        public TimeSpan    RetryIntervall           { get; private set; }

//        /// <summary>
//        /// The event source may be accessed via multiple URI templates.
//        /// </summary>
//        public Boolean     IsSharedEventSource      { get; private set; }

//        #endregion

//        #region Constructor(s)

//        #region HTTPEventMappingAttribute(EventIdentification, UriTemplate, MaxNumberOfCachedEvents = 0, RetryIntervallSeconds = 30, IsSharedEventSource = false)

//        /// <summary>
//        /// Creates a new HTTP event mapping.
//        /// </summary>
//        /// <param name="EventIdentification">The internal identification of the HTTP event.</param>
//        /// <param name="UriTemplate">The URI template of this HTTP event mapping.</param>
//        /// <param name="MaxNumberOfCachedEvents">Maximum number of cached events (0 means infinite).</param>
//        /// <param name="RetryIntervallSeconds">The retry intervall in seconds.</param>
//        /// <param name="IsSharedEventSource">The event source may be accessed via multiple URI templates.</param>
//        public HTTPEventMappingAttribute(String   EventIdentification,
//                                         String   UriTemplate,
//                                         UInt32   MaxNumberOfCachedEvents  = 0,
//                                         UInt64   RetryIntervallSeconds    = 30,
//                                         Boolean  IsSharedEventSource      = false)

//        {

//            this.EventIdentification      = EventIdentification;
//            this.UriTemplate              = UriTemplate;
//            this.HTTPMethod               = HTTPMethod.GET;
//            this.MaxNumberOfCachedEvents  = MaxNumberOfCachedEvents;
//            this.RetryIntervall           = TimeSpan.FromSeconds(RetryIntervallSeconds);
//            this.IsSharedEventSource      = IsSharedEventSource;

//        }

//        #endregion

//        #region HTTPEventMappingAttribute(EventIdentification, UriTemplate, HTTPMethod, MaxNumberOfCachedEvents = 0, RetryIntervallSeconds = 30, IsSharedEventSource = false)

//        /// <summary>
//        /// Creates a new HTTP event mapping.
//        /// </summary>
//        /// <param name="EventIdentification">The internal identification of the HTTP event.</param>
//        /// <param name="UriTemplate">The URI template of this HTTP event mapping.</param>
//        /// <param name="HTTPMethod">The HTTP method to use.</param>
//        /// <param name="MaxNumberOfCachedEvents">Maximum number of cached events (0 means infinite).</param>
//        /// <param name="RetryIntervallSeconds">The retry intervall in seconds.</param>
//        /// <param name="IsSharedEventSource">The event source may be accessed via multiple URI templates.</param>
//        public HTTPEventMappingAttribute(String       EventIdentification,
//                                         String       UriTemplate,
//                                         HTTPMethods  HTTPMethod,
//                                         UInt32       MaxNumberOfCachedEvents  = 0,
//                                         UInt64       RetryIntervallSeconds    = 30,
//                                         Boolean      IsSharedEventSource      = false)

//        {

//            this.EventIdentification      = EventIdentification;
//            this.UriTemplate              = UriTemplate;
//            this.HTTPMethod               = org.GraphDefined.Vanaheimr.Hermod.HTTP.HTTPMethod.Parse(HTTPMethod);
//            this.MaxNumberOfCachedEvents  = MaxNumberOfCachedEvents;
//            this.RetryIntervall           = TimeSpan.FromSeconds(RetryIntervallSeconds);
//            this.IsSharedEventSource      = IsSharedEventSource;

//        }

//        #endregion

//        #endregion

//    }

//}
