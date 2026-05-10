/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
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
using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using System.Net.Http.Headers;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    #region (class) BasicAuthenticationHandler

    public class BasicAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions>  Options,
                                            ILoggerFactory                                Logger,
                                            UrlEncoder                                    Encoder)

        : AuthenticationHandler<AuthenticationSchemeOptions>(
              Options,
              Logger,
              Encoder
          )

    {

        public const String AuthenticationScheme  = "BasicAuthentication";
        public const String ChallengeHeaderValue  = @"Basic realm=""Access to the staging site"", charset =""UTF-8""";

        private static readonly IReadOnlyDictionary<String, String> validCredentials = new Dictionary<String, String> {
            { "testUser1", "testPassword1" },
            { "testUser2", "testPassword2" }
        };

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {

            if (!Request.Headers.TryGetValue("Authorization", out Microsoft.Extensions.Primitives.StringValues authorizationValue))
                return Task.FromResult(AuthenticateResult.Fail("The received authorization header not found!"));

            if (authorizationValue.Count != 1)
                return Task.FromResult(AuthenticateResult.Fail("Multiple authorization headers are not supported!"));

            var authHeaderString  = authorizationValue.ToString();

            if (String.IsNullOrWhiteSpace(authHeaderString))
                return Task.FromResult(AuthenticateResult.Fail("The received authorization header is empty!"));

            if (!AuthenticationHeaderValue.TryParse(authHeaderString, out var authHeader))
                return Task.FromResult(AuthenticateResult.Fail("The received authorization header is invalid!"));

            if (!authHeader.Scheme.Equals("Basic", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(AuthenticateResult.Fail($"Unsupported authorization scheme '{authHeader.Scheme}'!"));

            if (String.IsNullOrWhiteSpace(authHeader.Parameter))
                return Task.FromResult(AuthenticateResult.Fail("The received authorization header is missing credentials!"));

            String decodedCredentials;

            try
            {
                decodedCredentials = Encoding.UTF8.GetString(Convert.FromBase64String(authHeader.Parameter));
            }
            catch (FormatException e)
            {
                return Task.FromResult(AuthenticateResult.Fail($"The received authorization header credentials are invalid: {e.Message}"));
            }

            var separatorIndex = decodedCredentials.IndexOf(':');

            if (separatorIndex < 0)
                return Task.FromResult(AuthenticateResult.Fail("The received authorization header credentials are missing a password separator!"));

            var username       = decodedCredentials[..separatorIndex];
            var password       = decodedCredentials[(separatorIndex + 1)..];

            if (String.IsNullOrEmpty(username) || String.IsNullOrEmpty(password))
                return Task.FromResult(AuthenticateResult.Fail("The received authorization header credentials are incomplete!"));

            if (!validCredentials.TryGetValue(username, out var expectedPassword) ||
                !password.Equals(expectedPassword, StringComparison.Ordinal))
                return Task.FromResult(AuthenticateResult.Fail("Invalid username or password!"));

            var claims         = new[] {
                                     new Claim(
                                         ClaimTypes.Name,
                                         username
                                     )
                                 };

            var identity       = new ClaimsIdentity      (claims,    Scheme.Name);
            var principal      = new ClaimsPrincipal     (identity);
            var ticket         = new AuthenticationTicket(principal, Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(ticket));

        }

        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {

            // This method will be called whenever HandleAuthenticateAsync() failed to authenticate a user.

            Response.Headers.WWWAuthenticate = ChallengeHeaderValue;

            return base.HandleChallengeAsync(properties);

        }

    }

    #endregion

    #region StatisticsMiddleware

    public class StatisticsMiddleware(RequestDelegate Next)
    {

        public const String ResponseStartHeader = "X-Response-Start-Ms";

        private readonly RequestDelegate _next = Next;

        public async Task InvokeAsync(HttpContext HTTPContext)
        {

            var watch = Stopwatch.StartNew();

            HTTPContext.Response.OnStarting(() => {

                watch.Stop();

                HTTPContext.Response.Headers[ResponseStartHeader] = watch.Elapsed.TotalMilliseconds.ToString(
                                                                        "F3",
                                                                        CultureInfo.InvariantCulture
                                                                    );

                return Task.CompletedTask;

            });

            await _next(HTTPContext).
                      ConfigureAwait(false);

        }

    }

    #endregion


    /// <summary>
    /// .NET Kestrel HTTP server tests endpoints.
    /// </summary>
    public abstract class AMinimalDotNetWebAPI
    {

        #region Data

        protected readonly WebApplicationBuilder  builder;
        protected readonly WebApplication         app;

        protected          Uri                    BaseURI  { get; private set; } = null!;
        protected          String                 BaseURL  { get; private set; } = String.Empty;

        private            Boolean                isStarted;

        #endregion

        #region Constructor(s)

        public AMinimalDotNetWebAPI(IPPort HTTPPort)
        {

            builder  = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls($"http://127.0.0.1:{HTTPPort}");
            builder.Services.AddAuthentication(BasicAuthenticationHandler.AuthenticationScheme).
                             AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>(
                                 BasicAuthenticationHandler.AuthenticationScheme,
                                 null
                             );
            builder.Services.AddAuthorization();

            app      = builder.Build();
            app.UseMiddleware<StatisticsMiddleware>();
            app.Use((context, next) => {
                context.Response.Headers.Server = "Kestrel Test Server";
                return next.Invoke();
            });
            app.UseAuthentication();
            app.UseAuthorization();


            #region GET     /

            app.MapGet("/",
                       (HttpRequest   httpRequest,
                        HttpResponse  httpResponse) =>

                           WriteTextAsync(
                               httpResponse,
                               "Hello World!"
                           )

            );

            #endregion

            #region GET     /NotForEveryone

            app.MapGet("/NotForEveryone",
                       [Authorize(AuthenticationSchemes = BasicAuthenticationHandler.AuthenticationScheme)]
                       async (context) => {

                var username = context.User?.Identity?.Name ?? "-";

                if (username == "testUser2")
                {

                    await WriteTextAsync(
                              context.Response,
                              $"Sorry '{username}' please contact your administrator!",
                              StatusCodes.Status403Forbidden
                          );

                }
                else
                {

                    await WriteTextAsync(
                              context.Response,
                              $"Hello '{username}'!"
                          );

                }

            });

            #endregion

            #region POST    /mirror/queryString

            app.MapPost("/mirror/queryString",
                        async (HttpRequest   httpRequest,
                               HttpResponse  httpResponse) => {

                await WriteTextAsync(
                          httpResponse,
                          httpRequest.Query["q"].ToString().Reverse()
                      );

            });

            #endregion

            #region POST    /mirror/httpBody

            app.MapPost("/mirror/httpBody",
                        async (HttpRequest   httpRequest,
                               HttpResponse  httpResponse) => {

                var requestBody = await new StreamReader(httpRequest.Body).ReadToEndAsync();

                await WriteTextAsync(
                          httpResponse,
                          requestBody.Reverse()
                      );

            });

            #endregion

            #region MIRROR  /mirror/httpBody

            app.MapMethods("/mirror/httpBody",
                           [ "MIRROR" ],
                           async (HttpRequest   httpRequest,
                                  HttpResponse  httpResponse) => {

                var requestBody = await new StreamReader(httpRequest.Body).ReadToEndAsync();

                await WriteTextAsync(
                          httpResponse,
                          requestBody.Reverse()
                      );

            });

            #endregion


            #region GET     /chunked

            app.MapGet("/chunked", async (http) => {

                http.Response.StatusCode   = 200;
                http.Response.ContentType  = "text/plain";

                var httpStream = http.Response.Body;

                await httpStream.WriteAsync("Hello".ToUTF8Bytes());
                await httpStream.FlushAsync();
                await httpStream.WriteAsync(" ".ToUTF8Bytes());
                await httpStream.FlushAsync();
                await httpStream.WriteAsync("World!".ToUTF8Bytes());
                await httpStream.FlushAsync();

            });

            #endregion

            #region GET     /chunkedSlow

            app.MapGet("/chunkedSlow", async (http) => {

                http.Response.StatusCode   = 200;
                http.Response.ContentType  = "text/plain";

                var httpStream = http.Response.Body;

                await httpStream.WriteAsync("Hello".ToUTF8Bytes());
                await httpStream.FlushAsync();
                await Task.Delay(100, http.RequestAborted);
                await httpStream.WriteAsync(" ".ToUTF8Bytes());
                await httpStream.FlushAsync();
                await Task.Delay(100, http.RequestAborted);
                await httpStream.WriteAsync("World!".ToUTF8Bytes());
                await httpStream.FlushAsync();

            });

            #endregion

            #region GET     /chunkedSlowTrailerHeaders

            app.MapGet("/chunkedSlowTrailerHeaders", async (http) => {

                http.Response.StatusCode   = 200;
                http.Response.ContentType  = "text/plain";
                http.Response.DeclareTrailer("X-Message-Length");
                //http.Response.Headers.TransferEncoding = "chunked";

                var httpStream = http.Response.Body;

                await http.Response.StartAsync();

                await httpStream.WriteAsync("Hello World!".ToUTF8Bytes());
                //await httpStream.FlushAsync();
                //await Task.Delay(100);
                //await httpStream.WriteAsync(" ".ToUTF8Bytes());
                //await httpStream.FlushAsync();
                //await Task.Delay(100);
                //await httpStream.WriteAsync("World!".ToUTF8Bytes());
                //await httpStream.FlushAsync();

                if (http.Response.SupportsTrailers())
                {
                    http.Response.AppendTrailer("X-Message-Length",   "13");
                    http.Response.AppendTrailer("X-Protocol-Version", "1.0");
                }

                await http.Response.CompleteAsync();
                //await http.Response.Body.FlushAsync();

            });

            #endregion

        }

        #endregion


        #region WriteTextAsync(HTTPResponse, Text, ...)

        private static Task WriteTextAsync(HttpResponse  HTTPResponse,
                                           String        Text,
                                           Int32         StatusCode   = StatusCodes.Status200OK,
                                           String        ContentType  = "text/plain; charset=utf-8")
        {

            HTTPResponse.StatusCode     = StatusCode;
            HTTPResponse.ContentLength  = Encoding.UTF8.GetByteCount(Text);
            HTTPResponse.ContentType    = ContentType;

            return HTTPResponse.WriteAsync(Text);

        }

        #endregion


        #region StartWebAPI()

        [OneTimeSetUp]
        public async Task StartWebAPI()
        {

            await app.StartAsync().
                      ConfigureAwait(false);

            var baseURL = app.Urls.FirstOrDefault()
                              ?? throw new InvalidOperationException("The minimal web API did not expose a listening URL!");

            BaseURI  = new Uri(baseURL);
            BaseURL  = baseURL.TrimEnd('/');

            isStarted = true;

        }

        #endregion


        #region StopWebAPI()

        [OneTimeTearDown]
        public async Task StopWebAPI()
        {

            if (isStarted)
            {

                await app.StopAsync().
                          ConfigureAwait(false);

                isStarted = false;

            }

            await app.DisposeAsync().
                      ConfigureAwait(false);

        }

        #endregion


    }

}
