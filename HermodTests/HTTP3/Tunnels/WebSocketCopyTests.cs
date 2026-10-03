/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Hermod <https://www.github.com/Vanaheimr/Hermod>
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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP3
{

    /// <summary>
    /// The RFC 6455 framing under <c>Hermod/HTTP3/WebSocket/</c> is a copy of the one
    /// under <c>Hermod/HTTP2/WebSocket/</c>, plus <c>HTTP2/Core/IHTTP2Tunnel.cs</c>, and
    /// the README there asks for every copy to differ from its original in the
    /// namespace line alone, so that merging the two stays trivial.
    ///
    /// A request nothing checks has not held. <c>WebSocketDeflate</c> drifted in
    /// September 2026, which <c>WebSocketDeflateNegotiationTests</c> now answers in
    /// behaviour. <c>WebSocketConnection</c> drifted in October: the HTTP/2 copy got
    /// <c>Tunnel</c>, <c>UpdatePriorityAsync</c> and a sentence on how its tunnel ends,
    /// and the HTTP/3 copy none of them. This compares the files themselves, all of
    /// them, so a change that reaches one copy and not the other is red in the pull
    /// request that makes it.
    ///
    /// What is one transport's alone lives in a part of the class of its own, which
    /// has no copy: <see cref="OwnPerTransport"/>.
    ///
    /// Reads the source tree, found above the directory of the test assembly.
    /// </summary>
    [TestFixture]
    public class WebSocketCopyTests
    {

        #region Data

        /// <summary>
        /// The copies, relative to the root of the source tree.
        /// </summary>
        private static readonly String   CopyDirectory       = Path.Combine("Hermod", "HTTP3", "WebSocket");

        /// <summary>
        /// Where the originals are: a copy's is the first file of its name in these.
        /// </summary>
        private static readonly String[] OriginalDirectories = [
            Path.Combine("Hermod", "HTTP2", "WebSocket"),
            Path.Combine("Hermod", "HTTP2", "Core")         // IHTTP2Tunnel.cs
        ];

        /// <summary>
        /// Parts of the class that are one transport's alone, and so have no copy:
        /// HTTP/2's reprioritization (RFC 9218).
        /// </summary>
        private static readonly String[] OwnPerTransport = [
            "WebSocketConnection.Priority.cs"
        ];

        #endregion


        #region EveryCopy_DiffersFromItsOriginal_InTheNamespaceLineAlone()

        /// <summary>
        /// Each copy, against its original with the namespace line swapped
        /// (<c>….Hermod.HTTP2</c> → <c>….Hermod.HTTP3</c>): the rest must be the same,
        /// to the byte.
        /// </summary>
        [Test]
        public void EveryCopy_DiffersFromItsOriginal_InTheNamespaceLineAlone()
        {

            var root   = SourceTree();
            var copies = CSharpFiles(root, CopyDirectory).
                             Where (name => !OwnPerTransport.Contains(name)).
                             Select(name => (Copy: Path.Combine(root, CopyDirectory, name), Original: OriginalOf(root, name))).
                             Where (pair => pair.Original is not null).
                             ToArray();

            Assert.That(copies, Is.Not.Empty, "the copies under HTTP3/WebSocket/");

            Assert.Multiple(() => {

                foreach (var (copy, original) in copies)
                    Assert.That(Difference(root, original!, copy),
                                Is.Null,
                                "Change both copies alike, or move what is one transport's alone into a part of its own and name it in OwnPerTransport (and in HTTP3/WebSocket/README.md).");

            });

        }

        #endregion

        #region EveryFile_HasItsCounterpart_UnlessItIsOneTransportsAlone()

        /// <summary>
        /// No copy without an original, no file next to the HTTP/2 WebSocket without a
        /// copy, and no name in <see cref="OwnPerTransport"/> that is not there.
        /// </summary>
        [Test]
        public void EveryFile_HasItsCounterpart_UnlessItIsOneTransportsAlone()
        {

            var root      = SourceTree();
            var copies    = CSharpFiles(root, CopyDirectory);
            var originals = CSharpFiles(root, OriginalDirectories[0]);

            Assert.Multiple(() => {

                foreach (var name in copies.Where(name => !OwnPerTransport.Contains(name)))
                    Assert.That(OriginalOf(root, name),
                                Is.Not.Null,
                                $"HTTP3/WebSocket/{name} is a copy of nothing under HTTP2/WebSocket/ or HTTP2/Core/");

                foreach (var name in originals.Where(name => !OwnPerTransport.Contains(name)))
                    Assert.That(copies,
                                Does.Contain(name),
                                $"HTTP2/WebSocket/{name} has no copy under HTTP3/WebSocket/: copy it, or, if it is HTTP/2's alone, name it in OwnPerTransport");

                foreach (var name in OwnPerTransport)
                    Assert.That(copies.Contains(name) || originals.Contains(name),
                                Is.True,
                                $"{name} is in OwnPerTransport, but in neither directory");

            });

        }

        #endregion


        #region (private) SourceTree()

        /// <summary>
        /// The root of the source tree: the first directory above the test assembly's
        /// that has the copies in it.
        /// </summary>
        private static String SourceTree()
        {

            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, CopyDirectory)))
                directory = directory.Parent;

            Assert.That(directory,
                        Is.Not.Null,
                        $"No Hermod source tree above {AppContext.BaseDirectory} — these tests compare the source files themselves");

            return directory!.FullName;

        }

        #endregion

        #region (private) CSharpFiles(Root, RelativePath)

        /// <summary>
        /// The names of the C# files in one directory of the source tree.
        /// </summary>
        private static String[] CSharpFiles(String Root, String RelativePath)

            => Directory.GetFiles(Path.Combine(Root, RelativePath), "*.cs").
                   Select(path => Path.GetFileName(path)).
                   Order(StringComparer.Ordinal).
                   ToArray();

        #endregion

        #region (private) OriginalOf(Root, Name)

        private static String? OriginalOf(String Root, String Name)

            => OriginalDirectories.
                   Select(directory => Path.Combine(Root, directory, Name)).
                   FirstOrDefault(File.Exists);

        #endregion

        #region (private) Difference(Root, Original, Copy)

        /// <summary>
        /// Null when the copy is its original with the one namespace line swapped,
        /// otherwise where they part. Decoded as they are — a BOM stays U+FEFF, a
        /// line keeps its CR — so "the same" means the same to the byte.
        /// </summary>
        private static String? Difference(String Root, String Original, String Copy)
        {

            var original = Lines(Original);
            var copy     = Lines(Copy);
            var names    = $"{Path.GetRelativePath(Root, Copy)} against {Path.GetRelativePath(Root, Original)}";

            var namespaceLines = 0;

            for (var i = 0; i < original.Length; i++)
            {
                if (original[i].TrimStart().StartsWith("namespace ", StringComparison.Ordinal) &&
                    original[i].Contains(".Hermod.HTTP2", StringComparison.Ordinal))
                {
                    original[i] = original[i].Replace(".Hermod.HTTP2", ".Hermod.HTTP3", StringComparison.Ordinal);
                    namespaceLines++;
                }
            }

            if (namespaceLines != 1)
                return $"{names}: {namespaceLines} namespace lines in the original, where one was expected";

            // The lines both share at the start, and at the end: what is left in
            // between is where they part — one stretch, if there is one change.
            var head = 0;
            while (head < original.Length && head < copy.Length && original[head] == copy[head])
                head++;

            if (head == original.Length && head == copy.Length)
                return null;

            var tail = 0;
            while (tail < original.Length - head && tail < copy.Length - head &&
                   original[original.Length - 1 - tail] == copy[copy.Length - 1 - tail])
                tail++;

            var copyLine     = LineAt(copy,     head);
            var originalLine = LineAt(original, head);

            return $"{names}: beyond the namespace line they part in {Stretch(head, copy.Length - tail)} of the copy and {Stretch(head, original.Length - tail)} of the original, " +
                   (copyLine == originalLine
                        ? $"first \"{copyLine}\" in both, in bytes that do not show: a BOM, or a line break"
                        : $"first \"{copyLine}\" against \"{originalLine}\"");

        }

        private static String[] Lines(String FilePath)
            => Encoding.UTF8.GetString(File.ReadAllBytes(FilePath)).Split('\n');

        private static String Stretch(Int32 From, Int32 To)

            => To <= From
                   ? $"no lines (after line {From})"
                   : To - From == 1
                         ? $"line {From + 1}"
                         : $"lines {From + 1}–{To}";

        /// <summary>
        /// A line as it reads: without its indentation, its CR or a BOM.
        /// </summary>
        private static String LineAt(String[] FileLines, Int32 Index)

            => Index < FileLines.Length
                   ? FileLines[Index].Replace("﻿", "").Trim()
                   : "<end of file>";

        #endregion

    }

}
