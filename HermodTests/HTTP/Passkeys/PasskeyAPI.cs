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

using System.Reflection;
using System.Security.Cryptography;
using System.Security.Authentication;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Logging;
using org.GraphDefined.Vanaheimr.Hermod.Mail;
using org.GraphDefined.Vanaheimr.Hermod.SMTP;
using org.GraphDefined.Vanaheimr.Hermod.Sockets;
using org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP;
using org.GraphDefined.Vanaheimr.Hermod.Passkeys;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.Passkeys
{

    /// <summary>
    /// Extension methods for the Passkey API.
    /// </summary>
    public static class PasskeyAPIExtensions
    {


    }


    public class PasskeyAPI : HTTPExtAPI
    {

        #region Data

        /// <summary>
        /// The default HTTP server name.
        /// </summary>
        public new const           String                    DefaultHTTPServerName                 = "Passkey API";

        /// <summary>
        /// The default HTTP service name.
        /// </summary>
        public new const           String                    DefaultHTTPServiceName                = "Passkey API";

        public     const           String                    DefaultBCTAPI_LoggingPath           = "default";
        public     const           String                    DefaultBCTAPI_DatabaseFileName      = "Passkey.db";
        public     const           String                    DefaultBCTAPI_LogfileName           = "Passkey.log";

        /// <summary>
        /// The HTTP root for embedded resources.
        /// </summary>
        public new const            String                     HTTPRoot                           = "org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.Passkeys.HTTPRoot.";

        /// <summary>
        /// The name of the main chain log file.
        /// </summary>
        public const                String                     BCTDBFile                        = "Passkey.db";

        #endregion

        #region Properties


        #endregion

        #region Events

        #region (protected internal) RegisterOptionsHTTPRequest  (Request)

        /// <summary>
        /// An event sent whenever a RegisterOptions request was received.
        /// </summary>
        public HTTPRequestLogEvent OnRegisterOptions = new ();

        /// <summary>
        /// An event sent whenever a RegisterOptions request was received.
        /// </summary>
        /// <param name="Timestamp">The timestamp of the request.</param>
        /// <param name="API">The HTTP API.</param>
        /// <param name="Request">An HTTP request.</param>
        protected internal Task RegisterOptionsHTTPRequest(DateTime     Timestamp,
                                                           HTTPAPI      API,
                                                           HTTPRequest  Request)

            => OnRegisterOptions.WhenAll(Timestamp,
                                         API ?? this,
                                         Request);

        #endregion

        #region (protected internal) RegisterOptionsHTTPResponse (Response)

        /// <summary>
        /// An event sent whenever a RegisterOptions response was sent.
        /// </summary>
        public HTTPResponseLogEvent OnRegisterOptionsResponse = new ();

        /// <summary>
        /// An event sent whenever a RegisterOptions response was sent.
        /// </summary>
        /// <param name="Timestamp">The timestamp of the request.</param>
        /// <param name="API">The HTTP API.</param>
        /// <param name="Request">An HTTP request.</param>
        /// <param name="Response">An HTTP response.</param>
        protected internal Task RegisterOptionsHTTPResponse(DateTime      Timestamp,
                                                            HTTPAPI       API,
                                                            HTTPRequest   Request,
                                                            HTTPResponse  Response)

            => OnRegisterOptionsResponse.WhenAll(Timestamp,
                                                 API ?? this,
                                                 Request,
                                                 Response);

        #endregion


        #region (protected internal) RegisterHTTPRequest         (Request)

        /// <summary>
        /// An event sent whenever a Register request was received.
        /// </summary>
        public HTTPRequestLogEvent OnRegister = new ();

        /// <summary>
        /// An event sent whenever a Register request was received.
        /// </summary>
        /// <param name="Timestamp">The timestamp of the request.</param>
        /// <param name="API">The HTTP API.</param>
        /// <param name="Request">An HTTP request.</param>
        protected internal Task RegisterHTTPRequest(DateTime     Timestamp,
                                                    HTTPAPI      API,
                                                    HTTPRequest  Request)

            => OnRegister.WhenAll(Timestamp,
                                  API ?? this,
                                  Request);

        #endregion

        #region (protected internal) RegisterHTTPResponse        (Response)

        /// <summary>
        /// An event sent whenever a Register response was sent.
        /// </summary>
        public HTTPResponseLogEvent OnRegisterResponse = new ();

        /// <summary>
        /// An event sent whenever a Register response was sent.
        /// </summary>
        /// <param name="Timestamp">The timestamp of the request.</param>
        /// <param name="API">The HTTP API.</param>
        /// <param name="Request">An HTTP request.</param>
        /// <param name="Response">An HTTP response.</param>
        protected internal Task RegisterHTTPResponse(DateTime      Timestamp,
                                                     HTTPAPI       API,
                                                     HTTPRequest   Request,
                                                     HTTPResponse  Response)

            => OnRegisterResponse.WhenAll(Timestamp,
                                          API ?? this,
                                          Request,
                                          Response);

        #endregion


        #region (protected internal) LoginOptionsHTTPRequest     (Request)

        /// <summary>
        /// An event sent whenever a LoginOptions request was received.
        /// </summary>
        public HTTPRequestLogEvent OnLoginOptions = new ();

        /// <summary>
        /// An event sent whenever a LoginOptions request was received.
        /// </summary>
        /// <param name="Timestamp">The timestamp of the request.</param>
        /// <param name="API">The HTTP API.</param>
        /// <param name="Request">An HTTP request.</param>
        protected internal Task LoginOptionsHTTPRequest(DateTime     Timestamp,
                                                        HTTPAPI      API,
                                                        HTTPRequest  Request)

            => OnLoginOptions.WhenAll(Timestamp,
                                      API ?? this,
                                      Request);

        #endregion

        #region (protected internal) LoginOptionsHTTPResponse    (Response)

        /// <summary>
        /// An event sent whenever a LoginOptions response was sent.
        /// </summary>
        public HTTPResponseLogEvent OnLoginOptionsResponse = new ();

        /// <summary>
        /// An event sent whenever a LoginOptions response was sent.
        /// </summary>
        /// <param name="Timestamp">The timestamp of the request.</param>
        /// <param name="API">The HTTP API.</param>
        /// <param name="Request">An HTTP request.</param>
        /// <param name="Response">An HTTP response.</param>
        protected internal Task LoginOptionsHTTPResponse(DateTime      Timestamp,
                                                         HTTPAPI       API,
                                                         HTTPRequest   Request,
                                                         HTTPResponse  Response)

            => OnLoginOptionsResponse.WhenAll(Timestamp,
                                              API ?? this,
                                              Request,
                                              Response);

        #endregion


        #region (protected internal) LoginHTTPRequest            (Request)

        /// <summary>
        /// An event sent whenever a Login request was received.
        /// </summary>
        public HTTPRequestLogEvent OnLogin = new ();

        /// <summary>
        /// An event sent whenever a Login request was received.
        /// </summary>
        /// <param name="Timestamp">The timestamp of the request.</param>
        /// <param name="API">The HTTP API.</param>
        /// <param name="Request">An HTTP request.</param>
        protected internal Task LoginHTTPRequest(DateTime     Timestamp,
                                                 HTTPAPI      API,
                                                 HTTPRequest  Request)

            => OnLogin.WhenAll(Timestamp,
                               API ?? this,
                               Request);

        #endregion

        #region (protected internal) LoginHTTPResponse           (Response)

        /// <summary>
        /// An event sent whenever a Login response was sent.
        /// </summary>
        public HTTPResponseLogEvent OnLoginResponse = new ();

        /// <summary>
        /// An event sent whenever a Login response was sent.
        /// </summary>
        /// <param name="Timestamp">The timestamp of the request.</param>
        /// <param name="API">The HTTP API.</param>
        /// <param name="Request">An HTTP request.</param>
        /// <param name="Response">An HTTP response.</param>
        protected internal Task LoginHTTPResponse(DateTime      Timestamp,
                                                  HTTPAPI       API,
                                                  HTTPRequest   Request,
                                                  HTTPResponse  Response)

            => OnLoginResponse.WhenAll(Timestamp,
                                       API ?? this,
                                       Request,
                                       Response);

        #endregion

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create an instance of the Passkey API.
        /// </summary>
        /// <param name="HTTPServerName">The default HTTP server name, used whenever no HTTP Host-header had been given.</param>
        /// <param name="URLPathPrefix">A common prefix for all URLs.</param>
        /// 
        /// <param name="ServerCertificateSelector">An optional delegate to select a SSL/TLS server certificate.</param>
        /// <param name="ClientCertificateValidator">An optional delegate to verify the SSL/TLS client certificate used for authentication.</param>
        /// <param name="AllowedTLSProtocols">The SSL/TLS protocol(s) allowed for this connection.</param>
        /// 
        /// <param name="MaintenanceEvery">The maintenance interval.</param>
        /// <param name="DisableMaintenanceTasks">Disable all maintenance tasks.</param>
        /// 
        /// <param name="SkipURLTemplates">Skip URI templates.</param>
        /// <param name="DisableNotifications">Disable external notifications.</param>
        /// <param name="DisableLogging">Disable the log file.</param>
        /// <param name="LoggingPath">The path for all logfiles.</param>
        /// <param name="LogfileName">The name of the logfile for this API.</param>
        /// <param name="DNSClient">The DNS client of the API.</param>
        /// <param name="Autostart">Whether to start the API automatically.</param>
        public PasskeyAPI(HTTPHostname?                                              HTTPHostname                     = null,
                          String?                                                    ExternalDNSName                  = null,
                          IPPort?                                                    HTTPServerPort                   = null,
                          HTTPPath?                                                  BasePath                         = null,
                          String                                                     HTTPServerName                   = "Passkey API",

                          HTTPPath?                                                  URLPathPrefix                    = null,
                          String                                                     HTTPServiceName                  = "Passkey API",
                          String?                                                    HTMLTemplate                     = null,
                          JObject?                                                   APIVersionHashes                 = null,

                          ServerCertificateSelectorDelegate?                         ServerCertificateSelector        = null,
                          RemoteTLSClientCertificateValidationHandler<IHTTPServer>?  ClientCertificateValidator       = null,
                          LocalCertificateSelectionHandler?                          LocalCertificateSelector         = null,
                          SslProtocols?                                              AllowedTLSProtocols              = null,
                          Boolean?                                                   ClientCertificateRequired        = null,
                          Boolean?                                                   CheckCertificateRevocation       = null,

                          ServerThreadNameCreatorDelegate?                           ServerThreadNameCreator          = null,
                          ServerThreadPriorityDelegate?                              ServerThreadPrioritySetter       = null,
                          Boolean?                                                   ServerThreadIsBackground         = null,
                          ConnectionIdBuilder?                                       ConnectionIdBuilder              = null,
                          TimeSpan?                                                  ConnectionTimeout                = null,
                          UInt32?                                                    MaxClientConnections             = null,

                          Organization_Id?                                           AdminOrganizationId              = null,
                          EMailAddress?                                              APIRobotEMailAddress             = null,
                          String?                                                    APIRobotGPGPassphrase            = null,
                          ISMTPClient?                                               SMTPClient                       = null,

                          PasswordQualityCheckDelegate?                              PasswordQualityCheck             = null,
                          HTTPCookieName?                                            CookieName                       = null,
                          Boolean                                                    UseSecureCookies                 = true,
                          Languages?                                                 DefaultLanguage                  = null,

                          Boolean?                                                   DisableMaintenanceTasks          = null,
                          TimeSpan?                                                  MaintenanceInitialDelay          = null,
                          TimeSpan?                                                  MaintenanceEvery                 = null,

                          Boolean?                                                   DisableWardenTasks               = null,
                          TimeSpan?                                                  WardenInitialDelay               = null,
                          TimeSpan?                                                  WardenCheckEvery                 = null,

                          IEnumerable<URLWithAPIKey>?                                RemoteAuthServers                = null,
                          IEnumerable<APIKey_Id>?                                    RemoteAuthAPIKeys                = null,

                          Boolean?                                                   IsDevelopment                    = null,
                          IEnumerable<String>?                                       DevelopmentServers               = null,
                          Boolean                                                    SkipURLTemplates                 = false,
                          String?                                                    DatabaseFileName                 = DefaultBCTAPI_DatabaseFileName,
                          Boolean                                                    DisableNotifications             = false,
                          Boolean                                                    DisableLogging                   = false,
                          String?                                                    LoggingPath                      = null,
                          String?                                                    LogfileName                      = DefaultBCTAPI_LogfileName,
                          LogfileCreatorDelegate?                                    LogfileCreator                   = null,
                          DNSClient?                                                 DNSClient                        = null,
                          Boolean                                                    Autostart                        = false)

            : base(HTTPHostname,
                   ExternalDNSName,
                   HTTPServerPort  ?? DefaultHTTPServerPort,
                   BasePath,
                   HTTPServerName  ?? DefaultHTTPServerName,

                   URLPathPrefix,
                   HTTPServiceName ?? DefaultHTTPServerName,
                   HTMLTemplate,
                   APIVersionHashes,

                   ServerCertificateSelector,
                   ClientCertificateValidator,
                   LocalCertificateSelector,
                   AllowedTLSProtocols,
                   ClientCertificateRequired,
                   CheckCertificateRevocation,

                   ServerThreadNameCreator,
                   ServerThreadPrioritySetter,
                   ServerThreadIsBackground,
                   ConnectionIdBuilder,
                   ConnectionTimeout,
                   MaxClientConnections,

                   AdminOrganizationId,
                   APIRobotEMailAddress  ?? new EMailAddress(
                                                "Passkey",
                                                SimpleEMailAddress.Parse("robot@example.org")
                                                //OpenPGP.ReadSecretKeyRing(File.OpenRead(Path.Combine(AppContext.BaseDirectory, "robot@offenes-jena_secring.gpg"))),
                                                //OpenPGP.ReadPublicKeyRing(File.OpenRead(Path.Combine(AppContext.BaseDirectory, "robot@offenes-jena_pubring.gpg")))
                                            ),
                   APIRobotGPGPassphrase ?? "!",
                   SMTPClient,

                   PasswordQualityCheck,
                   CookieName ?? HTTPCookieName.Parse(nameof(PasskeyAPI)),
                   UseSecureCookies,
                   TimeSpan.FromDays(36524), // ~100 Jahre
                   DefaultLanguage ?? Languages.en,
                   4,
                   4,
                   4,
                   5,
                   20,
                   8,
                   4,
                   4,
                   8,
                   8,
                   8,
                   8,

                   DisableMaintenanceTasks,
                   MaintenanceInitialDelay,
                   MaintenanceEvery,

                   DisableWardenTasks,
                   WardenInitialDelay,
                   WardenCheckEvery,

                   RemoteAuthServers,
                   RemoteAuthAPIKeys,

                   true, //IsDevelopment,
                   DevelopmentServers,
                   SkipURLTemplates,
                   DatabaseFileName,
                   true, //DisableNotifications,
                   true, //DisableLogging,
                   LoggingPath,
                   LogfileName,
                   LogfileCreator,
                   DNSClient,
                   false)

        {

            //this.HTMLTemplate = GetResourceString("template.html");

            if (!SkipURLTemplates)
                RegisterURLTemplates();

            if (Autostart)
                Start().Wait();

        }

        #endregion


        #region (private)   GenerateCookieUserData(ValidUser, Astronaut = null)

        private String GenerateCookieUserData(IUser   User,
                                              IUser?  Astronaut  = null)

            => String.Concat("=login=",            User.     Id.      ToString().ToBase64(),
                             Astronaut is not null
                                 ? ":astronaut=" + Astronaut.Id.      ToString().ToBase64()
                                 : String.Empty,
                             ":username=",         User.Name.FirstText().ToBase64(),
                             ":email=",            User.EMail.Address.ToString().ToBase64(),
                             ":language=",         User.UserLanguage. AsText().  ToBase64(),
                             IsAdmin(User) == Access_Levels.ReadOnly  ? ":isAdminRO" : String.Empty,
                             IsAdmin(User) == Access_Levels.ReadWrite ? ":isAdminRW" : String.Empty);


        #endregion

        #region (private)   GenerateCookieSettings(Expires)

        private String GenerateCookieSettings(DateTime Expires)

            => String.Concat("; Expires=",  Expires.ToRFC1123(),
                             HTTPCookieDomain.IsNotNullOrEmpty()
                                 ? "; Domain=" + HTTPCookieDomain
                                 : String.Empty,
                             "; Path=",     URLPathPrefix.ToString(),
                             "; SameSite=strict",
                             UseSecureCookies
                                 ? "; secure"
                                 : String.Empty);

        #endregion

        #region CheckHTTPCookie(Request,         RemoteAuthServersMaxHopCount = 0)

        public async Task<IUser?> CheckHTTPCookie(HTTPRequest Request,
                                                  Byte?       RemoteAuthServersMaxHopCount = 0)
        {

            if (TryGetSecurityTokenFromCookie(Request, out var securityTokenId))
            {

                var securityToken = await CheckHTTPCookie(securityTokenId,
                                                          RemoteAuthServersMaxHopCount);

                if (securityToken is not null &&
                    _TryGetUser(securityToken.UserId, out var user))
                {
                    return user;
                }

            }

            return null;

        }

        #endregion



        private readonly WebAuthnService webAuthnService = new (
                                                               Name:     "Hermod Passkey Tests",
                                                               Hostname: "localhost"
                                                           );


        #region (private) RegisterURLTemplates()

        #region Manage HTTP Resources

        private readonly Tuple<String, Assembly>[] resourceAssemblies = [
            new Tuple<String, Assembly>(PasskeyAPI.HTTPRoot, typeof(PasskeyAPI).Assembly),
            new Tuple<String, Assembly>(HTTPExtAPI.HTTPRoot, typeof(HTTPExtAPI).Assembly),
            new Tuple<String, Assembly>(HTTPAPI.   HTTPRoot, typeof(HTTPAPI).   Assembly)
        ];

        #region (protected override) GetResourceStream      (ResourceName)

        protected override Stream? GetResourceStream(String ResourceName)

            => GetResourceStream(
                   ResourceName,
                   resourceAssemblies
               );

        #endregion

        #region (protected override) GetResourceMemoryStream(ResourceName)

        protected override MemoryStream? GetResourceMemoryStream(String ResourceName)

            => GetResourceMemoryStream(
                   ResourceName,
                   resourceAssemblies
               );

        #endregion

        #region (protected override) GetResourceString      (ResourceName)

        protected override String GetResourceString(String ResourceName)

            => GetResourceString(
                   ResourceName,
                   resourceAssemblies
               );

        #endregion

        #region (protected override) GetResourceBytes       (ResourceName)

        protected override Byte[] GetResourceBytes(String ResourceName)

            => GetResourceBytes(
                   ResourceName,
                   resourceAssemblies
               );

        #endregion

        #region (protected override) MixWithHTMLTemplate    (ResourceName)

        protected override String MixWithHTMLTemplate(String ResourceName)

            => MixWithHTMLTemplate(
                   ResourceName,
                   resourceAssemblies
               );

        #endregion

        #endregion

        private void RegisterURLTemplates()
        {

            HTTPServer.AddAuth(request => {

                // Allow anonymous access...
                return Anonymous;

            });


            #region /webauthn/register/options

            #region GET  /webauthn/register/options

            HTTPServer.AddMethodCallback(
                HTTPHostname.Any,
                HTTPMethod.GET,
                URLPathPrefix + "/webauthn/register/options",
                HTTPRequestLogger:   RegisterOptionsHTTPRequest,
                HTTPResponseLogger:  RegisterOptionsHTTPResponse,
                HTTPDelegate:  async request => {

                    var username = request.QueryString.GetString("username");

                    if (username.IsNullOrEmpty() || !User_Id.TryParse(username, out var userId))
                        return new HTTPResponse.Builder(request) {
                                       HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                       ContentType     = HTTPContentType.Text.PLAIN,
                                       Content         = "Invalid username!".ToUTF8Bytes(),
                                       Connection      = ConnectionType.Close
                                   }.AsImmutable;

                    if (!TryGetUser(userId, out var user))
                    {

                        user = new User(
                                   userId,
                                   I18NString.Create(username),
                                   SimpleEMailAddress.Parse(username)
                               );

                        await AddUser(user, true, true, true);

                    }

                    var options  = webAuthnService.GenerateRegistrationOptions(user);

                    // {
                    //   "challenge": "MGKsCCrae/9LI+6ZBVWA1DeEcVSb321M+G3UPXfd3GM=",
                    //   "rp": {
                    //     "id":   "localhost",
                    //     "name": "Hermod Passkey Tests"
                    //   },
                    //   "user": {
                    //     "id":          "YWh6ZjM=",
                    //     "name":        "ahzf",
                    //     "displayName": "ahzf"
                    //   },
                    //   "pubKeyCredParams": [
                    //     {
                    //       "type": "public-key",
                    //       "alg": -7
                    //     },
                    //     {
                    //       "type": "public-key",
                    //       "alg": -257
                    //     }
                    //   ],
                    //   "timeout": 60000,
                    //   "attestation": "direct"
                    // }

                    return new HTTPResponse.Builder(request) {
                               HTTPStatusCode  = HTTPStatusCode.OK,
                               ContentType     = HTTPContentType.Application.JSON_UTF8,
                               Content         = options.ToJSON().ToUTF8Bytes(),
                               Connection      = ConnectionType.Close
                           }.AsImmutable;

                }
            );

            #endregion

            #endregion

            #region /webauthn/register

            #region POST /webauthn/register

            HTTPServer.AddMethodCallback(
                HTTPHostname.Any,
                HTTPMethod.POST,
                URLPathPrefix + "/webauthn/register",
                HTTPRequestLogger:   RegisterHTTPRequest,
                HTTPResponseLogger:  RegisterHTTPResponse,
                HTTPDelegate:  async request => {

                    var username = request.QueryString.GetString("username");

                    if (username.IsNullOrEmpty() || !User_Id.TryParse(username, out var userId))
                        return new HTTPResponse.Builder(request) {
                                       HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                       ContentType     = HTTPContentType.Text.PLAIN,
                                       Content         = "Invalid username!".ToUTF8Bytes(),
                                       Connection      = ConnectionType.Close
                                   }.AsImmutable;

                    // {
                    //     "id":     "fkkiMSQ_jZKmIVNKKoKHexrPm4WWsuucLr0NQNLA7AA",
                    //     "rawId":  "fkkiMSQ/jZKmIVNKKoKHexrPm4WWsuucLr0NQNLA7AA=",
                    //     "type":   "public-key",
                    //     "response": {
                    //         "clientDataJSON":    "eyJ0eXBlIjoid2ViYXV0aG4uY3JlYXRlIiwiY2hhbGxlbmdlIjoiTUdLc0NDcmFlXzlMSS02WkJWV0ExRGVFY1ZTYjMyMU0tRzNVUFhmZDNHTSIsIm9yaWdpbiI6Imh0dHA6Ly9sb2NhbGhvc3Q6OTAwMCIsImNyb3NzT3JpZ2luIjpmYWxzZX0=",
                    //         "attestationObject": "o2NmbXRkbm9uZWdhdHRTdG10oGhhdXRoRGF0YVikSZYN5YgOjGh0NBcPZHZgW4/krrmihjLHmVzzuoMdl2NFAAAAAAiYcFjK3EuBtuEw3lDcvpYAIH5JIjEkP42SpiFTSiqCh3saz5uFlrLrnC69DUDSwOwApQECAyYgASFYIKj3y+VlaO6W0kHo76+zacPd9D8GeTBT1nrO1zYxMyfkIlggUYXL2LXxP9E//G1Cf1oHa3exHwyU1wsVtrwo2eOv6NY="
                    //     }
                    // }

                    if (!request.TryParseJSONObjectRequestBody(out var json, out var errResp))
                        return errResp.AsImmutable;

                    if (!CredentialRegistrationData.TryParse(json, out var credentialData, out var errorResponse))
                        return new HTTPResponse.Builder(request) {
                                       HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                       ContentType     = HTTPContentType.Application.JSON_UTF8,
                                       Content         = errorResponse.ToUTF8Bytes(),
                                       Connection      = ConnectionType.Close
                                   }.AsImmutable;


                    var result = webAuthnService.VerifyAndRegisterCredential(
                                     userId,
                                     credentialData
                                 );


                    await Task.Delay(10);


                    if (result.IsSuccess)
                        return new HTTPResponse.Builder(request) {
                                   HTTPStatusCode  = HTTPStatusCode.OK,
                                   Connection      = ConnectionType.Close
                               }.AsImmutable;

                    return new HTTPResponse.Builder(request) {
                                   HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                   ContentType     = HTTPContentType.Application.JSON_UTF8,
                                   Content         = result.ErrorMessage.ToUTF8Bytes(),
                                   Connection      = ConnectionType.Close
                               }.AsImmutable;

                }
            );

            #endregion

            #endregion



            #region /webauthn/login/options

            #region GET  /webauthn/login/options

            HTTPServer.AddMethodCallback(
                HTTPHostname.Any,
                HTTPMethod.GET,
                URLPathPrefix + "/webauthn/login/options",
                HTTPRequestLogger:   LoginOptionsHTTPRequest,
                HTTPResponseLogger:  LoginOptionsHTTPResponse,
                HTTPDelegate:  async request => {

                    IEnumerable<PublicKeyCredentialDescriptor>?  credentials   = null;
                    User_Id?                                     userId        = null;
                    IUser?                                       user          = null;

                    var userlogin = request.QueryString.GetString("userlogin");
                    if (userlogin.IsNotNullOrEmpty())
                    {

                        if (!User_Id.TryParse(userlogin, out var _userId))
                            return new HTTPResponse.Builder(request) {
                                           HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                           ContentType     = HTTPContentType.Text.PLAIN,
                                           Content         = "Invalid login!".ToUTF8Bytes(),
                                           Connection      = ConnectionType.Close
                                       }.AsImmutable;

                        if (!TryGetUser(_userId, out var _user))
                            return new HTTPResponse.Builder(request) {
                                           HTTPStatusCode  = HTTPStatusCode.NotFound,
                                           ContentType     = HTTPContentType.Text.PLAIN,
                                           Content         = "Unknown user!".ToUTF8Bytes(),
                                           Connection      = ConnectionType.Close
                                       }.AsImmutable;

                        userId       = _userId;
                        user         = _user;
                        credentials  = webAuthnService.
                                           GetStoredCredentials(_userId).
                                           Select(storedCredential => new PublicKeyCredentialDescriptor(
                                                                          Type:        PublicKeyCredentialType.PublicKey,
                                                                          Id:          storedCredential.CredentialId.FromBASE64URL(),
                                                                          Transports:  []
                                                                      ));

                    }

                    var challenge    = new Byte[32];
                    RandomNumberGenerator.Fill(challenge);

                    if (user is not null)
                        webAuthnService.StoreChallengeForLogin(user.Id, challenge);

                    var options      = new PublicKeyCredentialRequestOptions(
                                           Challenge:         challenge,
                                           RelyingPartyId:    webAuthnService.Hostname,
                                           Timeout:           TimeSpan.FromSeconds(60),
                                           AllowCredentials:  credentials,
                                           UserVerification:  UserVerificationRequirement.Preferred
                                       );


                    await Task.Delay(10);


                    return new HTTPResponse.Builder(request) {
                                   HTTPStatusCode  = HTTPStatusCode.OK,
                                   ContentType     = HTTPContentType.Application.JSON_UTF8,
                                   Content         = options.ToJSON().ToUTF8Bytes(),
                                   Connection      = ConnectionType.Close
                               }.AsImmutable;


                }
            );

            #endregion

            #endregion

            #region /webauthn/login

            #region POST /webauthn/login

            HTTPServer.AddMethodCallback(
                HTTPHostname.Any,
                HTTPMethod.POST,
                URLPathPrefix + "/webauthn/login",
                HTTPRequestLogger:   LoginHTTPRequest,
                HTTPResponseLogger:  LoginHTTPResponse,
                HTTPDelegate:  async request => {

                    // {
                    //     "id":    "KKiLy9tkCw6vpsTaLNO0Kp0grtsQvUjqTULL1yra-I8",
                    //     "rawId": "KKiLy9tkCw6vpsTaLNO0Kp0grtsQvUjqTULL1yra+I8=",
                    //     "type":  "public-key",
                    //     "response": {
                    //         "clientDataJSON":    "eyJ0eXBlIjoid2ViYXV0aG4uZ2V0IiwiY2hhbGxlbmdlIjoiTXVqblhwQ2xncjhJQlFlbnBidnF5aDdBU1QxYktwdXJ1VzVzdE5Fc05sSSIsIm9yaWdpbiI6Imh0dHA6Ly9sb2NhbGhvc3Q6OTAwMCIsImNyb3NzT3JpZ2luIjpmYWxzZSwib3RoZXJfa2V5c19jYW5fYmVfYWRkZWRfaGVyZSI6ImRvIG5vdCBjb21wYXJlIGNsaWVudERhdGFKU09OIGFnYWluc3QgYSB0ZW1wbGF0ZS4gU2VlIGh0dHBzOi8vZ29vLmdsL3lhYlBleCJ9",
                    //         "authenticatorData": "SZYN5YgOjGh0NBcPZHZgW4/krrmihjLHmVzzuoMdl2MFAAAAAg==",
                    //         "signature":         "MEUCIQCKOhqUMaB4mhv4Gzyv4WzcifGB1IeBolKreLP72kuawwIgaeK0ZD6/f9bZvl42Y0I7sJ2OZJVfvs1oraEmcnvZAWM=",
                    //         "userHandle":        "SzM1RVVCUDMyMU45NVM0"
                    //     }
                    // }

                    if (!request.TryParseJSONObjectRequestBody(out var json, out var errResp))
                        return errResp.AsImmutable;

                    var userId = webAuthnService.GetUserForCredentialId(json["id"]?.Value<String>() ?? "");
                    if (!userId.HasValue)
                        return new HTTPResponse.Builder(request) {
                                       HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                       ContentType     = HTTPContentType.Text.PLAIN,
                                       Content         = "Invalid username!".ToUTF8Bytes(),
                                       Connection      = ConnectionType.Close
                                   }.AsImmutable;

                    if (!TryGetUser(userId.Value, out var user))
                        return new HTTPResponse.Builder(request) {
                                       HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                       ContentType     = HTTPContentType.Text.PLAIN,
                                       Content         = "Invalid username!".ToUTF8Bytes(),
                                       Connection      = ConnectionType.Close
                                   }.AsImmutable;

                    var expectedChallenge = webAuthnService.GetStoredChallenge(userId.Value);
                    if (expectedChallenge is null || expectedChallenge.Length == 0)
                        return new HTTPResponse.Builder(request) {
                                       HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                       ContentType     = HTTPContentType.Text.PLAIN,
                                       Content         = "Invalid challenge!".ToUTF8Bytes(),
                                       Connection      = ConnectionType.Close
                                   }.AsImmutable;

                    var clientDataText = json["response"]?["clientDataJSON"]?.Value<String>() ?? "";
                    if (clientDataText.IsNullOrEmpty())
                        return new HTTPResponse.Builder(request) {
                                       HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                       ContentType     = HTTPContentType.Text.PLAIN,
                                       Content         = "Invalid client data!".ToUTF8Bytes(),
                                       Connection      = ConnectionType.Close
                                   }.AsImmutable;

                    JObject? clientDataJSON = null;
                    try
                    {

                        // {
                        //   "type":                          "webauthn.get",
                        //   "challenge":                     "NfoMKWpyBaNnzWmtPZk6Xut7lirgad4W8iz6JrEqB5E",
                        //   "origin":                        "http://localhost:9000",
                        //   "crossOrigin":                    false,
                        //   "other_keys_can_be_added_here":  "do not compare clientDataJSON against a template. See https://goo.gl/yabPex"
                        // }

                        clientDataJSON = JObject.Parse(clientDataText.FromBASE64().ToUTF8String());

                    }
                    catch (Exception e)
                    {
                        return new HTTPResponse.Builder(request) {
                                       HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                       ContentType     = HTTPContentType.Text.PLAIN,
                                       Content         = "Invalid client data JSON!".ToUTF8Bytes(),
                                       Connection      = ConnectionType.Close
                                   }.AsImmutable;
                    }

                    #region Mandatory security checks

                    if (clientDataJSON["type"]?.Value<String>() != "webauthn.get")
                        return new HTTPResponse.Builder(request) {
                                       HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                       ContentType     = HTTPContentType.Text.PLAIN,
                                       Content         = "Invalid client data type!".ToUTF8Bytes(),
                                       Connection      = ConnectionType.Close
                                   }.AsImmutable;

                    if (expectedChallenge.ToBase64URL() != clientDataJSON["challenge"]?.Value<String>())
                        return new HTTPResponse.Builder(request) {
                                       HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                       ContentType     = HTTPContentType.Text.PLAIN,
                                       Content         = $"Invalid challenge '{expectedChallenge.ToBase64()}' != '{clientDataJSON["challenge"]?.Value<String>()}'!".ToUTF8Bytes(),
                                       Connection      = ConnectionType.Close
                                   }.AsImmutable;

                    if (!webAuthnService.ValidateHostname(clientDataJSON["origin"]?.Value<String>() ?? ""))
                        return new HTTPResponse.Builder(request) {
                                       HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                       ContentType     = HTTPContentType.Text.PLAIN,
                                       Content         = $"Invalid origin '{webAuthnService.Hostname}' != '{clientDataJSON["origin"]?.Value<String>()}'!".ToUTF8Bytes(),
                                       Connection      = ConnectionType.Close
                                   }.AsImmutable;

                    var topOrigin = clientDataJSON["topOrigin"]?.Value<String>();

                    // Check Sign-Counter > last login counter!

                    #endregion

                    var storedCredential = webAuthnService.GetCredential(json["id"]?.Value<String>() ?? "");
                    if (storedCredential is null)
                        return new HTTPResponse.Builder(request) {
                                       HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                       ContentType     = HTTPContentType.Text.PLAIN,
                                       Content         = "Invalid credential identification!".ToUTF8Bytes(),
                                       Connection      = ConnectionType.Close
                                   }.AsImmutable;


                    var authenticatorData  = json["response"]?["authenticatorData"]?.Value<String>()?.FromBASE64() ?? [];
                    var signature          = json["response"]?["signature"]?.        Value<String>()?.FromBASE64() ?? [];

                    var isValid            = WebAuthnService.VerifyAssertionSignature(
                                                 storedCredential.PublicKey,
                                                 authenticatorData,
                                                 clientDataText.FromBASE64(),
                                                 signature
                                             );

                    if (!isValid)
                        return new HTTPResponse.Builder(request) {
                                       HTTPStatusCode  = HTTPStatusCode.BadRequest,
                                       ContentType     = HTTPContentType.Text.PLAIN,
                                       Content         = $"Invalid challenge '{expectedChallenge.ToBase64()}' != '{clientDataJSON["challenge"]?.Value<String>()}'!".ToUTF8Bytes(),
                                       Connection      = ConnectionType.Close
                                   }.AsImmutable;


                    //UpdateSignCount(storedCredential, authenticatorData);


                    var securityTokenId  = SecurityToken_Id.Parse(
                                               SHA256.HashData(
                                                   String.Concat(
                                                       Guid.NewGuid().ToString(),
                                                       user.Id
                                                   ).
                                                   ToUTF8Bytes()
                                               ).ToHexString()
                                           );

                    var expires          = Timestamp.Now.Add(MaxSignInSessionLifetime);

                    httpCookies.TryAdd(
                        securityTokenId,
                        new SecurityToken(
                            user.Id,
                            expires
                        )
                    );

                    await Task.Delay(10);

                    //ToDo: Forward to protected page!
                    return new HTTPResponse.Builder(request) {
                               HTTPStatusCode  = HTTPStatusCode.OK,
                               ContentType     = HTTPContentType.Text.PLAIN,
                               Content         = "Hello world!".ToUTF8Bytes(),
                               CacheControl    = "private",
                               SetCookie       = HTTPCookies.Parse(

                                                     String.Concat(CookieName,
                                                                   GenerateCookieUserData(user),
                                                                   GenerateCookieSettings(expires)),

                                                     String.Concat(SessionCookieName, "=", securityTokenId.ToString(),
                                                                   GenerateCookieSettings(expires),
                                                                   "; HttpOnly")

                                                 ),
                               Connection      = ConnectionType.Close,
                               X_FrameOptions  = "DENY"
                           }.AsImmutable;

                }
            );

            #endregion

            #endregion


            // 

        }

        #endregion


    }

}
