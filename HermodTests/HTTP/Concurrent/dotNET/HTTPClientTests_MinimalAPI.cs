/*
 * Copyright (c) 2010-2023, Achim Friedland <achim.friedland@graphdefined.com>
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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.UnitTests.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.UnitTests.HTTP.Concurrent
{

    /// <summary>
    /// Tests between Hermod HTTP clients and Hermod HTTP servers.
    /// </summary>
    [TestFixture]
    public class HTTPClientTests_MinimalAPI : AMinimalDotNetWebAPI
    {

        #region Data

        public static readonly IPPort HTTPPort = IPPort.Parse(184);

        #endregion

        #region Constructor(s)

        public HTTPClientTests_MinimalAPI()
            : base(HTTPPort)
        { }

        #endregion


        #region HTTPClientTest_Concurrent_001()

        [Test]
        public async Task HTTPClientTest_Concurrent_001()
        {

            var startTime     = Timestamp.Now;
            var httpRequests  = new List<Task<HTTPResponse>>();

            for (var i = 0; i < 100; i++)
            {
                httpRequests.Add(new HTTPClient(URL.Parse("http://127.0.0.1:82")).
                                         POST(HTTPPath.Root + "mirror" + "httpBody",
                                              request => {
                                                  request.ContentType  = HTTPContentType.TEXT_UTF8;
                                                  request.Content      = i.ToString().ToUTF8Bytes();//.PadLeft(4, '0').ToUTF8Bytes();
                                              }));
            }

            var responeses = await Task.WhenAll(httpRequests.ToArray());

            var runtime = Timestamp.Now - startTime;

            var responseTuples  = responeses.    Select (response => new Tuple<String, String, TimeSpan>(response.HTTPRequest?.HTTPBodyAsUTF8String ?? "xxxx",
                                                                                                         response.HTTPBodyAsUTF8String              ?? "yyyy",
                                                                                                         response.Runtime)).
                                                 ToArray();

            var responseErrors  = responseTuples.Where  (tuple => tuple.Item1 != tuple.Item2.Reverse()).
                                                 ToArray();

            var minRuntime      = responseTuples.Min    (tuple => tuple.Item3.TotalMicroseconds);
            var maxRuntime      = responseTuples.Max    (tuple => tuple.Item3.TotalMicroseconds);
            var avgRuntime      = responseTuples.Average(tuple => tuple.Item3.TotalMicroseconds);

        }

        #endregion


    }

}
