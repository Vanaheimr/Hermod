/*
 * Copyright (c) 2010-2024 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System;
using System.Linq;
using System.Collections.Generic;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Sockets.RawIP.ICMP
{

    /// <summary>
    /// A collection of ping results.
    /// </summary>
    public class PingResults
    {

        #region Properties

        /// <summary>
        /// Whether the ping succeeded.
        /// </summary>
        public Boolean                  Success            { get; }

        /// <summary>
        /// The common result or error.
        /// </summary>
        public ICMPErrors               Error              { get; }

        /// <summary>
        /// The enumeration of results or errors.
        /// </summary>
        public IEnumerable<PingResult>  Results            { get; }

        /// <summary>
        /// The number of "good" results.
        /// </summary>
        public UInt32                   NumberOfReplies    { get; }

        /// <summary>
        /// The number of lost packets.
        /// </summary>
        public Double                   Paketloss          { get; }

        /// <summary>
        /// The minimum round trip time.
        /// </summary>
        public TimeSpan                 Min                { get; }

        /// <summary>
        /// The average round trip time.
        /// </summary>
        public TimeSpan                 Avg                { get; }

        /// <summary>
        /// The standard deviation of the round trip time.
        /// </summary>
        public Double                   StdDev             { get; }

        /// <summary>
        /// The maximum round trip time.
        /// </summary>
        public TimeSpan                 Max                { get; }

        /// <summary>
        /// The common timeout of all pings.
        /// </summary>
        public TimeSpan                 Timeout            { get; }

        /// <summary>
        /// The overall runtime of all pings.
        /// </summary>
        public TimeSpan                 Runtime            { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// A collection of ping results.
        /// </summary>
        /// <param name="PingResults">An enumeration of results or errors.</param>
        /// <param name="Timeout">The common timeout of all pings.</param>
        /// <param name="Runtime">The overall runtime of all pings.</param>
        public PingResults(IEnumerable<PingResult>  PingResults,
                            TimeSpan                 Timeout,
                            TimeSpan                 Runtime)
        {

            var goodResults       = PingResults.Where(failure => failure.Error == ICMPErrors.Success).ToArray();

            var average           = goodResults.Any()
                                        ? goodResults.Select(result => result.Runtime.TotalMilliseconds).AverageAndStdDev()
                                        : new Illias.Collections.Tuple<Double, Double>(Timeout.TotalMilliseconds, 0);

            var commonError       = ICMPErrors.Mixed;

            var groups            = PingResults.GroupBy(pingResult => pingResult.Error).ToArray();
            if (groups.Length == 1)
                commonError = groups.First().Key;

            this.Error            = commonError;
            this.Results          = PingResults;
            this.NumberOfReplies  = (UInt32) goodResults.Length;
            this.Paketloss        = Math.Round(100 - 100 * ((Double) NumberOfReplies) / Results.Count(), 0);
            this.Success          = goodResults.Length == PingResults.Count();
            this.Min              = goodResults.Any() ? TimeSpan.FromMilliseconds(goodResults.Select(pingResult => pingResult.Runtime.TotalMilliseconds).Min()) : Timeout;
            this.Avg              = TimeSpan.FromMilliseconds(average.Item1);
            this.StdDev           = average.Item2;
            this.Max              = goodResults.Any() ? TimeSpan.FromMilliseconds(goodResults.Select(pingResult => pingResult.Runtime.TotalMilliseconds).Max()) : Timeout;
            this.Timeout          = Timeout;
            this.Runtime          = Runtime;

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => String.Concat(Error,
                                Error == ICMPErrors.Mixed
                                    ? String.Concat(" (", Paketloss, "% ", NumberOfReplies, "/", Results, "): ")
                                    : ": ",
                                "Min: ", Math.Round(Min.TotalMilliseconds, 0), ", ",
                                "Avg: ", Math.Round(Avg.TotalMilliseconds, 0), " (StdDev: ", Math.Round(StdDev, 2), "), ",
                                "Max: ", Math.Round(Max.TotalMilliseconds, 0));

        #endregion

    }

}
