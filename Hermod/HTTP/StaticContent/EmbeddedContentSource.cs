/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
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
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A static content bundle embedded into an assembly as manifest resources.
    /// The URL path "assets/app.3f9c1a.js" maps onto the resource name
    /// "&lt;prefix&gt;assets.app.3f9c1a.js", which is what MSBuild produces
    /// for an EmbeddedResource with a matching LogicalName (or by default,
    /// when the directory names are plain identifiers). Directory names must
    /// therefore not contain dots.
    /// </summary>
    public sealed class EmbeddedContentSource : IStaticContentSource
    {

        #region Data

        private readonly Assembly                                  assembly;
        private readonly String                                    prefix;
        private readonly HashSet<String>                           resourceNames;
        private readonly ConcurrentDictionary<String, StaticFile>  cache = new(StringComparer.Ordinal);

        #endregion

        #region Properties

        /// <summary>
        /// A human readable description for log output.
        /// </summary>
        public String   Description
            => $"{resourceNames.Count} embedded resources '{prefix}*' of assembly '{assembly.GetName().Name}'";

        /// <summary>
        /// Embedded resources never change while the process runs.
        /// </summary>
        public Boolean  IsImmutable
            => true;

        /// <summary>
        /// The number of embedded files.
        /// </summary>
        public Int32    Count
            => resourceNames.Count;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new content source reading manifest resources.
        /// </summary>
        /// <param name="ResourcePrefix">The common prefix of all bundle resources, e.g. "com.example.Website.HTTPRoot.".</param>
        /// <param name="Assembly">The assembly containing the resources.</param>
        public EmbeddedContentSource(String    ResourcePrefix,
                                     Assembly  Assembly)
        {

            prefix         = ResourcePrefix;
            assembly       = Assembly;

            resourceNames  = [.. Assembly.GetManifestResourceNames().
                                          Where(name => name.StartsWith(prefix, StringComparison.Ordinal))];

        }

        #endregion


        #region TryGet(RelativePath, out File)

        /// <summary>
        /// Try to get the file at the given path below the bundle root.
        /// </summary>
        /// <param name="RelativePath">A slash-separated path.</param>
        /// <param name="File">The file, when it exists.</param>
        public Boolean TryGet(String                               RelativePath,
                              [NotNullWhen(true)] out StaticFile?  File)
        {

            File = null;

            if (!StaticPath.TryNormalize(RelativePath, out var path))
                return false;

            var resourceName = prefix + path.Replace('/', '.');

            if (!resourceNames.Contains(resourceName))
                return false;

            File = cache.GetOrAdd(path, _ => Load(path, resourceName));
            return true;

        }

        #endregion

        #region (private) Load(RelativePath, ResourceName)

        private StaticFile Load(String  RelativePath,
                                String  ResourceName)
        {

            using var stream  = assembly.GetManifestResourceStream(ResourceName)
                                    ?? throw new FileNotFoundException($"The embedded resource '{ResourceName}' vanished!");

            using var memory  = new MemoryStream();
            stream.CopyTo(memory);

            return StaticFile.Create(RelativePath, memory.ToArray());

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()
            => Description;

        #endregion

    }

}
