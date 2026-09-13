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
    /// A static content bundle read from a directory on disk, e.g. the output
    /// directory of a frontend bundler during development. Files are read on
    /// every request and never cached, so a rebuild is visible immediately.
    /// </summary>
    public sealed class FileSystemContentSource : IStaticContentSource
    {

        #region Data

        private readonly String            root;
        private readonly StringComparison  pathComparison;

        #endregion

        #region Properties

        /// <summary>
        /// A human readable description for log output.
        /// </summary>
        public String   Description
            => $"directory '{root}'";

        /// <summary>
        /// Files on disk may change at any time.
        /// </summary>
        public Boolean  IsImmutable
            => false;

        /// <summary>
        /// The absolute root directory, with a trailing directory separator.
        /// </summary>
        public String   RootDirectory
            => root;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new content source reading from the given directory.
        /// </summary>
        /// <param name="RootDirectory">The bundle root directory, absolute or relative to the current directory.</param>
        public FileSystemContentSource(String RootDirectory)
        {

            var fullPath = Path.GetFullPath(RootDirectory);

            if (!Directory.Exists(fullPath))
                throw new DirectoryNotFoundException($"The static content directory '{fullPath}' does not exist!");

            // The trailing separator matters for the prefix check below:
            // without it "C:\app\dist" would also accept "C:\app\dist-secrets\...".
            root            = fullPath.EndsWith(Path.DirectorySeparatorChar)
                                  ? fullPath
                                  : fullPath + Path.DirectorySeparatorChar;

            pathComparison  = OperatingSystem.IsWindows()
                                  ? StringComparison.OrdinalIgnoreCase
                                  : StringComparison.Ordinal;

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

            var fullPath = Path.GetFullPath(
                               Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar))
                           );

            // Resolved paths must stay below the root directory.
            if (!fullPath.StartsWith(root, pathComparison))
                return false;

            if (!System.IO.File.Exists(fullPath))
                return false;

            File = StaticFile.Create(path, System.IO.File.ReadAllBytes(fullPath));

            return true;

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
