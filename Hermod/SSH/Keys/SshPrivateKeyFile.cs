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

using System.Text;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Security.AccessControl;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// Writes private-key files the way OpenSSH expects them: readable by their owner only. OpenSSH
    /// (and therefore <c>ssh</c>, <c>ssh-keygen</c>, <c>sftp</c>, …) refuses to use a private key that
    /// anybody else can read — on POSIX that means mode <c>0600</c>, on Windows a protected DACL that
    /// grants nobody but the file's owner.
    /// </summary>
    public static class SshPrivateKeyFile
    {

        #region Data

        private static readonly UTF8Encoding utf8WithoutBom = new (encoderShouldEmitUTF8Identifier: false);

        #endregion


        #region WriteAsync(Path, Text, CancellationToken = default)

        /// <summary>
        /// Write private-key text to <paramref name="Path"/>, restricting the file to its owner
        /// <i>before</i> any key material reaches the disk.
        /// </summary>
        /// <param name="Path">The path of the private-key file (created or truncated).</param>
        /// <param name="Text">The private-key text, e.g. an <c>openssh-key-v1</c> PEM block.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public static async Task WriteAsync(String             Path,
                                            String             Text,
                                            CancellationToken  CancellationToken   = default)
        {

            // Create (or truncate) the file while it is still empty, lock it down, and only then write
            // the key — an inherited ACL/umask must never apply to a file that already holds the key.
            using (new FileStream(Path, FileMode.Create, FileAccess.Write, FileShare.None)) { }

            RestrictToOwner(Path);

            await File.WriteAllTextAsync(Path, Text, utf8WithoutBom, CancellationToken).ConfigureAwait(false);

        }

        #endregion

        #region RestrictToOwner(Path)

        /// <summary>
        /// Remove every permission but the owner's from an existing file (the <c>chmod 600</c> equivalent).
        /// </summary>
        /// <param name="Path">The path of the file to restrict.</param>
        public static void RestrictToOwner(String Path)
        {

            if (OperatingSystem.IsWindows())
                RestrictToOwnerOnWindows(Path);

            else
                File.SetUnixFileMode(Path, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        }

        // A protected (non-inheriting) DACL with a single ACE for the current user: this is what
        // 'ssh-keygen' itself produces on Windows and what OpenSSH's permission check accepts.
        [SupportedOSPlatform("windows")]
        private static void RestrictToOwnerOnWindows(String Path)
        {

            using var identity = WindowsIdentity.GetCurrent();

            var user = identity.User
                           ?? throw new SshWireException("Cannot determine the current Windows user to restrict the private key to.");

            var security = new FileSecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new FileSystemAccessRule(user, FileSystemRights.FullControl, AccessControlType.Allow));

            new FileInfo(Path).SetAccessControl(security);

        }

        #endregion

    }

}
