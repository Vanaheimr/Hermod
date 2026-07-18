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

using System.Collections.Concurrent;

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    public readonly record struct RateLimitDecision(Boolean Allowed,
                                                     TimeSpan RetryAfter,
                                                     Int32    RemainingTokens);


    /// <summary>
    /// A bounded, thread-safe in-memory token-bucket limiter.
    /// </summary>
    public sealed class InMemoryTokenBucketRateLimiter
    {

        private sealed class Bucket(DateTimeOffset Timestamp,
                                    Double          Capacity)
        {
            public readonly Object SyncRoot = new();
            public       Double     Tokens  = Capacity;
            public       DateTimeOffset LastRefill  = Timestamp;
            public       DateTimeOffset LastTouched = Timestamp;
        }

        private readonly ConcurrentDictionary<String, Bucket> buckets = new(StringComparer.Ordinal);
        private readonly Object                                 registryLock = new();

        private readonly Double   capacity;
        private readonly Double   refillTokensPerSecond;
        private readonly Int32    maximumBuckets;
        private readonly TimeSpan bucketLifetime;


        public Int32 BucketCount
            => buckets.Count;


        public InMemoryTokenBucketRateLimiter(Int32    Capacity,
                                              TimeSpan RefillPeriod,
                                              Int32    MaximumBuckets = 10_000,
                                              TimeSpan? BucketLifetime = null)
        {

            if (Capacity < 1)
                throw new ArgumentOutOfRangeException(nameof(Capacity));

            if (RefillPeriod <= TimeSpan.Zero || RefillPeriod == Timeout.InfiniteTimeSpan)
                throw new ArgumentOutOfRangeException(nameof(RefillPeriod));

            if (MaximumBuckets < 1)
                throw new ArgumentOutOfRangeException(nameof(MaximumBuckets));

            this.capacity              = Capacity;
            this.refillTokensPerSecond = Capacity / RefillPeriod.TotalSeconds;
            this.maximumBuckets        = MaximumBuckets;
            this.bucketLifetime        = BucketLifetime ?? TimeSpan.FromMinutes(10);

            if (this.bucketLifetime <= TimeSpan.Zero || this.bucketLifetime == Timeout.InfiniteTimeSpan)
                throw new ArgumentOutOfRangeException(nameof(BucketLifetime));

        }


        public RateLimitDecision TryAcquire(String           Key,
                                             DateTimeOffset? Timestamp = null)
        {

            if (String.IsNullOrEmpty(Key))
                throw new ArgumentException("The rate-limit key must not be null or empty.", nameof(Key));

            var now    = Timestamp ?? DateTimeOffset.UtcNow;
            var bucket = GetOrCreateBucket(Key, now);

            if (bucket is null)
            {
                return new RateLimitDecision(
                           Allowed:         false,
                           RetryAfter:      bucketLifetime,
                           RemainingTokens: 0
                       );
            }

            lock (bucket.SyncRoot)
            {

                var elapsed = now - bucket.LastRefill;
                if (elapsed > TimeSpan.Zero)
                {
                    bucket.Tokens = Math.Min(
                                        capacity,
                                        bucket.Tokens + elapsed.TotalSeconds * refillTokensPerSecond
                                    );
                    bucket.LastRefill = now;
                }

                bucket.LastTouched = now;

                if (bucket.Tokens >= 1.0)
                {
                    bucket.Tokens -= 1.0;

                    return new RateLimitDecision(
                               Allowed:         true,
                               RetryAfter:      TimeSpan.Zero,
                               RemainingTokens: (Int32) Math.Floor(bucket.Tokens)
                           );
                }

                var retryAfter = refillTokensPerSecond > 0
                                     ? TimeSpan.FromSeconds((1.0 - bucket.Tokens) / refillTokensPerSecond)
                                     : bucketLifetime;

                return new RateLimitDecision(
                           Allowed:         false,
                           RetryAfter:      retryAfter,
                           RemainingTokens: 0
                       );

            }

        }


        private Bucket? GetOrCreateBucket(String         Key,
                                          DateTimeOffset Timestamp)
        {

            if (buckets.TryGetValue(Key, out var existingBucket))
            {

                lock (existingBucket.SyncRoot)
                {
                    if (Timestamp - existingBucket.LastTouched < bucketLifetime)
                        return existingBucket;
                }

            }

            lock (registryLock)
            {

                EvictExpiredBuckets(Timestamp);

                if (buckets.TryGetValue(Key, out existingBucket))
                    return existingBucket;

                if (buckets.Count >= maximumBuckets)
                    return null;

                var newBucket = new Bucket(Timestamp, capacity);
                buckets.TryAdd(Key, newBucket);
                return newBucket;

            }

        }


        private void EvictExpiredBuckets(DateTimeOffset Timestamp)
        {

            foreach (var pair in buckets)
            {

                var bucket = pair.Value;

                lock (bucket.SyncRoot)
                {
                    if (Timestamp - bucket.LastTouched >= bucketLifetime)
                        buckets.TryRemove(new KeyValuePair<String, Bucket>(pair.Key, bucket));
                }

            }

        }

    }

}
