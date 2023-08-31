/*
 * Copyright (c) 2010-2023 GraphDefined GmbH
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

using System;
using System.Reflection;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Logging;
using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// The common interface of all HTTP API extensions.
    /// </summary>
    public abstract class AHTTPAPIExtension
    {

        #region Data



        #endregion

        #region Properties

        /// <summary>
        /// The extended HTTP API.
        /// </summary>
        public HTTPExtAPI                                 HTTPBaseAPI         { get; }

        /// <summary>
        /// The HTTP server name.
        /// </summary>
        public String                                     HTTPServerName      { get; }

        /// <summary>
        /// The default HTTP URL prefix.
        /// </summary>
        public HTTPPath                                   URLPathPrefix       { get; }


        public HTTPPath?                                  BasePath            { get; }


        /// <summary>
        /// The HTTP realm, if HTTP Basic Authentication is used.
        /// </summary>
        public String?                                    HTTPRealm           { get; }

        /// <summary>
        /// An enumeration of logins for an optional HTTP Basic Authentication.
        /// </summary>
        public IEnumerable<KeyValuePair<String, String>>  HTTPLogins          { get; }


        public String                                     HTMLTemplate        { get; }


        #endregion

        #region Constructor(s)

        /// <summary>
        /// Attach the given OCPP charging station management system WebAPI to the given HTTP API.
        /// </summary>
        /// <param name="HTTPAPI">A HTTP API.</param>
        /// <param name="URLPathPrefix">An optional prefix for the HTTP URLs.</param>
        /// <param name="HTTPRealm">The HTTP realm, if HTTP Basic Authentication is used.</param>
        /// <param name="HTTPLogins">An enumeration of logins for an optional HTTP Basic Authentication.</param>
        public AHTTPAPIExtension(HTTPExtAPI                                  HTTPAPI,
                                 String?                                     HTTPServerName   = null,
                                 HTTPPath?                                   URLPathPrefix    = null,
                                 HTTPPath?                                   BasePath         = null,
                                 String?                                     HTTPRealm        = null,
                                 IEnumerable<KeyValuePair<String, String>>?  HTTPLogins       = null,
                                 String?                                     HTMLTemplate     = null)
        {

            this.HTTPBaseAPI         = HTTPAPI;
            this.HTTPServerName      = HTTPServerName ?? HTTPAPI.HTTPServer.DefaultServerName;
            this.URLPathPrefix       = URLPathPrefix  ?? HTTPPath.Root;
            this.BasePath            = BasePath;

            this.HTTPRealm           = HTTPRealm;
            this.HTTPLogins          = HTTPLogins     ?? Array.Empty<KeyValuePair<String, String>>();
            this.HTMLTemplate        = HTMLTemplate   ?? GetResourceString("template.html");

        }

        #endregion



        #region (protected virtual) GetResourceStream      (ResourceName, ResourceAssemblies)

        protected virtual Stream? GetResourceStream(String ResourceName)

            => GetResourceStream(ResourceName,
                                 new Tuple<String, Assembly>(HTTPAPI.HTTPRoot, typeof(HTTPAPI).Assembly));

        protected virtual Stream? GetResourceStream(String                            ResourceName,
                                                    params Tuple<String, Assembly>[]  ResourceAssemblies)
        {

            foreach (var resourceAssembly in ResourceAssemblies)
            {
                try
                {

                    var resourceStream = resourceAssembly.Item2.GetManifestResourceStream(resourceAssembly.Item1 + ResourceName);

                    if (resourceStream is not null)
                        return resourceStream;

                }
                catch
                { }
            }

            return null;

        }

        #endregion

        #region (protected virtual) GetResourceMemoryStream(ResourceName, ResourceAssemblies)

        protected virtual MemoryStream? GetResourceMemoryStream(String ResourceName)

            => GetResourceMemoryStream(ResourceName,
                                       new Tuple<String, Assembly>(HTTPAPI.HTTPRoot, typeof(HTTPAPI).Assembly));

        protected virtual MemoryStream? GetResourceMemoryStream(String                            ResourceName,
                                                                params Tuple<String, Assembly>[]  ResourceAssemblies)
        {

            try
            {

                var resourceStream = GetResourceStream(ResourceName,
                                                       ResourceAssemblies);

                if (resourceStream is not null)
                {

                    var outputStream = new MemoryStream();
                    resourceStream.CopyTo(outputStream);
                    outputStream.Seek(0, SeekOrigin.Begin);

                    return outputStream;

                }

            }
            catch
            { }

            return null;

        }

        #endregion

        #region (protected virtual) GetResourceString      (ResourceName, ResourceAssemblies)

        protected virtual String GetResourceString(String ResourceName)

            => GetResourceString(ResourceName,
                                 new Tuple<String, Assembly>(HTTPAPI.HTTPRoot, typeof(HTTPAPI).Assembly));

        protected virtual String GetResourceString(String                            ResourceName,
                                                   params Tuple<String, Assembly>[]  ResourceAssemblies)

            => GetResourceMemoryStream(ResourceName, ResourceAssemblies)?.ToUTF8String() ?? String.Empty;

        #endregion

        #region (protected virtual) GetResourceBytes       (ResourceName, ResourceAssemblies)

        protected virtual Byte[] GetResourceBytes(String ResourceName)

            => GetResourceBytes(ResourceName,
                                new Tuple<String, Assembly>(HTTPAPI.HTTPRoot, typeof(HTTPAPI).Assembly));

        protected virtual Byte[] GetResourceBytes(String                            ResourceName,
                                                  params Tuple<String, Assembly>[]  ResourceAssemblies)

            => GetResourceMemoryStream(ResourceName, ResourceAssemblies)?.ToArray() ?? Array.Empty<Byte>();

        #endregion

        #region (protected virtual) MixWithHTMLTemplate    (ResourceName, ResourceAssemblies)

        protected virtual String MixWithHTMLTemplate(String ResourceName)

            => MixWithHTMLTemplate(ResourceName,
                                   new Tuple<String, Assembly>(HTTPAPI.HTTPRoot, typeof(HTTPAPI).Assembly));

        protected virtual String MixWithHTMLTemplate(String                            ResourceName,
                                                     params Tuple<String, Assembly>[]  ResourceAssemblies)
        {

            if (HTMLTemplate is not null)
            {

                var htmlStream = new MemoryStream();

                foreach (var assembly in ResourceAssemblies)
                {

                    var resourceStream = assembly.Item2.GetManifestResourceStream(assembly.Item1 + ResourceName);
                    if (resourceStream is not null)
                    {

                        resourceStream.Seek(3, SeekOrigin.Begin);
                        resourceStream.CopyTo(htmlStream);

                        return HTMLTemplate.Replace("<%= content %>",  htmlStream.ToArray().ToUTF8String()).
                                            Replace("{{BasePath}}",    BasePath?.ToString() ?? "");

                    }

                }

                return HTMLTemplate.Replace("<%= content %>",  "").
                                    Replace("{{BasePath}}",    "");

            }

            return String.Empty;

        }

        #endregion

        #region (protected virtual) MixWithHTMLTemplate    (ResourceName, HTMLConverter, ResourceAssemblies)

        protected virtual String MixWithHTMLTemplate(String                ResourceName,
                                                     Func<String, String>  HTMLConverter)

            => MixWithHTMLTemplate(ResourceName,
                                   HTMLConverter,
                                   new Tuple<String, Assembly>(HTTPAPI.HTTPRoot, typeof(HTTPAPI).Assembly));

        protected virtual String MixWithHTMLTemplate(String                            ResourceName,
                                                     Func<String, String>              HTMLConverter,
                                                     params Tuple<String, Assembly>[]  ResourceAssemblies)
        {

            if (HTMLTemplate is not null)
            {

                var htmlStream = new MemoryStream();

                foreach (var assembly in ResourceAssemblies)
                {

                    var resourceStream = assembly.Item2.GetManifestResourceStream(assembly.Item1 + ResourceName);
                    if (resourceStream is not null)
                    {

                        resourceStream.Seek(3, SeekOrigin.Begin);
                        resourceStream.CopyTo(htmlStream);

                        return HTMLConverter(HTMLTemplate.Replace("<%= content %>",  htmlStream.ToArray().ToUTF8String()).
                                                          Replace("{{BasePath}}",    BasePath?.ToString() ?? ""));

                    }

                }

                return HTMLConverter(HTMLTemplate.Replace("<%= content %>",  "").
                                                  Replace("{{BasePath}}",    ""));

            }

            return String.Empty;

        }

        #endregion

        #region (protected virtual) MixWithHTMLTemplate    (Template, ResourceName, ResourceAssemblies)

        protected virtual String MixWithHTMLTemplate(String   Template,
                                                     String   ResourceName,
                                                     String?  Content   = null)

            => MixWithHTMLTemplate(Template,
                                   ResourceName,
                                   new Tuple<String, Assembly>[] { new Tuple<String, Assembly>(HTTPAPI.HTTPRoot, typeof(HTTPAPI).Assembly) },
                                   Content);

        protected virtual String MixWithHTMLTemplate(String                     Template,
                                                     String                     ResourceName,
                                                     Tuple<String, Assembly>[]  ResourceAssemblies,
                                                     String?                    Content   = null)
        {

            var htmlStream = new MemoryStream();

            foreach (var assembly in ResourceAssemblies)
            {

                var resourceStream = assembly.Item2.GetManifestResourceStream(assembly.Item1 + ResourceName);
                if (resourceStream is not null)
                {

                    resourceStream.Seek(3, SeekOrigin.Begin);
                    resourceStream.CopyTo(htmlStream);

                    return Template.Replace("<%= content %>",  htmlStream.ToArray().ToUTF8String()).
                                    Replace("{{BasePath}}",    BasePath?.ToString() ?? "");

                }

            }

            return Template.Replace("<%= content %>",  "").
                            Replace("{{BasePath}}",    "");

        }

        #endregion



    }

}
