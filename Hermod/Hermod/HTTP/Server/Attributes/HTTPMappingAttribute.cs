///*
// * Copyright (c) 2010-2023 GraphDefined GmbH
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

//namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
//{

//    /// <summary>
//    /// Mapps a HTTP request onto a .NET method.
//    /// </summary>
//    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
//    public class HTTPMappingAttribute : Attribute
//    {

//        #region Properties

//        /// <summary>
//        /// The HTTP method of this HTTP mapping.
//        /// </summary>
//        public HTTPMethod   HTTPMethod      { get; private set; }

//        /// <summary>
//        /// The URL template of this HTTP mapping.
//        /// </summary>
//        public String       URLTemplate     { get; private set; }

//        #endregion

//        #region Constructor(s)

//        #region HTTPMappingAttribute(HTTPMethod, URLTemplate)

//        /// <summary>
//        /// Generates a new HTTP mapping.
//        /// </summary>
//        /// <param name="HTTPMethod">The HTTP method of this HTTP mapping.</param>
//        /// <param name="URLTemplate">The URL template of this HTTP mapping.</param>
//        public HTTPMappingAttribute(HTTPMethod HTTPMethod, String URLTemplate)
//        {
//            this.HTTPMethod  = HTTPMethod;
//            this.URLTemplate = URLTemplate;
//        }

//        #endregion

//        #region HTTPMappingAttribute(HTTPMethod, URLTemplate)

//        /// <summary>
//        /// Generates a new HTTP mapping.
//        /// </summary>
//        /// <param name="HTTPMethod">The HTTP method of this HTTP mapping.</param>
//        /// <param name="URLTemplate">The URL template of this HTTP mapping.</param>
//        public HTTPMappingAttribute(HTTPMethods HTTPMethod, String URLTemplate)
//        {

//            var httpMethod  = HTTP.HTTPMethod.TryParse(HTTPMethod);

//            if (!httpMethod.HasValue)
//                throw new ArgumentNullException("Invalid HTTP method!");

//            this.HTTPMethod   = httpMethod.Value;
//            this.URLTemplate  = URLTemplate;

//        }

//        #endregion

//        #region HTTPMappingAttribute(Text, URLTemplate)

//        /// <summary>
//        /// Generates a new HTTP mapping.
//        /// </summary>
//        /// <param name="Text">The HTTP method of this HTTP mapping.</param>
//        /// <param name="URLTemplate">The URL template of this HTTP mapping.</param>
//        public HTTPMappingAttribute(String Text, String URLTemplate)
//        {

//            var httpMethod = HTTPMethod.TryParse(Text);

//            if (!httpMethod.HasValue)
//                throw new ArgumentNullException("Invalid HTTP method!");

//            this.HTTPMethod   = httpMethod.Value;
//            this.URLTemplate  = URLTemplate;

//        }

//        #endregion

//        #endregion

//    }

//}
