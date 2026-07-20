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

using System.Collections.Concurrent;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP.Server;

/// <summary>
/// Simple sliding window counter for rate limiting
/// </summary>
public sealed class SlidingWindow
{

    private readonly TimeSpan                         _windowSize;
    private readonly ConcurrentQueue<DateTimeOffset>  _timestamps   = new();
    private          DateTimeOffset                   _lastAccess   = Timestamp.Now;

    public SlidingWindow(TimeSpan windowSize)
    {
        _windowSize = windowSize;
    }

    public int Count
    {
        get
        {
            Prune();
            return _timestamps.Count;
        }
    }

    public void Increment()
    {
        Prune();
        _timestamps.Enqueue(Timestamp.Now);
        _lastAccess = Timestamp.Now;
    }

    public Boolean IsExpired(DateTimeOffset now)
    {
        return now - _lastAccess > _windowSize * 2;
    }

    private void Prune()
    {

        var cutoff = Timestamp.Now - _windowSize;

        while (_timestamps.TryPeek(out var timestamp) && timestamp < cutoff)
        {
            _timestamps.TryDequeue(out _);
        }

    }

}
