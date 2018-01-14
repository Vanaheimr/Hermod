using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

using org.GraphDefined.Vanaheimr.Styx.Arrows;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using System.Security.Cryptography.X509Certificates;

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    public interface IHTTPServer : IDisposable
    {

        string DefaultServerName { get; }
        HTTPSecurity HTTPSecurity { get; }

        DNSClient DNSClient { get; }

        /// <summary>
        /// The X509 certificate.
        /// </summary>
        X509Certificate2 X509Certificate { get; }

        /// <summary>
        /// Is the server already started?
        /// </summary>
        Boolean IsStarted { get; }

        /// <summary>
        /// The current number of attached TCP clients.
        /// </summary>
        UInt64 NumberOfClients { get; }


        event AccessLogHandler AccessLog;
        event ErrorLogHandler ErrorLog;
        event BoomerangSenderHandler<string, DateTime, HTTPRequest, HTTPResponse> OnNotification;
        event RequestLogHandler RequestLog;

        HTTPEventSource AddEventSource(String EventIdentification, uint MaxNumberOfCachedEvents, TimeSpan? RetryIntervall = default(TimeSpan?), Func<String, DateTime, String> LogfileName = null);

        HTTPEventSource AddEventSource(String                          EventIdentification,
                                       String                          URITemplate,

                                       UInt32                          MaxNumberOfCachedEvents     = 500,
                                       TimeSpan?                       RetryIntervall              = null,
                                       Boolean                         EnableLogging               = false,
                                       Func<String, DateTime, String>  LogfileName                 = null,
                                       String                          LogfileReloadSearchPattern  = null,

                                       HTTPHostname                    Hostname                    = null,
                                       HTTPMethod                      HTTPMethod                  = null,
                                       HTTPContentType                 HTTPContentType             = null,

                                       HTTPAuthentication              HostAuthentication          = null,
                                       HTTPAuthentication              URIAuthentication           = null,
                                       HTTPAuthentication              HTTPMethodAuthentication    = null,

                                       HTTPDelegate                    DefaultErrorHandler         = null);

        void AddMethodCallback(HTTPHostname Hostname, HTTPMethod HTTPMethod, IEnumerable<string> URITemplates, HTTPContentType HTTPContentType = null, HTTPAuthentication HostAuthentication = null, HTTPAuthentication URIAuthentication = null, HTTPAuthentication HTTPMethodAuthentication = null, HTTPAuthentication ContentTypeAuthentication = null, HTTPDelegate HTTPDelegate = null, URIReplacement AllowReplacement = URIReplacement.Fail);
        void AddMethodCallback(HTTPHostname Hostname, HTTPMethod HTTPMethod, IEnumerable<string> URITemplates, IEnumerable<HTTPContentType> HTTPContentTypes, HTTPAuthentication HostAuthentication = null, HTTPAuthentication URIAuthentication = null, HTTPAuthentication HTTPMethodAuthentication = null, HTTPAuthentication ContentTypeAuthentication = null, HTTPDelegate HTTPDelegate = null, URIReplacement AllowReplacement = URIReplacement.Fail);
        void AddMethodCallback(HTTPHostname Hostname, HTTPMethod HTTPMethod, string URITemplate, HTTPContentType HTTPContentType = null, HTTPAuthentication HostAuthentication = null, HTTPAuthentication URIAuthentication = null, HTTPAuthentication HTTPMethodAuthentication = null, HTTPAuthentication ContentTypeAuthentication = null, HTTPDelegate HTTPDelegate = null, URIReplacement AllowReplacement = URIReplacement.Fail);
        void AddMethodCallback(HTTPHostname Hostname, HTTPMethod HTTPMethod, string URITemplate, IEnumerable<HTTPContentType> HTTPContentTypes, HTTPAuthentication HostAuthentication = null, HTTPAuthentication URIAuthentication = null, HTTPAuthentication HTTPMethodAuthentication = null, HTTPAuthentication ContentTypeAuthentication = null, HTTPDelegate HTTPDelegate = null, URIReplacement AllowReplacement = URIReplacement.Fail);
        IHTTPServer AttachTCPPort(IPPort Port);
        IHTTPServer AttachTCPPorts(params IPPort[] Ports);
        IHTTPServer AttachTCPSocket(IPSocket Socket);
        IHTTPServer AttachTCPSockets(params IPSocket[] Sockets);
        IHTTPServer DetachTCPPort(IPPort Port);
        IHTTPServer DetachTCPPorts(params IPPort[] Ports);
        Tuple<MethodInfo, IEnumerable<object>> GetErrorHandler(string Host, string URL, HTTPMethod HTTPMethod = null, HTTPContentType HTTPContentType = null, HTTPStatusCode HTTPStatusCode = null);
        HTTPEventSource GetEventSource(string EventSourceIdentification);
        IEnumerable<HTTPEventSource> GetEventSources(Func<HTTPEventSource, bool> EventSourceSelector = null);
        Task<HTTPResponse> InvokeHandler(HTTPRequest Request);
        void Redirect(HTTPHostname Hostname, HTTPMethod HTTPMethod, string URITemplate, HTTPContentType HTTPContentType, string URITarget);
        void Redirect(HTTPMethod HTTPMethod, string URITemplate, HTTPContentType HTTPContentType, string URITarget);

        void AddFilter(HTTPFilter1Delegate Filter);
        void AddFilter(HTTPFilter2Delegate Filter);

        void Rewrite(HTTPRewrite1Delegate Rewrite);
        void Rewrite(HTTPRewrite2Delegate Rewrite);

        bool TryGetEventSource(string EventSourceIdentification, out HTTPEventSource EventSource);
        void UseEventSource(string EventSourceIdentification, Action<HTTPEventSource> Action);
        void UseEventSource<T>(string EventSourceIdentification, IEnumerable<T> DataSource, Action<HTTPEventSource, T> Action);

        void Start();
        void Start(TimeSpan Delay, Boolean InBackground = true);
        void Shutdown(String Message = null, Boolean Wait = true);

    }

}