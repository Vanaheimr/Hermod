/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System.Net;
using System.Text;

using NUnit.Framework;
using org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP;
using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.Concurrent
{

    /// <summary>
    /// Tests between .NET HTTP clients and Hermod HTTP servers.
    /// </summary>
    [TestFixture]
    public class DotNetHTTPClientTests_MinimalAPI : AMinimalDotNetWebAPI
    {

        #region Data

        public static readonly IPPort HTTPPort = IPPort.Parse(185);

        #endregion

        #region Constructor(s)

        public DotNetHTTPClientTests_MinimalAPI()
            : base(HTTPPort)
        { }

        #endregion


        private async Task<Tuple<HttpResponseMessage?, TimeSpan>> POST_Timestamped(string URL,
                                                                                   string HTTPBody)
        {

            var startTime = Timestamp.Now;

            try
            {

                var response = await new HttpClient().PostAsync(URL,
                                                                  new StringContent(
                                                                      HTTPBody,
                                                                      Encoding.UTF8,
                                                                      "text/plain"
                                                                  ));

                var runtime = Timestamp.Now - startTime;

                return new Tuple<HttpResponseMessage?, TimeSpan>(response,
                                                                 runtime);

            }
            catch
            {
                return new Tuple<HttpResponseMessage?, TimeSpan>(null,
                                                                 Timestamp.Now - startTime);
            }


            //     System.Net.Http.HttpRequestException : Es konnte keine Verbindung hergestellt werden, da der Zielcomputer die Verbindung verweigerte. (127.0.0.1:82)
            //   ----> System.Net.Sockets.SocketException : Es konnte keine Verbindung hergestellt werden, da der Zielcomputer die Verbindung verweigerte.
            // 
            //   Stack Trace: 
            // HttpConnectionPool.ConnectToTcpHostAsync(String host, Int32 port, HttpRequestMessage initialRequest, Boolean async, CancellationToken cancellationToken)
            // HttpConnectionPool.ConnectAsync(HttpRequestMessage request, Boolean async, CancellationToken cancellationToken)
            // HttpConnectionPool.CreateHttp11ConnectionAsync(HttpRequestMessage request, Boolean async, CancellationToken cancellationToken)
            // HttpConnectionPool.AddHttp11ConnectionAsync(QueueItem queueItem)
            // TaskCompletionSourceWithCancellation`1.WaitWithCancellationAsync(CancellationToken cancellationToken)
            // HttpConnectionWaiter`1.WaitForConnectionAsync(Boolean async, CancellationToken requestCancellationToken)
            // HttpConnectionPool.SendWithVersionDetectionAndRetryAsync(HttpRequestMessage request, Boolean async, Boolean doRequestAuth, CancellationToken cancellationToken)
            // RedirectHandler.SendAsync(HttpRequestMessage request, Boolean async, CancellationToken cancellationToken)
            // HttpClient.<SendAsync>g__Core|83_0(HttpRequestMessage request, HttpCompletionOption completionOption, CancellationTokenSource cts, Boolean disposeCts, CancellationTokenSource pendingRequestsCts, CancellationToken originalCancellationToken)
            // DotNetHTTPClientTests.POST_Timestamped(String URL, String HTTPBody) line 103
            // DotNetHTTPClientTests.HTTPClientTest_Concurrent_001() line 131
            // GenericAdapter`1.BlockUntilCompleted()
            // NoMessagePumpStrategy.WaitForCompletion(AwaitAdapter awaiter)
            // AsyncToSyncAdapter.Await(Func`1 invoke)
            // TestMethodCommand.RunTestMethod(TestExecutionContext context)
            // TestMethodCommand.Execute(TestExecutionContext context)
            // <>c__DisplayClass4_0.<PerformWork>b__0()
            // <>c__DisplayClass1_0`1.<DoIsolated>b__0(Object _)
            // ExecutionContext.RunInternal(ExecutionContext executionContext, ContextCallback callback, Object state)
            // --- End of stack trace from previous location ---
            // ExecutionContext.RunInternal(ExecutionContext executionContext, ContextCallback callback, Object state)
            // ContextUtils.DoIsolated(ContextCallback callback, Object state)
            // ContextUtils.DoIsolated[T](Func`1 func)
            // SimpleWorkItem.PerformWork()
            // --SocketException
            // AwaitableSocketAsyncEventArgs.ThrowException(SocketError error, CancellationToken cancellationToken)
            // IValueTaskSource.GetResult(Int16 token)
            // Socket.<ConnectAsync>g__WaitForConnectWithCancellation|281_0(AwaitableSocketAsyncEventArgs saea, ValueTask connectTask, CancellationToken cancellationToken)
            // HttpConnectionPool.ConnectToTcpHostAsync(String host, Int32 port, HttpRequestMessage initialRequest, Boolean async, CancellationToken cancellationToken)

        }



        #region HTTPClientTest_Concurrent_001()

        [Test]
        public async Task HTTPClientTest_Concurrent_001()
        {

            var startTime = Timestamp.Now;
            var httpRequests = new List<Task<Tuple<HttpResponseMessage?, TimeSpan>>>();

            for (var i = 0; i < 1000; i++)
                httpRequests.Add(POST_Timestamped("http://127.0.0.1:82/mirror/httpBody", i.ToString()));

            var responeses = await Task.WhenAll(httpRequests.ToArray());

            var runtime1 = (Timestamp.Now - startTime).TotalSeconds;

            var responseTuples = responeses.Where(response => response.Item1 is not null).
                                                 Select(response => new Tuple<string, string, TimeSpan>(
                                                                         response.Item1!.Content.ReadAsStringAsync().GetAwaiter().GetResult(),
                                                                         response.Item1!.RequestMessage!.Content!.ReadAsStringAsync().GetAwaiter().GetResult(),
                                                                         response.Item2!
                                                                     )).
                                                 ToArray();

            var responseErrors = responseTuples.Where(tuple => tuple.Item1 != tuple.Item2.Reverse()).
                                                 ToArray();

            var minRuntime = responseTuples.Min(tuple => tuple.Item3.TotalMilliseconds);
            var maxRuntime = responseTuples.Max(tuple => tuple.Item3.TotalMilliseconds);
            var avgRuntime = responseTuples.Average(tuple => tuple.Item3.TotalMilliseconds);

            var runtime2 = (Timestamp.Now - startTime).TotalSeconds;

        }

        #endregion


    }

}
