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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP3.Qpack;

/// <summary>
/// Access to the QPACK static table (RFC 9204, appendix A) with lookup indices.
/// </summary>
internal static partial class QpackStaticTable
{
    public static int Count => Entries.Length;

    public static (string Name, string Value) Get(int index) => Entries[index];

    // name -> smallest index; (name,value) -> index. Lazy, since the initialisation order of
    // fields across partial-class files is not guaranteed (Entries vs. index).
    private static Dictionary<string, int>? _nameIndex;
    private static Dictionary<(string, string), int>? _pairIndex;
    private static Dictionary<string, int> NameIndex => _nameIndex ??= BuildNameIndex();
    private static Dictionary<(string, string), int> PairIndex => _pairIndex ??= BuildPairIndex();

    private static Dictionary<string, int> BuildNameIndex()
    {
        var map = new Dictionary<string, int>(Entries.Length);
        for (int i = 0; i < Entries.Length; i++)
            map.TryAdd(Entries[i].Name, i); // the first (lowest) index wins
        return map;
    }

    private static Dictionary<(string, string), int> BuildPairIndex()
    {
        var map = new Dictionary<(string, string), int>(Entries.Length);
        for (int i = 0; i < Entries.Length; i++)
            map.TryAdd(Entries[i], i);
        return map;
    }

    /// <summary>
    /// Finds the index of an exact (name, value) pair, if present.
    /// </summary>
    public static bool TryGetPairIndex(string name, string value, out int index)
        => PairIndex.TryGetValue((name, value), out index);

    /// <summary>
    /// Finds the index of an entry with a matching name (any value), if present.
    /// </summary>
    public static bool TryGetNameIndex(string name, out int index)
        => NameIndex.TryGetValue(name, out index);
}
