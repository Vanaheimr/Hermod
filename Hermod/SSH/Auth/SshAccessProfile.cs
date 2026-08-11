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

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>The individual SFTP operations an access profile may permit (least-privilege gating).</summary>
    [Flags]
    public enum SftpPermissions : UInt32
    {
        /// <summary>Nothing is allowed.</summary>
        None           = 0,
        /// <summary>Read file contents.</summary>
        Read           = 1 << 0,
        /// <summary>Write to existing files.</summary>
        Write          = 1 << 1,
        /// <summary>Create new files (upload).</summary>
        Create         = 1 << 2,
        /// <summary>List directories.</summary>
        List           = 1 << 3,
        /// <summary>Remove files and directories.</summary>
        Delete         = 1 << 4,
        /// <summary>Create directories.</summary>
        MakeDirectory  = 1 << 5,
        /// <summary>Rename files and directories.</summary>
        Rename         = 1 << 6,
        /// <summary>Query attributes (stat) — usually needed by any client.</summary>
        Stat           = 1 << 7,

        /// <summary>Everything.</summary>
        All            = Read | Write | Create | List | Delete | MakeDirectory | Rename | Stat
    }


    /// <summary>
    /// A least-privilege authorization profile attached to an authenticated session. For SFTP it gates the
    /// individual operations, enabling ready-made presets such as upload-only (log collection) and
    /// download-only (firmware distribution) — the most restrictive source always wins.
    /// </summary>
    public sealed record SshAccessProfile
    {

        /// <summary>The SFTP operations this profile permits.</summary>
        public SftpPermissions Sftp { get; init; } = SftpPermissions.All;

        /// <summary>The port-forwarding policy for this session (defaults to off).</summary>
        public ForwardingPolicy PortForwarding { get; init; } = ForwardingPolicy.None;


        /// <summary>Full SFTP access.</summary>
        public static SshAccessProfile FullSftp
            => new () { Sftp = SftpPermissions.All };

        /// <summary>
        /// Upload-only: create and write files (and stat) — no reading, listing or deleting. The log-upload
        /// use case: a device may drop a log file but cannot read anything back.
        /// </summary>
        public static SshAccessProfile SftpUploadOnly
            => new () { Sftp = SftpPermissions.Create | SftpPermissions.Write | SftpPermissions.Stat | SftpPermissions.MakeDirectory };

        /// <summary>
        /// Download-only: read and list (and stat) — no writing, creating or deleting. The firmware-download
        /// use case: a device may fetch an image but cannot alter the store.
        /// </summary>
        public static SshAccessProfile SftpDownloadOnly
            => new () { Sftp = SftpPermissions.Read | SftpPermissions.List | SftpPermissions.Stat };


        /// <summary>Whether all of the given SFTP permissions are granted.</summary>
        public Boolean AllowsSftp(SftpPermissions Required)
            => (Sftp & Required) == Required;

    }

}
