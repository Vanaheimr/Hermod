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

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.SFTP
{

    /// <summary>
    /// File-system statistics as returned by the <c>statvfs@openssh.com</c> extension. When a session quota
    /// is in force the server reports it here as total/free space, so a client can see how much it may still
    /// transfer.
    /// </summary>
    public readonly record struct SftpFileSystemStats(UInt64  BlockSize,
                                                      UInt64  FundamentalBlockSize,
                                                      UInt64  TotalBlocks,
                                                      UInt64  FreeBlocks,
                                                      UInt64  AvailableBlocks,
                                                      UInt64  TotalFiles,
                                                      UInt64  FreeFiles,
                                                      UInt64  AvailableFiles,
                                                      UInt64  FileSystemId,
                                                      UInt64  Flags,
                                                      UInt64  MaxNameLength)
    {

        /// <summary>The total capacity in bytes.</summary>
        public UInt64 TotalBytes      => TotalBlocks     * FundamentalBlockSize;

        /// <summary>The space available to the client in bytes.</summary>
        public UInt64 AvailableBytes  => AvailableBlocks * FundamentalBlockSize;

    }


    /// <summary>The server's protocol limits as returned by the <c>limits@openssh.com</c> extension.</summary>
    /// <param name="MaxPacketLength">The largest SFTP packet the server accepts.</param>
    /// <param name="MaxReadLength">The largest READ length the server will honour in one request.</param>
    /// <param name="MaxWriteLength">The largest WRITE payload the server will accept in one request.</param>
    /// <param name="MaxOpenHandles">The maximum number of open handles (0 = no limit).</param>
    public readonly record struct SftpProtocolLimits(UInt64 MaxPacketLength,
                                                     UInt64 MaxReadLength,
                                                     UInt64 MaxWriteLength,
                                                     UInt64 MaxOpenHandles);

}
