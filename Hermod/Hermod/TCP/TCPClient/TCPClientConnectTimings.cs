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

using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Runtime.CompilerServices;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;


#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    public class TCPClientConnectTimings
    {

        #region Data

        private readonly List<Elapsed<String>>  errors = [];
        private readonly Stopwatch              stopWatch;

        #endregion

        #region Properties

        public TimeSpan                      Elapsed
            => stopWatch.Elapsed;

        public IEnumerable<Elapsed<String>>  Errors
            => errors;


        public DateTimeOffset                Start             { get; }
        public TimeSpan?                     DNSSRVLookup      { get; internal set; }
        public TimeSpan?                     DNSLookup         { get; internal set; }
        public TimeSpan?                     Connected         { get; internal set; }
        public TimeSpan?                     TLSHandshake      { get; internal set; }
        public Byte                          RestartCounter    { get; internal set; }

        #endregion

        #region Constructor(s)


        public TCPClientConnectTimings()
        {

            this.Start      = Timestamp.Now;
            this.stopWatch  = Stopwatch.StartNew();

        }

        #endregion



        public void AddError(String Error)
        {
            errors.Add(
                new Elapsed<String>(
                    stopWatch.IsRunning
                        ? stopWatch.Elapsed
                        : Timestamp.Now - Start,
                    Error
                )
            );
        }


        public void Stop()
        {
            stopWatch.Stop();
        }


        public String ErrorsAsString()

            => errors.
                   Select(elapsed => $"{elapsed.Time.TotalMilliseconds:F2}:{elapsed.Value}").
                   AggregateWith(Environment.NewLine);


        public override String ToString()

            => String.Concat(
                   $"Start: ",                Start.                                      ToISO8601(),                             " > ",
                   $"DNS SRV Lookup: ",       DNSSRVLookup?.                              TotalMilliseconds.ToString("F2") ?? "-", " > ",
                   $"DNS Lookup: ",           DNSLookup?.                                 TotalMilliseconds.ToString("F2") ?? "-", " > ",
                   $"Connected: ",            Connected?.                                 TotalMilliseconds.ToString("F2") ?? "-", " > ",
                   $"TLSHandshake: ",         TLSHandshake?.                              TotalMilliseconds.ToString("F2") ?? "-", " > ",
                   $"RestartCounter: ",       RestartCounter - 1,                                                                  " > "
               );

    }

}
