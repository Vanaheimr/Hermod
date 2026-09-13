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

using System.Diagnostics.CodeAnalysis;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Where a static content bundle comes from: the manifest resources of an
    /// assembly (deployment) or a directory on disk (development).
    /// </summary>
    public interface IStaticContentSource
    {

        /// <summary>
        /// A human readable description for log output.
        /// </summary>
        String   Description    { get; }

        /// <summary>
        /// Whether the files can never change while the process runs.
        /// Only then may hashed assets be cached long-term by clients.
        /// </summary>
        Boolean  IsImmutable    { get; }

        /// <summary>
        /// Try to get the file at the given path below the bundle root.
        /// </summary>
        /// <param name="RelativePath">A slash-separated path, e.g. "assets/app.3f9c1a.js" or "index.html".</param>
        /// <param name="File">The file, when it exists.</param>
        Boolean  TryGet(String                               RelativePath,
                        [NotNullWhen(true)] out StaticFile?  File);

    }


    /// <summary>
    /// Path rules shared by all static content sources.
    /// </summary>
    public static class StaticPath
    {

        #region (static) TryNormalize(Path, out NormalizedPath)

        /// <summary>
        /// Normalize a request path into a bundle-relative path and reject
        /// everything that could escape the bundle root or is otherwise not a
        /// plain file path. The HTTP server already rejects dot segments and
        /// encoded separators on the wire; this is the second line of defence.
        /// </summary>
        /// <param name="Path">The raw path, e.g. "/assets/app.js" or "assets/app.js".</param>
        /// <param name="NormalizedPath">The normalized path without leading or trailing slashes.</param>
        public static Boolean TryNormalize(String                           Path,
                                           [NotNullWhen(true)] out String?  NormalizedPath)
        {

            NormalizedPath = null;

            var path = Path.Trim('/');

            if (path.Length == 0 || path.Length > 1024)
                return false;

            if (path.Contains('\\') || path.Contains('\0') || path.Contains("//", StringComparison.Ordinal))
                return false;

            foreach (var segment in path.Split('/'))
            {
                if (segment.Length == 0 || segment == "." || segment == "..")
                    return false;
            }

            NormalizedPath = path;
            return true;

        }

        #endregion

    }

}
