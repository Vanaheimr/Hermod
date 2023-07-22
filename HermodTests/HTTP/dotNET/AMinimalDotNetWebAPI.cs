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
using System.Text.Encodings.Web;
using System.Security.Claims;
using System.Net.Http.Headers;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.UnitTests.HTTP
{

    #region (class) BasicAuthenticationHandler

    public class BasicAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public BasicAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions>  Options,
                                          ILoggerFactory                                Logger,
                                          UrlEncoder                                    Encoder,
                                          ISystemClock                                  Clock)

            : base(Options,
                   Logger,
                   Encoder,
                   Clock)

        { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {

            try
            {

                if (!Request.Headers.ContainsKey("Authorization"))
                    return Task.FromResult(AuthenticateResult.Fail("The received authorization header not found!"));

                var authHeader   = AuthenticationHeaderValue.Parse(Request.Headers["Authorization"]);

                if (authHeader is null || authHeader.Parameter is null || authHeader.Parameter.IsNullOrEmpty())
                    return Task.FromResult(AuthenticateResult.Fail("The received authorization header is invalid!"));

                var credentials  = Encoding.UTF8.GetString(Convert.FromBase64String(authHeader.Parameter)).Split(':');
                var username     = credentials[0];
                var password     = credentials[1];

                if (!username.Equals("testUser1")     && !username.Equals("testUser2"))
                    return Task.FromResult(AuthenticateResult.Fail($"Invalid username '{username}'!"));

                if (!password.Equals("testPassword1") && !password.Equals("testPassword2"))
                    return Task.FromResult(AuthenticateResult.Fail($"Invalid password '{password}'!"));

                var claims       = new[] {
                                       new Claim(
                                           ClaimTypes.Name,
                                           username
                                       )
                                   };

                var identity     = new ClaimsIdentity      (claims,    Scheme.Name);
                var principal    = new ClaimsPrincipal     (identity);
                var ticket       = new AuthenticationTicket(principal, Scheme.Name);

                return Task.FromResult(AuthenticateResult.Success(ticket));

            }
            catch (Exception e)
            {
                return Task.FromResult(AuthenticateResult.Fail($"The received authorization header is invalid: {e.Message}"));
            }

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

        #endregion

        #region Constructor(s)

        public AMinimalDotNetWebAPI(IPPort HTTPPort)
        {

            builder  = WebApplication.CreateBuilder();
            builder.WebHost. UseUrls($"http://localhost:{HTTPPort}");
            builder.Services.AddAuthentication("BasicAuthentication").AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>("BasicAuthentication", null);
            builder.Services.AddAuthorization();

            app      = builder.Build();
            app.Use((context, next) => {
                context.Response.Headers["Server"] = "Kestrel Test Server";
                return next.Invoke();
            });
            app.UseAuthentication();
            //app.Use((context, next) => {
            //    return next.Invoke();
            //});
            app.UseAuthorization();


            #region GET     /

            app.MapGet("/",
                        async (HttpRequest   httpRequest,
                               HttpResponse  httpResponse) => {

                var responseString = "Hello World!";

                // Without Kestrel will send everything as: "Transfer-Encoding: chunked"
                httpResponse.ContentLength  = Encoding.UTF8.GetByteCount(responseString);
                httpResponse.ContentType    = "text/plain; charset=utf-8";

                await httpResponse.WriteAsync(responseString);

            });

            #endregion

            #region GET     /NotForEveryone

            app.MapGet("/NotForEveryone",
                       [Authorize(AuthenticationSchemes = "BasicAuthentication")] 
                       async (HttpContext context) => {

                var username = context.User?.Identity?.Name ?? "-";

                if (username == "testUser2")
                {

                    var responseString = $"Sorry '{username}' please contact your administrator!";

                    context.Response.StatusCode                   = 403;
                    // Without Kestrel will send everything as: "Transfer-Encoding: chunked"
                    context.Response.ContentLength                = Encoding.UTF8.GetByteCount(responseString);
                    context.Response.ContentType                  = "text/plain; charset=utf-8";
                    context.Response.Headers["WWW-Authenticate"]  = @"Basic realm=""Access to the staging site"", charset =""UTF-8""";

                    await context.Response.WriteAsync(responseString);

                }
                else
                {

                    var responseString = $"Hello '{username}'!";

                    // Without Kestrel will send everything as: "Transfer-Encoding: chunked"
                    context.Response.ContentLength  = Encoding.UTF8.GetByteCount(responseString);
                    context.Response.ContentType    = "text/plain; charset=utf-8";

                    await context.Response.WriteAsync(responseString);

                }

            });

            #endregion

            #region POST    /mirror/queryString

            app.MapPost("/mirror/queryString",
                        async (HttpRequest   httpRequest,
                               HttpResponse  httpResponse) => {

                var requestBody   = httpRequest.Query["q"].ToString();
                var reversedBody  = requestBody.Reverse();

                // Without Kestrel will send everything as: "Transfer-Encoding: chunked"
                httpResponse.ContentLength  = Encoding.UTF8.GetByteCount(reversedBody);
                httpResponse.ContentType    = "text/plain; charset=utf-8";

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
                httpResponse.ContentLength  = Encoding.UTF8.GetByteCount(reversedBody);
                httpResponse.ContentType    = "text/plain; charset=utf-8";

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
                httpResponse.ContentLength  = Encoding.UTF8.GetByteCount(reversedBody);
                httpResponse.ContentType    = "text/plain; charset=utf-8";

                await httpResponse.WriteAsync(reversedBody);

            });

            #endregion

            app.Start();

        }

        #endregion


    }

}
