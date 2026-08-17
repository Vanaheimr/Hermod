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
    /// SFTP protocol message types (draft-ietf-secsh-filexfer-02, version 3).
    /// </summary>
    public enum SftpPacketType : Byte
    {
        Init      =   1,
        Version   =   2,
        Open      =   3,
        Close     =   4,
        Read      =   5,
        Write     =   6,
        LStat     =   7,
        FStat     =   8,
        SetStat   =   9,
        FSetStat  =  10,
        OpenDir   =  11,
        ReadDir   =  12,
        Remove    =  13,
        MkDir     =  14,
        RmDir     =  15,
        RealPath  =  16,
        Stat      =  17,
        Rename    =  18,
        ReadLink  =  19,
        Symlink   =  20,
        Status    = 101,
        Handle    = 102,
        Data      = 103,
        Name      = 104,
        Attrs     = 105,
        Extended       = 200,
        ExtendedReply  = 201
    }


    /// <summary>
    /// SFTP status codes (SSH_FX_*).
    /// </summary>
    public enum SftpStatusCode : UInt32
    {
        Ok               = 0,
        Eof              = 1,
        NoSuchFile       = 2,
        PermissionDenied = 3,
        Failure          = 4,
        BadMessage       = 5,
        NoConnection     = 6,
        ConnectionLost   = 7,
        OpUnsupported    = 8
    }


    /// <summary>
    /// SFTP open flags (SSH_FXF_*).
    /// </summary>
    [Flags]
    public enum SftpOpenFlags : UInt32
    {
        Read      = 0x00000001,
        Write     = 0x00000002,
        Append    = 0x00000004,
        Create    = 0x00000008,
        Truncate  = 0x00000010,
        Exclusive = 0x00000020
    }


    /// <summary>
    /// The SFTP protocol version this implementation speaks.
    /// </summary>
    public static class SftpVersion
    {
        /// <summary>
        /// Version 3 — the de-facto standard.
        /// </summary>
        public const UInt32 Three = 3;
    }


    /// <summary>
    /// SFTP file attributes (the SSH_FXP ATTRS structure). Only the fields present (per the flags bitmask)
    /// are meaningful; a directory is indicated through the POSIX file-type bits of <see cref="Permissions"/>.
    /// </summary>
    public sealed record SftpFileAttributes
    {

        private const UInt32 FlagSize        = 0x00000001;
        private const UInt32 FlagPermissions = 0x00000004;
        private const UInt32 FlagAcModTime   = 0x00000008;

        private const UInt32 ModeDirectory   = 0x4000;   // S_IFDIR >> 4? no — see IsDirectory
        private const UInt32 IfmtDirectory   = 0x4000;   // S_IFDIR (0040000 octal) high bits

        /// <summary>
        /// The file size in bytes, if known.
        /// </summary>
        public Int64?           Size          { get; init; }

        /// <summary>
        /// The POSIX permission/type bits, if known.
        /// </summary>
        public UInt32?          Permissions   { get; init; }

        /// <summary>
        /// The last-modified time, if known.
        /// </summary>
        public DateTimeOffset?  ModifyTime    { get; init; }

        /// <summary>
        /// Whether the permission bits mark this as a directory.
        /// </summary>
        public Boolean IsDirectory => Permissions is { } p && (p & 0xF000) == 0x4000;


        /// <summary>
        /// Attributes for a regular file of the given size.
        /// </summary>
        public static SftpFileAttributes File(Int64 Size, UInt32 Mode = 0x81A4 /* 0100644 */)
            => new () { Size = Size, Permissions = Mode };

        /// <summary>
        /// Attributes for a directory.
        /// </summary>
        public static SftpFileAttributes Directory(UInt32 Mode = 0x41ED /* 040755 */)
            => new () { Permissions = Mode };


        /// <summary>
        /// Encode the ATTRS structure.
        /// </summary>
        public void Encode(ref SshPacketWriter Writer)
        {

            UInt32 flags = 0;
            if (Size        is not null) flags |= FlagSize;
            if (Permissions is not null) flags |= FlagPermissions;
            if (ModifyTime  is not null) flags |= FlagAcModTime;

            Writer.WriteUInt32(flags);

            if (Size        is { } size)  Writer.WriteUInt64((UInt64) size);
            if (Permissions is { } perm)  Writer.WriteUInt32(perm);
            if (ModifyTime  is { } mtime) { Writer.WriteUInt32((UInt32) mtime.ToUnixTimeSeconds()); Writer.WriteUInt32((UInt32) mtime.ToUnixTimeSeconds()); }

        }

        /// <summary>
        /// Decode an ATTRS structure.
        /// </summary>
        public static SftpFileAttributes Decode(ref SshPacketReader Reader)
        {

            var flags        = Reader.ReadUInt32();
            Int64?  size     = null;
            UInt32? perms    = null;
            DateTimeOffset? mtime = null;

            if ((flags & FlagSize)        != 0) size  = (Int64) Reader.ReadUInt64();
            if ((flags & 0x00000002)      != 0) { Reader.ReadUInt32(); Reader.ReadUInt32(); }   // uid, gid
            if ((flags & FlagPermissions) != 0) perms = Reader.ReadUInt32();
            if ((flags & FlagAcModTime)   != 0) { Reader.ReadUInt32(); mtime = DateTimeOffset.FromUnixTimeSeconds(Reader.ReadUInt32()); }

            return new SftpFileAttributes { Size = size, Permissions = perms, ModifyTime = mtime };

        }

    }

}
