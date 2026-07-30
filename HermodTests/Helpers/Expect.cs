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

namespace org.GraphDefined.Vanaheimr.Hermod.Tests;

/// <summary>
/// Small NUnit test helpers that CHECK a condition and RETURN the checked value — handy when
/// parsing (confirm an element's type and use it right away). Internally via
/// <see cref="Assert.That(object, NUnit.Framework.Constraints.IResolveConstraint)"/>, i.e. NUnit-native.
/// </summary>
internal static class Expect
{
    /// <summary>
    /// Confirms that <paramref name="value"/> is of type <typeparamref name="T"/> and returns it
    /// typed.
    /// </summary>
    public static T Type<T>(object? value)
    {
        Assert.That(value, Is.TypeOf<T>());
        return (T)value!;
    }

    /// <summary>
    /// Confirms that <paramref name="items"/> contains exactly one element and returns it.
    /// </summary>
    public static T Single<T>(IEnumerable<T> items)
    {
        var list = items.ToList();
        Assert.That(list, Has.Count.EqualTo(1));
        return list[0];
    }
}
