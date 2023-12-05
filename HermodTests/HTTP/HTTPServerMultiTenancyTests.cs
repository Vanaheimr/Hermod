using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eu.Vanaheimr.Hermod.UnitTests
{
    class HTTPServerMultiTenancyTests
    {


        //            // HTTP methods...
        //            HTTPServer01.AddMethodCallback(HTTPMethod.GET,
        //                                           "localhost:2000",    // Check rfc!
        ////                                           "/test",
        //                                           HTTPDelegate: Request => {

        //                                               // Cache-control:max-age=300
        //                                               // Content-Encoding:gzip
        //                                               // Expires:Fri, 04 Jul 2014 23:05:04 GMT
        //                                               // Vary:User-Agent,Accept-Encoding

        //                                               return new HTTPResponseBuilder() {
        //                                                   HTTPStatusCode  = HTTPStatusCode.OK,
        //                                                   ContentType     = HTTPContentType.Text.TEXT_UTF8,
        //                                                   Content         = String.Concat("Hello world on port 2000 /!", Environment.NewLine, Thread.CurrentThread.ManagedThreadId).ToUTF8Bytes(),
        //                                                   Server          = "Hermod",
        //                                                   Connection      = "close"
        //                                               };

        //                                           });

        //            HTTPServer01.AddMethodCallback(HTTPMethod.GET,
        //                                           "localhost:2002",    // Check rfc!
        ////                                           "/test",
        //                                           HTTPDelegate: Request => {

        //                                               // Cache-control:max-age=300
        //                                               // Content-Encoding:gzip
        //                                               // Expires:Fri, 04 Jul 2014 23:05:04 GMT
        //                                               // Vary:User-Agent,Accept-Encoding

        //                                               return new HTTPResponseBuilder() {
        //                                                   HTTPStatusCode  = HTTPStatusCode.OK,
        //                                                   ContentType     = HTTPContentType.Text.TEXT_UTF8,
        //                                                   Content         = String.Concat("Hello world on port 2002 /!", Environment.NewLine, Thread.CurrentThread.ManagedThreadId).ToUTF8Bytes(),
        //                                                   Server          = "Hermod",
        //                                                   Connection      = "close"
        //                                               };

        //                                           });

        //            HTTPServer01.AddMethodCallback(HTTPMethod.GET,
        //                                           "localhost:2000",    // Check rfc!
        //                                           "/tests/{2000}",
        //                                           HTTPDelegate: Request =>
        //                                           {

        //                                               // Cache-control:max-age=300
        //                                               // Content-Encoding:gzip
        //                                               // Expires:Fri, 04 Jul 2014 23:05:04 GMT
        //                                               // Vary:User-Agent,Accept-Encoding

        //                                               return new HTTPResponseBuilder()
        //                                               {
        //                                                   HTTPStatusCode  = HTTPStatusCode.OK,
        //                                                   ContentType     = HTTPContentType.Text.TEXT_UTF8,
        //                                                   Content = String.Concat("Hello world on port 2000 /tests/{id} with id == " + Request.ParsedQueryParameters.FirstOrDefault() + "!", Environment.NewLine, Thread.CurrentThread.ManagedThreadId).ToUTF8Bytes(),
        //                                                   Server          = "Hermod",
        //                                                   Connection      = "close"
        //                                               };

        //                                           });

        //            HTTPServer01.AddMethodCallback(HTTPMethod.GET,
        //                                           "localhost:2000",    // Check rfc!
        //                                           "/tests/{test}/ids/{id}",
        //                                           HTTPDelegate: Request =>
        //                                           {

        //                                               // Cache-control:max-age=300
        //                                               // Content-Encoding:gzip
        //                                               // Expires:Fri, 04 Jul 2014 23:05:04 GMT
        //                                               // Vary:User-Agent,Accept-Encoding

        //                                               return new HTTPResponseBuilder()
        //                                               {
        //                                                   HTTPStatusCode  = HTTPStatusCode.OK,
        //                                                   ContentType     = HTTPContentType.Text.TEXT_UTF8,
        //                                                   Content = String.Concat("Hello world on port 2000 /tests/{test}/ids/{id} with id == " + Request.ParsedQueryParameters.FirstOrDefault() + "!", Environment.NewLine, Thread.CurrentThread.ManagedThreadId).ToUTF8Bytes(),
        //                                                   Server          = "Hermod",
        //                                                   Connection      = "close"
        //                                               };

        //                                           });

        //            HTTPServer01.AddMethodCallback(HTTPMethod.GET,
        //                                           "localhost:2002",    // Check rfc!
        //                                           "/test2002",
        //                                           HTTPDelegate: Request =>
        //                                           {

        //                                               // Cache-control:max-age=300
        //                                               // Content-Encoding:gzip
        //                                               // Expires:Fri, 04 Jul 2014 23:05:04 GMT
        //                                               // Vary:User-Agent,Accept-Encoding

        //                                               return new HTTPResponseBuilder()
        //                                               {
        //                                                   HTTPStatusCode  = HTTPStatusCode.OK,
        //                                                   ContentType     = HTTPContentType.Text.TEXT_UTF8,
        //                                                   Content         = String.Concat("Hello world on port 2002 /test2!", Environment.NewLine, Thread.CurrentThread.ManagedThreadId).ToUTF8Bytes(),
        //                                                   Server          = "Hermod",
        //                                                   Connection      = "close"
        //                                               };

        //                                           });

        //_HTTPServer.OnNotification      += (ConnectionId, ServerTimestamp, Request) => {

        //    // Cache-control:max-age=300
        //    // Content-Encoding:gzip
        //    // Expires:Fri, 04 Jul 2014 23:05:04 GMT
        //    // Vary:User-Agent,Accept-Encoding

        //    return new HTTPResponseBuilder() {
        //        HTTPStatusCode  = HTTPStatusCode.OK,
        //        ContentType     = HTTPContentType.Text.TEXT_UTF8,
        //        Content         = String.Concat(Request.Host, Environment.NewLine, "Hello world any port!", Environment.NewLine, Thread.CurrentThread.ManagedThreadId).ToUTF8Bytes(),
        //        Server          = _HTTPServer.DefaultServerName,
        //        Connection      = "close"
        //    };

        //};

    }
}
