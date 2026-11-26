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

namespace org.GraphDefined.Vanaheimr.Hermod.ACME
{

    /// <summary>
    /// How keys and certificates go into files.
    /// </summary>
    internal static class ACMEFiles
    {

        /// <summary>
        /// Write a file in one go: into a file beside it first, which then
        /// replaces it - so that whoever reads it at the same time reads the
        /// old content or the new one, and never half of the new one.
        /// </summary>
        public static void Write(String   Path,
                                 String   Text,
                                 Boolean  Private   = false)
        {

            var directory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path));

            if (directory is not null)
                Directory.CreateDirectory(directory);

            var temporary = $"{Path}.{Guid.NewGuid():N}.tmp";

            try
            {

                File.WriteAllText(temporary, Text);

                // Before the key is where it is looked for, not after: in between,
                // anybody on the machine could read it.
                if (Private && !OperatingSystem.IsWindows())
                    File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);

                File.Move(temporary, Path, overwrite: true);

            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }

        }

        /// <summary>
        /// Write a private key: as <see cref="Write"/>, readable by its owner only
        /// where the file system knows owners.
        /// </summary>
        public static void WritePrivate(String  Path,
                                        String  Text)

            => Write(Path, Text, Private: true);

    }

}
