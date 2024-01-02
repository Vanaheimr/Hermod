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

using System.Reflection;

using org.GraphDefined.Vanaheimr.Hermod.Logging;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// The common interface of all HTTP API extensions.
    /// </summary>
    public abstract class AHTTPAPIExtension<THTTPAPI> : AHTTPAPIBase

        where THTTPAPI : HTTPAPI

    {

        #region Data



        #endregion

        #region Properties

        /// <summary>
        /// The extended HTTP API.
        /// </summary>
        public THTTPAPI  HTTPBaseAPI    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Attach the given OCPP charging station management system WebAPI to the given HTTP API.
        /// </summary>
        /// <param name="HTTPAPI">A HTTP API.</param>
        /// <param name="URLPathPrefix">An optional prefix for the HTTP URLs.</param>
        public AHTTPAPIExtension(THTTPAPI                 HTTPAPI,
                                 String?                  HTTPServerName       = null,
                                 HTTPPath?                URLPathPrefix        = null,
                                 HTTPPath?                BasePath             = null,
                                 String?                  HTMLTemplate         = null,

                                 Boolean?                 IsDevelopment        = false,
                                 IEnumerable<String>?     DevelopmentServers   = null,
                                 Boolean?                 DisableLogging       = false,
                                 String?                  LoggingPath          = null,
                                 String?                  LogfileName          = DefaultHTTPAPI_LogfileName,
                                 LogfileCreatorDelegate?  LogfileCreator       = null)

            : base(HTTPServerName,
                   URLPathPrefix,
                   BasePath,
                   HTMLTemplate,

                   IsDevelopment,
                   DevelopmentServers,
                   DisableLogging,
                   LoggingPath,
                   LogfileName,
                   LogfileCreator)

        {

            this.HTTPBaseAPI  = HTTPAPI;

        }

        #endregion



        #region (protected override) GetResourceStream      (ResourceName, ResourceAssemblies)

        protected override Stream? GetResourceStream(String ResourceName)

            => GetResourceStream(ResourceName,
                                 new Tuple<String, Assembly>(HTTPExtAPI.HTTPRoot, typeof(HTTPExtAPI).Assembly),
                                 new Tuple<String, Assembly>(HTTPAPI.   HTTPRoot, typeof(HTTPAPI).   Assembly));

        #endregion

        #region (protected override) GetResourceMemoryStream(ResourceName, ResourceAssemblies)

        protected override MemoryStream? GetResourceMemoryStream(String ResourceName)

            => GetResourceMemoryStream(ResourceName,
                                       new Tuple<String, Assembly>(HTTPExtAPI.HTTPRoot, typeof(HTTPExtAPI).Assembly),
                                       new Tuple<String, Assembly>(HTTPAPI.   HTTPRoot, typeof(HTTPAPI).   Assembly));

        #endregion

        #region (protected override) GetResourceString      (ResourceName, ResourceAssemblies)

        protected override String GetResourceString(String ResourceName)

            => GetResourceString(ResourceName,
                                 new Tuple<String, Assembly>(HTTPExtAPI.HTTPRoot, typeof(HTTPExtAPI).Assembly),
                                 new Tuple<String, Assembly>(HTTPAPI.   HTTPRoot, typeof(HTTPAPI).   Assembly));

        #endregion

        #region (protected override) GetResourceBytes       (ResourceName, ResourceAssemblies)

        protected override Byte[] GetResourceBytes(String ResourceName)

            => GetResourceBytes(ResourceName,
                                new Tuple<String, Assembly>(HTTPExtAPI.HTTPRoot, typeof(HTTPExtAPI).Assembly),
                                new Tuple<String, Assembly>(HTTPAPI.   HTTPRoot, typeof(HTTPAPI).   Assembly));

        #endregion

        #region (protected override) MixWithHTMLTemplate    (ResourceName, ResourceAssemblies)

        protected override String MixWithHTMLTemplate(String ResourceName)

            => MixWithHTMLTemplate(ResourceName,
                                   new Tuple<String, Assembly>(HTTPExtAPI.HTTPRoot, typeof(HTTPExtAPI).Assembly),
                                   new Tuple<String, Assembly>(HTTPAPI.   HTTPRoot, typeof(HTTPAPI).   Assembly));

        #endregion

        #region (protected override) MixWithHTMLTemplate    (ResourceName, HTMLConverter, ResourceAssemblies)

        protected override String MixWithHTMLTemplate(String                ResourceName,
                                                      Func<String, String>  HTMLConverter)

            => MixWithHTMLTemplate(ResourceName,
                                   HTMLConverter,
                                   new Tuple<String, Assembly>(HTTPExtAPI.HTTPRoot, typeof(HTTPExtAPI).Assembly),
                                   new Tuple<String, Assembly>(HTTPAPI.   HTTPRoot, typeof(HTTPAPI).   Assembly));

        #endregion

        #region (protected override) MixWithHTMLTemplate    (Template, ResourceName, ResourceAssemblies)

        protected override String MixWithHTMLTemplate(String   Template,
                                                      String   ResourceName,
                                                      String?  Content   = null)

            => MixWithHTMLTemplate(Template,
                                   ResourceName,
                                   new[] {
                                       new Tuple<String, Assembly>(HTTPExtAPI.HTTPRoot, typeof(HTTPExtAPI).Assembly),
                                       new Tuple<String, Assembly>(HTTPAPI.   HTTPRoot, typeof(HTTPAPI).   Assembly)
                                   },
                                   Content);

        #endregion


    }

}
