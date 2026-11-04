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

using System.Buffers;
using System.Globalization;
using System.IO.Pipelines;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SSH;
using org.GraphDefined.Vanaheimr.Hermod.SSH.SFTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// The longname of a listing: the line OpenSSH's sftp prints for <c>ls -l</c>, as the server sends it.
    ///
    /// <para>
    /// It used to be "-rw-r--r-- 1 owner group &lt;size&gt; Jan  1 00:00" whatever the attributes said, so a
    /// read-only log file showed as writable and every file as written on the first of January, while
    /// <c>ls -ln</c>, which sftp builds from the attributes, showed both right.
    /// </para>
    /// </summary>
    [TestFixture]
    public class SftpLongNameTests
    {

        #region Data

        private static readonly DateTimeOffset Now = new (2026, 11, 4, 9, 30, 0, TimeSpan.Zero);

        private static readonly TimeZoneInfo TwoHoursAhead = TimeZoneInfo.CreateCustomTimeZone("UTC+2", TimeSpan.FromHours(2), "UTC+2", "UTC+2");

        private static String LongName(String Name, SftpFileAttributes Attributes, TimeZoneInfo? Zone = null)
            => SftpServer.LongName(new SftpDirectoryEntry(Name, Attributes), Now, Zone ?? TimeZoneInfo.Utc);

        #endregion


        #region AReadOnlyFileIsListedWithItsBitsOwnerAndTime

        /// <summary>
        /// A log file nobody may write, as a node lists it: its bits, its owner and group by number, its
        /// size and the minute it was written.
        /// </summary>
        [Test]
        public void AReadOnlyFileIsListedWithItsBitsOwnerAndTime()
        {

            var attributes = SftpFileAttributes.File(1234, 0x8124 /* 0100444 */) with {
                                 UserId     = 1000,
                                 GroupId    = 1000,
                                 ModifyTime = new DateTimeOffset(2026, 11, 3, 16, 10, 22, TimeSpan.Zero)
                             };

            Assert.That(LongName("ev-2026-11-03.log", attributes),
                        Is.EqualTo("-r--r--r--    1 1000     1000         1234 Nov  3 16:10 ev-2026-11-03.log"));

        }

        #endregion

        #region ATimeOutsideTheLastHalfYearShowsItsYear

        /// <summary>
        /// As <c>ls</c> writes it: the hour and minute within half a year before now, the year before that,
        /// and the year for a time after now. Shown in the zone the server is in, whatever the offset the
        /// time came with.
        /// </summary>
        [Test]
        public void ATimeOutsideTheLastHalfYearShowsItsYear()
        {

            String TimeOf(DateTimeOffset Modified, TimeZoneInfo? Zone = null)
                => LongName("f", SftpFileAttributes.File(0) with { ModifyTime = Modified }, Zone)[^14..^2];

            Assert.Multiple(() => {
                Assert.That(TimeOf(Now),                                                    Is.EqualTo("Nov  4 09:30"), "now");
                Assert.That(TimeOf(Now.AddDays(-182)),                                      Is.EqualTo("May  6 09:30"), "182 days ago");
                Assert.That(TimeOf(Now.AddDays(-183)),                                      Is.EqualTo("May  5  2026"), "183 days ago");
                Assert.That(TimeOf(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero)), Is.EqualTo("Jan  1  2025"), "last year");
                Assert.That(TimeOf(Now.AddMinutes(1)),                                      Is.EqualTo("Nov  4  2026"), "a minute ahead");
                Assert.That(TimeOf(new DateTimeOffset(2026, 11, 4, 10, 15, 0, TimeSpan.FromHours(1))),
                                                                                            Is.EqualTo("Nov  4 09:15"), "+01:00, shown in UTC");
                Assert.That(TimeOf(new DateTimeOffset(2026, 11, 3, 23, 15, 0, TimeSpan.Zero), TwoHoursAhead),
                                                                                            Is.EqualTo("Nov  4 01:15"), "UTC, shown two hours ahead");
            });

        }

        #endregion

        #region TheTypeAndTheSpecialBitsAreShownAsLsShowsThem

        /// <summary>
        /// The type letter, and setuid, setgid and sticky in the execute places: lower case where execute is
        /// set as well, upper case where it is not.
        /// </summary>
        [Test]
        public void TheTypeAndTheSpecialBitsAreShownAsLsShowsThem()
        {

            String ModeOf(UInt32 Permissions)
                => LongName("f", new SftpFileAttributes { Permissions = Permissions })[..10];

            Assert.Multiple(() => {
                Assert.That(ModeOf(0x81A4 /* 0100644 */), Is.EqualTo("-rw-r--r--"));
                Assert.That(ModeOf(0x41ED /*  040755 */), Is.EqualTo("drwxr-xr-x"));
                Assert.That(ModeOf(0x416D /*  040555 */), Is.EqualTo("dr-xr-xr-x"));
                Assert.That(ModeOf(0xA1FF /* 0120777 */), Is.EqualTo("lrwxrwxrwx"));
                Assert.That(ModeOf(0x21B6 /*  020666 */), Is.EqualTo("crw-rw-rw-"));
                Assert.That(ModeOf(0x61B0 /*  060660 */), Is.EqualTo("brw-rw----"));
                Assert.That(ModeOf(0x11A4 /*  010644 */), Is.EqualTo("prw-r--r--"));
                Assert.That(ModeOf(0xC1FF /* 0140777 */), Is.EqualTo("srwxrwxrwx"));
                Assert.That(ModeOf(0x89ED /* 0104755 */), Is.EqualTo("-rwsr-xr-x"), "setuid");
                Assert.That(ModeOf(0x89A4 /* 0104644 */), Is.EqualTo("-rwSr--r--"), "setuid without execute");
                Assert.That(ModeOf(0x85ED /* 0102755 */), Is.EqualTo("-rwxr-sr-x"), "setgid");
                Assert.That(ModeOf(0x85A4 /* 0102644 */), Is.EqualTo("-rw-r-Sr--"), "setgid without execute");
                Assert.That(ModeOf(0x43FF /*  041777 */), Is.EqualTo("drwxrwxrwt"), "sticky");
                Assert.That(ModeOf(0x43FE /*  041776 */), Is.EqualTo("drwxrwxrwT"), "sticky without execute");
                Assert.That(ModeOf(0x01A4 /*    0644 */), Is.EqualTo("?rw-r--r--"), "no type");
            });

        }

        #endregion

        #region WhatTheAttributesLeaveOutIsAQuestionMark

        /// <summary>
        /// Attributes that say nothing make a line that says nothing, rather than one that makes it up; and
        /// an access time alone stands in for the modification time, as it does on the wire.
        /// </summary>
        [Test]
        public void WhatTheAttributesLeaveOutIsAQuestionMark()
        {

            Assert.Multiple(() => {

                Assert.That(LongName("x", new SftpFileAttributes()),
                            Is.EqualTo("??????????    1 ?        ?               ?            ? x"));

                Assert.That(LongName("x", new SftpFileAttributes { AccessTime = new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.Zero) }),
                            Does.EndWith(" Nov  1 08:00 x"));

            });

        }

        #endregion

        #region AListingSendsEachEntrysLongName

        /// <summary>
        /// Over the wire: READDIR sends each entry's line of <c>ls -l</c>, and REALPATH sends the path itself
        /// as its longname, as OpenSSH's server does. The server shows times in its own zone; noon UTC is the
        /// third of February in every zone but the few twelve hours or more ahead.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public async Task AListingSendsEachEntrysLongName(CancellationToken CancellationToken)
        {

            var anHourAgo  = DateTimeOffset.UtcNow.AddHours(-1);
            var shownAt    = TimeZoneInfo.ConvertTime(anHourAgo, TimeZoneInfo.Local).DateTime;

            var fileSystem = new Listing(new SftpDirectoryEntry("logs",      SftpFileAttributes.Directory(0x416D /* 040555 */)),
                                         new SftpDirectoryEntry("ev.log",    SftpFileAttributes.File(5, 0x8124 /* 0100444 */) with {
                                                                                 ModifyTime = new DateTimeOffset(2001, 2, 3, 12, 0, 0, TimeSpan.Zero)
                                                                             }),
                                         new SftpDirectoryEntry("today.log", SftpFileAttributes.File(7) with { ModifyTime = anHourAgo }));

            var (client, server) = PipeDuplex.CreatePair();

            var serving = Task.Run(async () => {
                try { await SftpServer.ServeAsync(server, fileSystem, CancellationToken: CancellationToken); }
                catch { /* torn down with the test */ }
            }, CancellationToken);

            await SendAsync(client, (ref SshPacketWriter w) => { w.WriteByte((Byte) SftpPacketType.Init); w.WriteUInt32(SftpVersion.Three); }, CancellationToken);
            await SftpServer.ReadPacketAsync(client, CancellationToken);

            await SendAsync(client, (ref SshPacketWriter w) => { w.WriteByte((Byte) SftpPacketType.RealPath); w.WriteUInt32(1); w.WriteString("."); }, CancellationToken);
            var realPath = Names(await SftpServer.ReadPacketAsync(client, CancellationToken));

            await SendAsync(client, (ref SshPacketWriter w) => { w.WriteByte((Byte) SftpPacketType.OpenDir); w.WriteUInt32(2); w.WriteString("/"); }, CancellationToken);
            var handle   = Handle(await SftpServer.ReadPacketAsync(client, CancellationToken));

            await SendAsync(client, (ref SshPacketWriter w) => { w.WriteByte((Byte) SftpPacketType.ReadDir); w.WriteUInt32(3); w.WriteString(handle); }, CancellationToken);
            var listing  = Names(await SftpServer.ReadPacketAsync(client, CancellationToken));

            await client.CloseAsync(CancellationToken);
            await serving;

            Assert.Multiple(() => {
                Assert.That(realPath,                 Is.EqualTo(new[] { ("/", "/") }),                                                         "REALPATH");
                Assert.That(listing.Select(e => e.Name), Is.EqualTo(new[] { "logs", "ev.log", "today.log" }));
                Assert.That(listing[0].LongName,     Does.StartWith("dr-xr-xr-x    1 "),                                                    "the directory");
                Assert.That(listing[1].LongName,     Is.EqualTo("-r--r--r--    1 ?        ?               5 Feb  3  2001 ev.log"),         "the file");
                Assert.That(listing[2].LongName,     Does.EndWith(shownAt.ToString("MMM ", CultureInfo.InvariantCulture) +
                                                                  shownAt.Day.ToString(CultureInfo.InvariantCulture).PadLeft(2) +
                                                                  shownAt.ToString(" HH:mm", CultureInfo.InvariantCulture) + " today.log"), "an hour ago, in the server's zone");
            });

        }

        #endregion


        #region (private) a file system that lists, and a raw client

        /// <summary>
        /// A directory of the given entries, wherever it is opened. Nothing else is asked of it.
        /// </summary>
        private sealed class Listing(params SftpDirectoryEntry[] Entries) : ISftpFileSystem
        {

            private Boolean listed;

            public ValueTask<String> RealPathAsync(String Path, CancellationToken CancellationToken = default)
                => ValueTask.FromResult("/");

            public ValueTask<String> OpenDirectoryAsync(String Path, CancellationToken CancellationToken = default)
            {
                listed = false;
                return ValueTask.FromResult("dir");
            }

            public ValueTask<IReadOnlyList<SftpDirectoryEntry>> ReadDirectoryAsync(String Handle, CancellationToken CancellationToken = default)
            {
                IReadOnlyList<SftpDirectoryEntry> batch = listed ? [] : Entries;
                listed = true;
                return ValueTask.FromResult(batch);
            }

            public ValueTask CloseAsync(String Handle, CancellationToken CancellationToken = default)
                => ValueTask.CompletedTask;

            public ValueTask<String>             OpenAsync           (String Path,    SftpOpenFlags Flags,                       CancellationToken CancellationToken = default) => throw new NotSupportedException();
            public ValueTask<Byte[]>             ReadAsync           (String Handle,  Int64 Offset, Int32 Length,                CancellationToken CancellationToken = default) => throw new NotSupportedException();
            public ValueTask                     WriteAsync          (String Handle,  Int64 Offset, ReadOnlyMemory<Byte> Data,   CancellationToken CancellationToken = default) => throw new NotSupportedException();
            public ValueTask<SftpFileAttributes> StatAsync           (String Path,                                               CancellationToken CancellationToken = default) => throw new NotSupportedException();
            public ValueTask                     MakeDirectoryAsync  (String Path,                                               CancellationToken CancellationToken = default) => throw new NotSupportedException();
            public ValueTask                     RemoveAsync         (String Path,                                               CancellationToken CancellationToken = default) => throw new NotSupportedException();
            public ValueTask                     RemoveDirectoryAsync(String Path,                                               CancellationToken CancellationToken = default) => throw new NotSupportedException();
            public ValueTask                     RenameAsync         (String OldPath, String NewPath,                            CancellationToken CancellationToken = default) => throw new NotSupportedException();

        }

        private delegate void Body(ref SshPacketWriter Writer);

        private static ValueTask SendAsync(ISftpDuplex Channel, Body Write, CancellationToken CancellationToken)
        {
            var abw = new ArrayBufferWriter<Byte>(); var w = new SshPacketWriter(abw);
            Write(ref w);
            return SftpServer.SendAsync(Channel, abw.WrittenMemory, CancellationToken);
        }

        private static String Handle(Byte[]? Packet)
        {
            var reader = new SshPacketReader(Packet);
            Assert.That((SftpPacketType) reader.ReadByte(), Is.EqualTo(SftpPacketType.Handle));
            reader.ReadUInt32();
            return reader.ReadString();
        }

        private static List<(String Name, String LongName)> Names(Byte[]? Packet)
        {

            var reader = new SshPacketReader(Packet);
            Assert.That((SftpPacketType) reader.ReadByte(), Is.EqualTo(SftpPacketType.Name));
            reader.ReadUInt32();

            var count  = reader.ReadUInt32();
            var names  = new List<(String, String)>();

            for (var i = 0U; i < count; i++)
            {
                var name     = reader.ReadString();
                var longName = reader.ReadString();
                SftpFileAttributes.Decode(ref reader);
                names.Add((name, longName));
            }

            return names;

        }

        private sealed class PipeDuplex : ISftpDuplex
        {

            private readonly PipeReader reader;
            private readonly PipeWriter writer;

            private PipeDuplex(PipeReader Reader, PipeWriter Writer)
            {
                this.reader = Reader;
                this.writer = Writer;
            }

            public static (ISftpDuplex Client, ISftpDuplex Server) CreatePair()
            {
                var clientToServer = new Pipe();
                var serverToClient = new Pipe();
                return (new PipeDuplex(serverToClient.Reader, clientToServer.Writer),
                        new PipeDuplex(clientToServer.Reader, serverToClient.Writer));
            }

            public async ValueTask<Byte[]?> TryReadExactAsync(Int32 Count, CancellationToken CancellationToken = default)
            {

                var buffer = new Byte[Count];
                var filled = 0;

                while (filled < Count)
                {

                    var result = await reader.ReadAsync(CancellationToken).ConfigureAwait(false);

                    if (result.Buffer.IsEmpty && result.IsCompleted)
                    {
                        reader.AdvanceTo(result.Buffer.Start);
                        return filled == 0 ? null : throw new EndOfStreamException();
                    }

                    var take = (Int32) Math.Min(Count - filled, result.Buffer.Length);
                    result.Buffer.Slice(0, take).CopyTo(buffer.AsSpan(filled));
                    filled += take;
                    reader.AdvanceTo(result.Buffer.GetPosition(take));

                }

                return buffer;

            }

            public async ValueTask<Byte[]> ReadExactAsync(Int32 Count, CancellationToken CancellationToken = default)
                => await TryReadExactAsync(Count, CancellationToken).ConfigureAwait(false)
                       ?? throw new EndOfStreamException();

            public async ValueTask SendAsync(ReadOnlyMemory<Byte> Data, CancellationToken CancellationToken = default)
                => await writer.WriteAsync(Data, CancellationToken).ConfigureAwait(false);

            public ValueTask CloseAsync(CancellationToken CancellationToken = default)
            {
                writer.Complete();
                return ValueTask.CompletedTask;
            }

        }

        #endregion

    }

}
