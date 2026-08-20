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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Rendezvous
{

    /// <summary>
    /// Remembers the nonces of recently accepted control requests.
    ///
    /// A signature proves who sent a request, but not that it is fresh: without
    /// this cache anybody who captured a signed request could send it again and
    /// again. A nonce may be used once, and only requests whose timestamp is
    /// within the accepted clock skew are considered at all - which is what
    /// bounds the size of this cache.
    /// </summary>
    public sealed class NonceCache
    {

        #region Data

        private readonly ConcurrentDictionary<String, DateTimeOffset>  nonces = [];

        private          Int64                                         lastCleanupTicks;

        #endregion

        #region Properties

        /// <summary>
        /// The number of remembered nonces.
        /// </summary>
        public Int32 Count
            => nonces.Count;

        #endregion


        #region TryUse(Nonce, Timestamp, Window)

        /// <summary>
        /// Try to use the given nonce: returns false when it was used before.
        /// </summary>
        /// <param name="Nonce">The nonce of a control request.</param>
        /// <param name="Timestamp">The current timestamp.</param>
        /// <param name="Window">How long a nonce has to be remembered.</param>
        public Boolean TryUse(Byte[]          Nonce,
                              DateTimeOffset  Timestamp,
                              TimeSpan        Window)
        {

            ArgumentNullException.ThrowIfNull(Nonce);

            Cleanup(Timestamp, Window);

            // A nonce is remembered until it could no longer be replayed anyway.
            return nonces.TryAdd(Convert.ToHexString(Nonce), Timestamp + Window);

        }

        #endregion

        #region Cleanup(Timestamp, Window)

        /// <summary>
        /// Forget all nonces that can no longer be replayed.
        /// </summary>
        /// <param name="Timestamp">The current timestamp.</param>
        /// <param name="Window">How long a nonce has to be remembered.</param>
        public void Cleanup(DateTimeOffset  Timestamp,
                            TimeSpan        Window)
        {

            #region Do not walk the whole cache on every single request

            var now       = Timestamp.UtcTicks;
            var lastRun   = Interlocked.Read(ref lastCleanupTicks);
            var interval  = Math.Max(TimeSpan.TicksPerSecond, Window.Ticks / 10);

            if (now - lastRun < interval)
                return;

            if (Interlocked.CompareExchange(ref lastCleanupTicks, now, lastRun) != lastRun)
                return;

            #endregion

            foreach (var nonce in nonces)
            {
                if (nonce.Value <= Timestamp)
                    nonces.TryRemove(nonce.Key, out _);
            }

        }

        #endregion

        #region Clear()

        /// <summary>
        /// Forget all nonces.
        /// </summary>
        public void Clear()
        {
            nonces.Clear();
            Interlocked.Exchange(ref lastCleanupTicks, 0);
        }

        #endregion

    }

}
