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

using System.Reflection;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Logging;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTPTest
{

    /// <summary>
    /// The common base of all HTTP APIs.
    /// </summary>
    public abstract class AHTTPAPIXBase
    {

        #region Data

        public const String  DefaultHTTPServerName       = "HTTP Server 1";
        public const String  DefaultHTTPServiceName      = "HTTP Service";
        public const String  DefaultLoggingContext       = "HTTP API";

        public const String  DefaultHTTPAPI_LoggingPath  = "default";
        public const String  DefaultHTTPAPI_LogfileName  = "HTTPAPI.log";

        #endregion

        #region Properties

        /// <summary>
        /// The external DNS name of this HTTP API.
        /// </summary>
        public String?                  ExternalDNSName          { get; }

        public String                   HTTPServerName           { get; protected set; } = DefaultHTTPServerName;

        /// <summary>
        /// The HTTP service name.
        /// </summary>
        public String                   HTTPServiceName          { get; protected set; } = DefaultHTTPServiceName;

        /// <summary>
        /// The default HTTP URL prefix.
        /// </summary>
        public HTTPPath                 URLPathPrefix            { get; }


        public HTTPPath?                BasePath                 { get; }

        /// <summary>
        /// The API version hash (git commit hash value).
        /// </summary>
        public String                   APIVersionHash           { get; }

        /// <summary>
        /// A JSON object containing all API version hashes.
        /// </summary>
        public JObject                  APIVersionHashes         { get; }


        public String                   HTMLTemplate             { get; protected set; } = String.Empty;

        public TimeSpan                 DefaultRequestTimeout    { get; protected set; } = TimeSpan.FromSeconds(30);


        /// <summary>
        /// This HTTP API runs in development mode.
        /// </summary>
        public Boolean                  IsDevelopment            { get; }

        /// <summary>
        /// The enumeration of server names which will imply to run this service in development mode.
        /// </summary>
        public HashSet<String>          DevelopmentServers       { get; } = [];

        /// <summary>
        /// Disable any logging.
        /// </summary>
        public Boolean                  DisableLogging           { get; }

        /// <summary>
        /// The path for all logfiles.
        /// </summary>
        public String                   LoggingPath              { get; }

        public String                   LoggingContext           { get; }

        public String                   LogfileName              { get; }

        public LogfileCreatorDelegate?  LogfileCreator           { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new common HTTP API Base.
        /// </summary>
        /// <param name="HTTPServiceName">An HTTP service name (HTTP Servername).</param>
        public AHTTPAPIXBase(HTTPPath?                URLPathPrefix        = null,
                             HTTPPath?                BasePath             = null,  // For URL prefixes in HTML!

                             String?                  ExternalDNSName      = null,
                             String?                  HTTPServerName       = DefaultHTTPServerName,
                             String?                  HTTPServiceName      = DefaultHTTPServiceName,
                             String?                  APIVersionHash       = null,
                             JObject?                 APIVersionHashes     = null,

                             Boolean?                 IsDevelopment        = false,
                             IEnumerable<String>?     DevelopmentServers   = null,
                             Boolean?                 DisableLogging       = false,
                             String?                  LoggingPath          = null,
                             String?                  LogfileName          = null,
                             LogfileCreatorDelegate?  LogfileCreator       = null)
        {

            this.ExternalDNSName     = ExternalDNSName;
            this.HTTPServerName      = HTTPServerName  ?? DefaultHTTPServerName;
            this.HTTPServiceName     = HTTPServiceName ?? DefaultHTTPServiceName;
            this.URLPathPrefix       = URLPathPrefix   ?? HTTPPath.Root;
            this.BasePath            = BasePath;
            this.HTMLTemplate        = GetResourceString("template.html");

            this.APIVersionHash      = APIVersionHash               ?? "";
            this.APIVersionHashes    = APIVersionHashes             ?? [];

            this.IsDevelopment       = IsDevelopment  ?? false;
            this.DevelopmentServers  = DevelopmentServers is not null
                                           ? [.. DevelopmentServers]
                                           : [];

            if (this.DevelopmentServers.Contains(Environment.MachineName))
                this.IsDevelopment = true;

            this.DisableLogging      = DisableLogging ?? false;

            this.LoggingPath         = LoggingPath    ?? Path.Combine(AppContext.BaseDirectory, DefaultHTTPAPI_LoggingPath);

            if (this.LoggingPath[^1] != Path.DirectorySeparatorChar)
                this.LoggingPath += Path.DirectorySeparatorChar;

            this.LoggingContext      = LoggingContext ?? DefaultLoggingContext;
            this.LogfileName         = LogfileName    ?? DefaultHTTPAPI_LogfileName;
            this.LogfileCreator      = LogfileCreator ?? ((loggingPath, context, logfileName) => String.Concat(loggingPath,
                                                                                                               context.IsNotNullOrEmpty() ? context + Path.DirectorySeparatorChar : String.Empty,
                                                                                                               logfileName.Replace(".log", ""), "_",
                                                                                                               DateTime.Now.Year, "-",
                                                                                                               DateTime.Now.Month.ToString("D2"),
                                                                                                               ".log"));

        }

        #endregion


        #region (protected virtual) GetResourceStream             (ResourceName, ResourceAssemblies)

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

        #region (protected virtual) GetResourceMemoryStream       (ResourceName, ResourceAssemblies)

        protected virtual MemoryStream? GetResourceMemoryStream(String ResourceName)

            => GetResourceMemoryStream(ResourceName,
                                       new Tuple<String, Assembly>(HTTPAPI.HTTPRoot, typeof(HTTPAPI).Assembly));

        protected virtual MemoryStream? GetResourceMemoryStream(String                            ResourceName,
                                                                params Tuple<String, Assembly>[]  ResourceAssemblies)
        {

            try
            {

                var resourceStream = GetResourceStream(
                                         ResourceName,
                                         ResourceAssemblies
                                     );

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

        #region (protected virtual) GetResourceString             (ResourceName, ResourceAssemblies)

        protected virtual String GetResourceString(String ResourceName)

            => GetResourceString(ResourceName,
                                 new Tuple<String, Assembly>(HTTPAPI.HTTPRoot, typeof(HTTPAPI).Assembly));

        protected virtual String GetResourceString(String                            ResourceName,
                                                   params Tuple<String, Assembly>[]  ResourceAssemblies)

            => GetResourceMemoryStream(ResourceName, ResourceAssemblies)?.ToUTF8String() ?? String.Empty;

        #endregion

        #region (protected virtual) GetResourceBytes              (ResourceName, ResourceAssemblies)

        protected virtual Byte[] GetResourceBytes(String ResourceName)

            => GetResourceBytes(ResourceName,
                                new Tuple<String, Assembly>(HTTPAPI.HTTPRoot, typeof(HTTPAPI).Assembly));

        protected virtual Byte[] GetResourceBytes(String                            ResourceName,
                                                  params Tuple<String, Assembly>[]  ResourceAssemblies)

            => GetResourceMemoryStream(ResourceName, ResourceAssemblies)?.ToArray() ?? [];

        #endregion


        #region (protected virtual) GetMergedResourceMemoryStream (ResourceName, ResourceAssemblies)

        protected virtual MemoryStream? GetMergedResourceMemoryStream(String ResourceName)

            => GetMergedResourceMemoryStream(ResourceName,
                                             new Tuple<String, Assembly>(HTTPAPI.HTTPRoot, typeof(HTTPAPI).Assembly));

        protected virtual MemoryStream? GetMergedResourceMemoryStream(String                            ResourceName,
                                                                      params Tuple<String, Assembly>[]  ResourceAssemblies)
        {

            try
            {

                var outputStream = new MemoryStream();
                var newLine      = "\r\n"u8.ToArray();

                foreach (var resourceAssembly in ResourceAssemblies)
                {
                    try
                    {

                        var data = resourceAssembly.Item2.GetManifestResourceStream(resourceAssembly.Item1 + ResourceName);
                        if (data is not null)
                        {

                            data.CopyTo(outputStream);

                            outputStream.Write(newLine, 0, newLine.Length);

                        }

                    }
                    catch
                    { }
                }

                outputStream.Seek(0, SeekOrigin.Begin);

                return outputStream;

            }
            catch
            { }

            return null;

        }

        #endregion

        #region (protected virtual) GetMergedResourceString       (ResourceName, ResourceAssemblies)

        protected virtual String GetMergedResourceString(String ResourceName)

            => GetMergedResourceString(ResourceName,
                                       new Tuple<String, Assembly>(HTTPAPI.HTTPRoot, typeof(HTTPAPI).Assembly));

        protected virtual String GetMergedResourceString(String                            ResourceName,
                                                         params Tuple<String, Assembly>[]  ResourceAssemblies)

            => GetMergedResourceMemoryStream(ResourceName, ResourceAssemblies)?.ToUTF8String() ?? String.Empty;

        #endregion

        #region (protected virtual) GetMergedResourceBytes        (ResourceName, ResourceAssemblies)

        protected virtual Byte[] GetMergedResourceBytes(String ResourceName)

            => GetMergedResourceBytes(ResourceName,
                                      new Tuple<String, Assembly>(HTTPAPI.HTTPRoot, typeof(HTTPAPI).Assembly));

        protected virtual Byte[] GetMergedResourceBytes(String                            ResourceName,
                                                        params Tuple<String, Assembly>[]  ResourceAssemblies)

            => GetMergedResourceMemoryStream(ResourceName, ResourceAssemblies)?.ToArray() ?? [];

        #endregion


        #region (protected virtual) MixWithHTMLTemplate           (ResourceName, ResourceAssemblies)

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

        #region (protected virtual) MixWithHTMLTemplate           (ResourceName, HTMLConverter, ResourceAssemblies)

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

        #region (protected virtual) MixWithHTMLTemplate           (Template, ResourceName, ResourceAssemblies)

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
