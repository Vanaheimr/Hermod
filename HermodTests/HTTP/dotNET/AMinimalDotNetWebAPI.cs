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

using System.Text;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.UnitTests.HTTP
{

    /// <summary>
    /// .NET Kestrel HTTP server tests endpoints.
    /// </summary>
    public abstract class AMinimalDotNetWebAPI
    {

        #region Data

        protected readonly WebApplicationBuilder  builder;
        protected readonly WebApplication         app;

        #endregion

        #region Constructor(s)

        public AMinimalDotNetWebAPI(IPPort HTTPPort)
        {

            builder  = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls($"http://localhost:{HTTPPort}");
            app      = builder.Build();

            #region GET     /

            app.MapGet("/",
                        async (HttpRequest   httpRequest,
                               HttpResponse  httpResponse) => {

                var responseString = "Hello World!";

                // Without Kestrel will send everything as: "Transfer-Encoding: chunked"
                httpResponse.ContentLength      = Encoding.UTF8.GetByteCount(responseString);
                httpResponse.ContentType        = "text/plain; charset=utf-8";
                httpResponse.Headers["Server"]  = "Kestrel Test Server";

                await httpResponse.WriteAsync(responseString);

            });

            #endregion

            #region POST    /mirror/queryString

            app.MapPost("/mirror/queryString",
                        async (HttpRequest   httpRequest,
                               HttpResponse  httpResponse) => {

                var requestBody   = httpRequest.Query["q"].ToString();
                var reversedBody  = requestBody.Reverse();

                // Without Kestrel will send everything as: "Transfer-Encoding: chunked"
                httpResponse.ContentLength      = Encoding.UTF8.GetByteCount(reversedBody);
                httpResponse.ContentType        = "text/plain; charset=utf-8";
                httpResponse.Headers["Server"]  = "Kestrel Test Server";

                await httpResponse.WriteAsync(reversedBody);

            });

            #endregion

            #region POST    /mirror/httpBody

            app.MapPost("/mirror/httpBody",
                        async (HttpRequest   httpRequest,
                               HttpResponse  httpResponse) => {

                var requestBody   = await new StreamReader(httpRequest.Body).ReadToEndAsync();
                var reversedBody  = requestBody.Reverse();

                // Without Kestrel will send everything as: "Transfer-Encoding: chunked"
                httpResponse.ContentLength      = Encoding.UTF8.GetByteCount(reversedBody);
                httpResponse.ContentType        = "text/plain; charset=utf-8";
                httpResponse.Headers["Server"]  = "Kestrel Test Server";

                await httpResponse.WriteAsync(reversedBody);

            });

            #endregion

            #region MIRROR  /mirror/httpBody

            app.MapMethods("/mirror/httpBody",
                           new[] { "MIRROR" },
                           async (HttpRequest   httpRequest,
                                  HttpResponse  httpResponse) => {

                var requestBody   = await new StreamReader(httpRequest.Body).ReadToEndAsync();
                var reversedBody  = requestBody.Reverse();

                // Without Kestrel will send everything as: "Transfer-Encoding: chunked"
                httpResponse.ContentLength      = Encoding.UTF8.GetByteCount(reversedBody);
                httpResponse.ContentType        = "text/plain; charset=utf-8";
                httpResponse.Headers["Server"]  = "Kestrel Test Server";

                await httpResponse.WriteAsync(reversedBody);

            });

            #endregion

            app.Start();

        }

        #endregion


    }

}
