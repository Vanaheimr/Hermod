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

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// The examples of RFC 9421, as the RFC gives them, with the line wrapping
    /// of RFC 8792 undone. Generated from the RFC's text rather than typed, so
    /// that a test failing on one of them is about the code, not about a copy.
    /// </summary>
    /// <remarks>
    /// Raw string literals keep the line endings of this file; the tests
    /// compare them by ReplaceLineEndings. The keys are the RFC's test keys,
    /// which Appendix B.1 says MUST NOT be used for anything but testing.
    /// </remarks>
    internal static class RFC9421Vectors
    {

        /// <summary>Appendix B.2, test-request: the header section.</summary>
        public const String TestRequest = """
            POST /foo?param=Value&Pet=dog HTTP/1.1
            Host: example.com
            Date: Tue, 20 Apr 2021 02:07:55 GMT
            Content-Type: application/json
            Content-Digest: sha-512=:WZDPaVn/7XgHaAy8pmojAkGWoRx2UFChF41A2svX+TaPm+AbwAgBWnrIiYllu7BNNyealdVLvRwEmTHWXvJwew==:
            Content-Length: 18
            """;

        /// <summary>Appendix B.2, test-response: the header section.</summary>
        public const String TestResponse = """
            HTTP/1.1 200 OK
            Date: Tue, 20 Apr 2021 02:07:56 GMT
            Content-Type: application/json
            Content-Digest: sha-512=:mEWXIS7MaLRuGgxOBdODa3xqM1XdEvxoYhvlCFJ41QJgJc4GTsPp29l5oGX69wWdXymyU0rjJuahq4l5aGgfLQ==:
            Content-Length: 23
            """;

        /// <summary>Section 2.5, Figure 1: the signature base.</summary>
        public const String Figure1Base = """
            "@method": POST
            "@authority": example.com
            "@path": /foo
            "content-digest": sha-512=:WZDPaVn/7XgHaAy8pmojAkGWoRx2UFChF41A2svX+TaPm+AbwAgBWnrIiYllu7BNNyealdVLvRwEmTHWXvJwew==:
            "content-length": 18
            "content-type": application/json
            "@signature-params": ("@method" "@authority" "@path" "content-digest" "content-length" "content-type");created=1618884473;keyid="test-key-rsa-pss"
            """;

        /// <summary>Section 3.2: test-request signed with test-key-rsa-pss, labeled sig1.</summary>
        public const String Section32Request = """
            POST /foo?param=Value&Pet=dog HTTP/1.1
            Host: example.com
            Date: Tue, 20 Apr 2021 02:07:55 GMT
            Content-Type: application/json
            Content-Digest: sha-512=:WZDPaVn/7XgHaAy8pmojAkGWoRx2UFChF41A2svX+TaPm+AbwAgBWnrIiYllu7BNNyealdVLvRwEmTHWXvJwew==:
            Content-Length: 18
            Signature-Input: sig1=("@method" "@authority" "@path" "content-digest" "content-length" "content-type");created=1618884473;keyid="test-key-rsa-pss"
            Signature: sig1=:HIbjHC5rS0BYaa9v4QfD4193TORw7u9edguPh0AW3dMq9WImrlFrCGUDih47vAxi4L2YRZ3XMJc1uOKk/J0ZmZ+wcta4nKIgBkKq0rM9hs3CQyxXGxHLMCy8uqK488o+9jrptQ+xFPHK7a9sRL1IXNaagCNN3ZxJsYapFj+JXbmaI5rtAdSfSvzPuBCh+ARHBmWuNo1UzVVdHXrl8ePL4cccqlazIJdC4QEjrF+Sn4IxBQzTZsL9y9TP5FsZYzHvDqbInkTNigBcE9cKOYNFCn4D/WM7F6TNuZO9EgtzepLWcjTymlHzK7aXq6Am6sfOrpIC49yXjj3ae6HRalVc/g==:
            """;

        /// <summary>Section 2.4: the request the first signed response answers.</summary>
        public const String Section24Request = """
            POST /foo?param=Value&Pet=dog HTTP/1.1
            Host: example.com
            Date: Tue, 20 Apr 2021 02:07:55 GMT
            Content-Digest: sha-512=:WZDPaVn/7XgHaAy8pmojAkGWoRx2UFChF41A2svX+TaPm+AbwAgBWnrIiYllu7BNNyealdVLvRwEmTHWXvJwew==:
            Content-Type: application/json
            Content-Length: 18
            """;

        /// <summary>Section 2.4: the first signed response, labeled reqres.</summary>
        public const String Section24ResponseA = """
            HTTP/1.1 503 Service Unavailable
            Date: Tue, 20 Apr 2021 02:07:56 GMT
            Content-Type: application/json
            Content-Length: 62
            Content-Digest: sha-512=:0Y6iCBzGg5rZtoXS95Ijz03mslf6KAMCloESHObfwnHJDbkkWWQz6PhhU9kxsTbARtY2PTBOzq24uJFpHsMuAg==:
            Signature-Input: reqres=("@status" "content-digest" "content-type" "@authority";req "@method";req "@path";req "content-digest";req);created=1618884479;keyid="test-key-ecc-p256"
            Signature: reqres=:dMT/A/76ehrdBTD/2Xx8QuKV6FoyzEP/I9hdzKN8LQJLNgzU4W767HK05rx1i8meNQQgQPgQp8wq2ive3tV5Ag==:
            """;

        /// <summary>Section 2.4: the first response signature base.</summary>
        public const String Section24BaseA = """
            "@status": 503
            "content-digest": sha-512=:0Y6iCBzGg5rZtoXS95Ijz03mslf6KAMCloESHObfwnHJDbkkWWQz6PhhU9kxsTbARtY2PTBOzq24uJFpHsMuAg==:
            "content-type": application/json
            "@authority";req: example.com
            "@method";req: POST
            "@path";req: /foo
            "content-digest";req: sha-512=:WZDPaVn/7XgHaAy8pmojAkGWoRx2UFChF41A2svX+TaPm+AbwAgBWnrIiYllu7BNNyealdVLvRwEmTHWXvJwew==:
            "@signature-params": ("@status" "content-digest" "content-type" "@authority";req "@method";req "@path";req "content-digest";req);created=1618884479;keyid="test-key-ecc-p256"
            """;

        /// <summary>Section 2.4: the signed request the second signed response answers.</summary>
        public const String Section24SignedRequest = """
            POST /foo?param=Value&Pet=dog HTTP/1.1
            Host: example.com
            Date: Tue, 20 Apr 2021 02:07:55 GMT
            Content-Digest: sha-512=:WZDPaVn/7XgHaAy8pmojAkGWoRx2UFChF41A2svX+TaPm+AbwAgBWnrIiYllu7BNNyealdVLvRwEmTHWXvJwew==:
            Content-Type: application/json
            Content-Length: 18
            Signature-Input: sig1=("@method" "@authority" "@path" "@query" "content-digest" "content-type" "content-length");created=1618884475;keyid="test-key-rsa-pss"
            Signature: sig1=:e8UJ5wMiRaonlth5ERtE8GIiEH7Akcr493nQ07VPNo6y3qvjdKt0fo8VHO8xXDjmtYoatGYBGJVlMfIp06eVMEyNW2I4vN7XDAz7m5v1108vGzaDljrd0H8+SJ28g7bzn6h2xeL/8q+qUwahWA/JmC8aOC9iVnwbOKCc0WSrLgWQwTY6VLp42Qt7jjhYT5W7/wCvfK9A1VmHH1lJXsV873Z6hpxesd50PSmO+xaNeYvDLvVdZlhtw5PCtUYzKjHqwmaQ6DEuM8udRjYsoNqp2xZKcuCO1nKc0V3RjpqMZLuuyVbHDAbCzr0pg2d2VM/OC33JAU7meEjjaNz+d7LWPg==:
            """;

        /// <summary>Section 2.4: the second signed response, labeled reqres.</summary>
        public const String Section24ResponseB = """
            HTTP/1.1 503 Service Unavailable
            Date: Tue, 20 Apr 2021 02:07:56 GMT
            Content-Type: application/json
            Content-Length: 62
            Content-Digest: sha-512=:0Y6iCBzGg5rZtoXS95Ijz03mslf6KAMCloESHObfwnHJDbkkWWQz6PhhU9kxsTbARtY2PTBOzq24uJFpHsMuAg==:
            Signature-Input: reqres=("@status" "content-digest" "content-type" "@authority";req "@method";req "@path";req "@query";req "content-digest";req "content-type";req "content-length";req);created=1618884479;keyid="test-key-ecc-p256"
            Signature: reqres=:C73J41GVKc+TYXbSobvZf0CmNcptRiWN+NY1Or0A36ISg6ymdRN6ZgR2QfrtopFNzqAyv+CeWrMsNbcV2Ojsgg==:
            """;

        /// <summary>Section 2.4: the second response signature base.</summary>
        public const String Section24BaseB = """
            "@status": 503
            "content-digest": sha-512=:0Y6iCBzGg5rZtoXS95Ijz03mslf6KAMCloESHObfwnHJDbkkWWQz6PhhU9kxsTbARtY2PTBOzq24uJFpHsMuAg==:
            "content-type": application/json
            "@authority";req: example.com
            "@method";req: POST
            "@path";req: /foo
            "@query";req: ?param=Value&Pet=dog
            "content-digest";req: sha-512=:WZDPaVn/7XgHaAy8pmojAkGWoRx2UFChF41A2svX+TaPm+AbwAgBWnrIiYllu7BNNyealdVLvRwEmTHWXvJwew==:
            "content-type";req: application/json
            "content-length";req: 18
            "@signature-params": ("@status" "content-digest" "content-type" "@authority";req "@method";req "@path";req "@query";req "content-digest";req "content-type";req "content-length";req);created=1618884479;keyid="test-key-ecc-p256"
            """;

        /// <summary>Section 4.3: the client request, signed as sig1 with test-key-ecc-p256.</summary>
        public const String Section43ClientRequest = """
            POST /foo?param=Value&Pet=dog HTTP/1.1
            Host: example.com
            Date: Tue, 20 Apr 2021 02:07:55 GMT
            Content-Type: application/json
            Content-Length: 18
            Content-Digest: sha-512=:WZDPaVn/7XgHaAy8pmojAkGWoRx2UFChF41A2svX+TaPm+AbwAgBWnrIiYllu7BNNyealdVLvRwEmTHWXvJwew==:
            Signature-Input: sig1=("@method" "@authority" "@path" "content-digest" "content-type" "content-length");created=1618884475;keyid="test-key-ecc-p256"
            Signature: sig1=:X5spyd6CFnAG5QnDyHfqoSNICd+BUP4LYMz2Q0JXlb//4Ijpzp+kve2w4NIyqeAuM7jTDX+sNalzA8ESSaHD3A==:
            """;

        /// <summary>Section 4.3: the request the proxy forwards, with sig1 and proxy_sig.</summary>
        public const String Section43ProxiedRequest = """
            POST /foo?param=Value&Pet=dog HTTP/1.1
            Host: origin.host.internal.example
            Date: Tue, 20 Apr 2021 02:07:56 GMT
            Content-Type: application/json
            Content-Length: 18
            Forwarded: for=192.0.2.123;host=example.com;proto=https
            Content-Digest: sha-512=:WZDPaVn/7XgHaAy8pmojAkGWoRx2UFChF41A2svX+TaPm+AbwAgBWnrIiYllu7BNNyealdVLvRwEmTHWXvJwew==:
            Signature-Input: sig1=("@method" "@authority" "@path" "content-digest" "content-type" "content-length");created=1618884475;keyid="test-key-ecc-p256", proxy_sig=("@method" "@authority" "@path" "content-digest" "content-type" "content-length" "forwarded");created=1618884480;keyid="test-key-rsa";alg="rsa-v1_5-sha256";expires=1618884540
            Signature: sig1=:X5spyd6CFnAG5QnDyHfqoSNICd+BUP4LYMz2Q0JXlb//4Ijpzp+kve2w4NIyqeAuM7jTDX+sNalzA8ESSaHD3A==:, proxy_sig=:S6ZzPXSdAMOPjN/6KXfXWNO/f7V6cHm7BXYUh3YD/fRad4BCaRZxP+JH+8XY1I6+8Cy+CM5g92iHgxtRPz+MjniOaYmdkDcnL9cCpXJleXsOckpURl49GwiyUpZ10KHgOEe11sx3G2gxI8S0jnxQB+Pu68U9vVcasqOWAEObtNKKZd8tSFu7LB5YAv0RAGhB8tmpv7sFnIm9y+7X5kXQfi8NMaZaA8i2ZHwpBdg7a6CMfwnnrtflzvZdXAsD3LH2TwevU+/PBPv0B6NMNk93wUs/vfJvye+YuI87HU38lZHowtznbLVdp770I6VHR6WfgS9ddzirrswsE1w5o0LV/g==:
            """;

        /// <summary>Section 4.3: the proxy signature base.</summary>
        public const String Section43ProxyBase = """
            "@method": POST
            "@authority": origin.host.internal.example
            "@path": /foo
            "content-digest": sha-512=:WZDPaVn/7XgHaAy8pmojAkGWoRx2UFChF41A2svX+TaPm+AbwAgBWnrIiYllu7BNNyealdVLvRwEmTHWXvJwew==:
            "content-type": application/json
            "content-length": 18
            "forwarded": for=192.0.2.123;host=example.com;proto=https
            "@signature-params": ("@method" "@authority" "@path" "content-digest" "content-type" "content-length" "forwarded");created=1618884480;keyid="test-key-rsa";alg="rsa-v1_5-sha256";expires=1618884540
            """;

        /// <summary>Appendix B.2.1: the signature base.</summary>
        public const String BaseB21 = """
            "@signature-params": ();created=1618884473;keyid="test-key-rsa-pss";nonce="b3k2pp5k7z-50gnwp.yemd"
            """;

        /// <summary>Appendix B.2.2: the signature base.</summary>
        public const String BaseB22 = """
            "@authority": example.com
            "content-digest": sha-512=:WZDPaVn/7XgHaAy8pmojAkGWoRx2UFChF41A2svX+TaPm+AbwAgBWnrIiYllu7BNNyealdVLvRwEmTHWXvJwew==:
            "@query-param";name="Pet": dog
            "@signature-params": ("@authority" "content-digest" "@query-param";name="Pet");created=1618884473;keyid="test-key-rsa-pss";tag="header-example"
            """;

        /// <summary>Appendix B.2.3: the signature base.</summary>
        public const String BaseB23 = """
            "date": Tue, 20 Apr 2021 02:07:55 GMT
            "@method": POST
            "@path": /foo
            "@query": ?param=Value&Pet=dog
            "@authority": example.com
            "content-type": application/json
            "content-digest": sha-512=:WZDPaVn/7XgHaAy8pmojAkGWoRx2UFChF41A2svX+TaPm+AbwAgBWnrIiYllu7BNNyealdVLvRwEmTHWXvJwew==:
            "content-length": 18
            "@signature-params": ("date" "@method" "@path" "@query" "@authority" "content-type" "content-digest" "content-length");created=1618884473;keyid="test-key-rsa-pss"
            """;

        /// <summary>Appendix B.2.4: the signature base.</summary>
        public const String BaseB24 = """
            "@status": 200
            "content-type": application/json
            "content-digest": sha-512=:mEWXIS7MaLRuGgxOBdODa3xqM1XdEvxoYhvlCFJ41QJgJc4GTsPp29l5oGX69wWdXymyU0rjJuahq4l5aGgfLQ==:
            "content-length": 23
            "@signature-params": ("@status" "content-type" "content-digest" "content-length");created=1618884473;keyid="test-key-ecc-p256"
            """;

        /// <summary>Appendix B.2.5: the signature base.</summary>
        public const String BaseB25 = """
            "date": Tue, 20 Apr 2021 02:07:55 GMT
            "@authority": example.com
            "content-type": application/json
            "@signature-params": ("date" "@authority" "content-type");created=1618884473;keyid="test-shared-secret"
            """;

        /// <summary>Appendix B.2.6: the signature base.</summary>
        public const String BaseB26 = """
            "date": Tue, 20 Apr 2021 02:07:55 GMT
            "@method": POST
            "@path": /foo
            "@authority": example.com
            "content-type": application/json
            "content-length": 18
            "@signature-params": ("date" "@method" "@path" "@authority" "content-type" "content-length");created=1618884473;keyid="test-key-ed25519"
            """;

        /// <summary>Appendix B.3: the signature base.</summary>
        public const String BaseB3 = """
            "@path": /foo
            "@query": ?param=Value&Pet=dog
            "@method": POST
            "@authority": service.internal.example
            "client-cert": :MIIBqDCCAU6gAwIBAgIBBzAKBggqhkjOPQQDAjA6MRswGQYDVQQKDBJMZXQncyBBdXRoZW50aWNhdGUxGzAZBgNVBAMMEkxBIEludGVybWVkaWF0ZSBDQTAeFw0yMDAxMTQyMjU1MzNaFw0yMTAxMjMyMjU1MzNaMA0xCzAJBgNVBAMMAkJDMFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE8YnXXfaUgmnMtOXU/IncWalRhebrXmckC8vdgJ1p5Be5F/3YC8OthxM4+k1M6aEAEFcGzkJiNy6J84y7uzo9M6NyMHAwCQYDVR0TBAIwADAfBgNVHSMEGDAWgBRm3WjLa38lbEYCuiCPct0ZaSED2DAOBgNVHQ8BAf8EBAMCBsAwEwYDVR0lBAwwCgYIKwYBBQUHAwIwHQYDVR0RAQH/BBMwEYEPYmRjQGV4YW1wbGUuY29tMAoGCCqGSM49BAMCA0gAMEUCIBHda/r1vaL6G3VliL4/Di6YK0Q6bMjeSkC3dFCOOB8TAiEAx/kHSB4urmiZ0NX5r5XarmPk0wmuydBVoU4hBVZ1yhk=:
            "@signature-params": ("@path" "@query" "@method" "@authority" "client-cert");created=1618884473;keyid="test-key-ecc-p256"
            """;

        /// <summary>Appendix B.4: the signature base.</summary>
        public const String BaseB4 = """
            "@method": GET
            "@path": /demo
            "@authority": example.org
            "accept": application/json, */*
            "@signature-params": ("@method" "@path" "@authority" "accept");created=1618884473;keyid="test-key-ed25519"
            """;

        /// <summary>Appendix B.2.1: the Signature-Input and Signature lines, labeled sig-b21.</summary>
        public const String FieldsB21 = """
            Signature-Input: sig-b21=();created=1618884473;keyid="test-key-rsa-pss";nonce="b3k2pp5k7z-50gnwp.yemd"
            Signature: sig-b21=:d2pmTvmbncD3xQm8E9ZV2828BjQWGgiwAaw5bAkgibUopemLJcWDy/lkbbHAve4cRAtx31Iq786U7it++wgGxbtRxf8Udx7zFZsckzXaJMkA7ChG52eSkFxykJeNqsrWH5S+oxNFlD4dzVuwe8DhTSja8xxbR/Z2cOGdCbzR72rgFWhzx2VjBqJzsPLMIQKhO4DGezXehhWwE56YCE+O6c0mKZsfxVrogUvA4HELjVKWmAvtl6UnCh8jYzuVG5WSb/QEVPnP5TmcAnLH1g+s++v6d4s8m0gCw1fV5/SITLq9mhho8K3+7EPYTU8IU1bLhdxO5Nyt8C8ssinQ98Xw9Q==:
            """;

        /// <summary>Appendix B.2.2: the Signature-Input and Signature lines, labeled sig-b22.</summary>
        public const String FieldsB22 = """
            Signature-Input: sig-b22=("@authority" "content-digest" "@query-param";name="Pet");created=1618884473;keyid="test-key-rsa-pss";tag="header-example"
            Signature: sig-b22=:LjbtqUbfmvjj5C5kr1Ugj4PmLYvx9wVjZvD9GsTT4F7GrcQEdJzgI9qHxICagShLRiLMlAJjtq6N4CDfKtjvuJyE5qH7KT8UCMkSowOB4+ECxCmT8rtAmj/0PIXxi0A0nxKyB09RNrCQibbUjsLS/2YyFYXEu4TRJQzRw1rLEuEfY17SARYhpTlaqwZVtR8NV7+4UKkjqpcAoFqWFQh62s7Cl+H2fjBSpqfZUJcsIk4N6wiKYd4je2U/lankenQ99PZfB4jY3I5rSV2DSBVkSFsURIjYErOs0tFTQosMTAoxk//0RoKUqiYY8Bh0aaUEb0rQl3/XaVe4bXTugEjHSw==:
            """;

        /// <summary>Appendix B.2.3: the Signature-Input and Signature lines, labeled sig-b23.</summary>
        public const String FieldsB23 = """
            Signature-Input: sig-b23=("date" "@method" "@path" "@query" "@authority" "content-type" "content-digest" "content-length");created=1618884473;keyid="test-key-rsa-pss"
            Signature: sig-b23=:bbN8oArOxYoyylQQUU6QYwrTuaxLwjAC9fbY2F6SVWvh0yBiMIRGOnMYwZ/5MR6fb0Kh1rIRASVxFkeGt683+qRpRRU5p2voTp768ZrCUb38K0fUxN0O0iC59DzYx8DFll5GmydPxSmme9v6ULbMFkl+V5B1TP/yPViV7KsLNmvKiLJH1pFkh/aYA2HXXZzNBXmIkoQoLd7YfW91kE9o/CCoC1xMy7JA1ipwvKvfrs65ldmlu9bpG6A9BmzhuzF8Eim5f8ui9eH8LZH896+QIF61ka39VBrohr9iyMUJpvRX2Zbhl5ZJzSRxpJyoEZAFL2FUo5fTIztsDZKEgM4cUA==:
            """;

        /// <summary>Appendix B.2.4: the Signature-Input and Signature lines, labeled sig-b24.</summary>
        public const String FieldsB24 = """
            Signature-Input: sig-b24=("@status" "content-type" "content-digest" "content-length");created=1618884473;keyid="test-key-ecc-p256"
            Signature: sig-b24=:wNmSUAhwb5LxtOtOpNa6W5xj067m5hFrj0XQ4fvpaCLx0NKocgPquLgyahnzDnDAUy5eCdlYUEkLIj+32oiasw==:
            """;

        /// <summary>Appendix B.2.5: the Signature-Input and Signature lines, labeled sig-b25.</summary>
        public const String FieldsB25 = """
            Signature-Input: sig-b25=("date" "@authority" "content-type");created=1618884473;keyid="test-shared-secret"
            Signature: sig-b25=:pxcQw6G3AjtMBQjwo8XzkZf/bws5LelbaMk5rGIGtE8=:
            """;

        /// <summary>Appendix B.2.6: the Signature-Input and Signature lines, labeled sig-b26.</summary>
        public const String FieldsB26 = """
            Signature-Input: sig-b26=("date" "@method" "@path" "@authority" "content-type" "content-length");created=1618884473;keyid="test-key-ed25519"
            Signature: sig-b26=:wqcAqbmYJ2ji2glfAMaRy4gruYYnx2nEFN2HN6jrnDnQCK1u02Gb04v9EDgwUPiu4A0w6vuQv5lIp5WPpBKRCw==:
            """;

        /// <summary>Appendix B.3: the request the TLS-terminating proxy signs, labeled ttrp.</summary>
        public const String ProxiedTLSRequest = """
            POST /foo?param=Value&Pet=dog HTTP/1.1
            Host: service.internal.example
            Date: Tue, 20 Apr 2021 02:07:55 GMT
            Content-Type: application/json
            Content-Length: 18
            Client-Cert: :MIIBqDCCAU6gAwIBAgIBBzAKBggqhkjOPQQDAjA6MRswGQYDVQQKDBJMZXQncyBBdXRoZW50aWNhdGUxGzAZBgNVBAMMEkxBIEludGVybWVkaWF0ZSBDQTAeFw0yMDAxMTQyMjU1MzNaFw0yMTAxMjMyMjU1MzNaMA0xCzAJBgNVBAMMAkJDMFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE8YnXXfaUgmnMtOXU/IncWalRhebrXmckC8vdgJ1p5Be5F/3YC8OthxM4+k1M6aEAEFcGzkJiNy6J84y7uzo9M6NyMHAwCQYDVR0TBAIwADAfBgNVHSMEGDAWgBRm3WjLa38lbEYCuiCPct0ZaSED2DAOBgNVHQ8BAf8EBAMCBsAwEwYDVR0lBAwwCgYIKwYBBQUHAwIwHQYDVR0RAQH/BBMwEYEPYmRjQGV4YW1wbGUuY29tMAoGCCqGSM49BAMCA0gAMEUCIBHda/r1vaL6G3VliL4/Di6YK0Q6bMjeSkC3dFCOOB8TAiEAx/kHSB4urmiZ0NX5r5XarmPk0wmuydBVoU4hBVZ1yhk=:
            Signature-Input: ttrp=("@path" "@query" "@method" "@authority" "client-cert");created=1618884473;keyid="test-key-ecc-p256"
            Signature: ttrp=:xVMHVpawaAC/0SbHrKRs9i8I3eOs5RtTMGCWXm/9nvZzoHsIg6Mce9315T6xoklyy0yzhD9ah4JHRwMLOgmizw==:
            """;

        /// <summary>Appendix B.4, the signed request.</summary>
        public const String B4Original = """
            GET /demo?name1=Value1&Name2=value2 HTTP/1.1
            Host: example.org
            Date: Fri, 15 Jul 2022 14:24:55 GMT
            Accept: application/json
            Accept: */*
            Signature-Input: transform=("@method" "@path" "@authority" "accept");created=1618884473;keyid="test-key-ed25519"
            Signature: transform=:ZT1kooQsEHpZ0I1IjCqtQppOmIqlJPeo7DHR3SoMn0s5JZ1eRGS0A+vyYP9t/LXlh5QMFFQ6cpLt2m0pmj3NDA==:
            """;

        /// <summary>Appendix B.4, a field and a query parameter added: still valid.</summary>
        public const String B4AddedQueryAndField = """
            GET /demo?name1=Value1&Name2=value2&param=added HTTP/1.1
            Host: example.org
            Date: Fri, 15 Jul 2022 14:24:55 GMT
            Accept: application/json
            Accept: */*
            Accept-Language: en-US,en;q=0.5
            Signature-Input: transform=("@method" "@path" "@authority" "accept");created=1618884473;keyid="test-key-ed25519"
            Signature: transform=:ZT1kooQsEHpZ0I1IjCqtQppOmIqlJPeo7DHR3SoMn0s5JZ1eRGS0A+vyYP9t/LXlh5QMFFQ6cpLt2m0pmj3NDA==:
            """;

        /// <summary>Appendix B.4, Date removed, Referer added, Accept on one line: still valid.</summary>
        public const String B4FieldsChanged = """
            GET /demo?name1=Value1&Name2=value2 HTTP/1.1
            Host: example.org
            Referer: https://developer.example.org/demo
            Accept: application/json, */*
            Signature-Input: transform=("@method" "@path" "@authority" "accept");created=1618884473;keyid="test-key-ed25519"
            Signature: transform=:ZT1kooQsEHpZ0I1IjCqtQppOmIqlJPeo7DHR3SoMn0s5JZ1eRGS0A+vyYP9t/LXlh5QMFFQ6cpLt2m0pmj3NDA==:
            """;

        /// <summary>Appendix B.4, the fields reordered, the Accept lines not: still valid.</summary>
        public const String B4Reordered = """
            GET /demo?name1=Value1&Name2=value2 HTTP/1.1
            Accept: application/json
            Accept: */*
            Date: Fri, 15 Jul 2022 14:24:55 GMT
            Host: example.org
            Signature-Input: transform=("@method" "@path" "@authority" "accept");created=1618884473;keyid="test-key-ed25519"
            Signature: transform=:ZT1kooQsEHpZ0I1IjCqtQppOmIqlJPeo7DHR3SoMn0s5JZ1eRGS0A+vyYP9t/LXlh5QMFFQ6cpLt2m0pmj3NDA==:
            """;

        /// <summary>Appendix B.4, method and authority changed: NOT valid.</summary>
        public const String B4MethodAndAuthority = """
            POST /demo?name1=Value1&Name2=value2 HTTP/1.1
            Host: example.com
            Date: Fri, 15 Jul 2022 14:24:55 GMT
            Accept: application/json
            Accept: */*
            Signature-Input: transform=("@method" "@path" "@authority" "accept");created=1618884473;keyid="test-key-ed25519"
            Signature: transform=:ZT1kooQsEHpZ0I1IjCqtQppOmIqlJPeo7DHR3SoMn0s5JZ1eRGS0A+vyYP9t/LXlh5QMFFQ6cpLt2m0pmj3NDA==:
            """;

        /// <summary>Appendix B.4, the two Accept lines swapped: NOT valid.</summary>
        public const String B4AcceptLinesSwapped = """
            GET /demo?name1=Value1&Name2=value2 HTTP/1.1
            Host: example.org
            Date: Fri, 15 Jul 2022 14:24:55 GMT
            Accept: */*
            Accept: application/json
            Signature-Input: transform=("@method" "@path" "@authority" "accept");created=1618884473;keyid="test-key-ed25519"
            Signature: transform=:ZT1kooQsEHpZ0I1IjCqtQppOmIqlJPeo7DHR3SoMn0s5JZ1eRGS0A+vyYP9t/LXlh5QMFFQ6cpLt2m0pmj3NDA==:
            """;

        /// <summary>Appendix B.1.1, test-key-rsa, the public key, PKCS #1. Its private key has primes of 1088 and 960 bits, which the BCL cannot import.</summary>
        public const String RSAPublicKeyPEM = """
            -----BEGIN RSA PUBLIC KEY-----
            MIIBCgKCAQEAhAKYdtoeoy8zcAcR874L8cnZxKzAGwd7v36APp7Pv6Q2jdsPBRrw
            WEBnez6d0UDKDwGbc6nxfEXAy5mbhgajzrw3MOEt8uA5txSKobBpKDeBLOsdJKFq
            MGmXCQvEG7YemcxDTRPxAleIAgYYRjTSd/QBwVW9OwNFhekro3RtlinV0a75jfZg
            kne/YiktSvLG34lw2zqXBDTC5NHROUqGTlML4PlNZS5Ri2U4aCNx2rUPRcKIlE0P
            uKxI4T+HIaFpv8+rdV6eUgOrB2xeI1dSFFn/nnv5OoZJEIB+VmuKn3DCUcCZSFlQ
            PSXSfBDiUGhwOw76WuSSsf1D4b/vLoJ10wIDAQAB
            -----END RSA PUBLIC KEY-----
            """;

        /// <summary>Appendix B.1.2, test-key-rsa-pss, the public key.</summary>
        public const String RSAPSSPublicKeyPEM = """
            -----BEGIN PUBLIC KEY-----
            MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAr4tmm3r20Wd/PbqvP1s2
            +QEtvpuRaV8Yq40gjUR8y2Rjxa6dpG2GXHbPfvMs8ct+Lh1GH45x28Rw3Ry53mm+
            oAXjyQ86OnDkZ5N8lYbggD4O3w6M6pAvLkhk95AndTrifbIFPNU8PPMO7OyrFAHq
            gDsznjPFmTOtCEcN2Z1FpWgchwuYLPL+Wokqltd11nqqzi+bJ9cvSKADYdUAAN5W
            Utzdpiy6LbTgSxP7ociU4Tn0g5I6aDZJ7A8Lzo0KSyZYoA485mqcO0GVAdVw9lq4
            aOT9v6d+nb4bnNkQVklLQ3fVAvJm+xdDOp9LCNCN48V2pnDOkFV6+U9nV5oyc6XI
            2wIDAQAB
            -----END PUBLIC KEY-----
            """;

        /// <summary>Appendix B.1.3, test-key-ecc-p256.</summary>
        public const String ECCP256PrivateKeyPEM = """
            -----BEGIN EC PRIVATE KEY-----
            MHcCAQEEIFKbhfNZfpDsW43+0+JjUr9K+bTeuxopu653+hBaXGA7oAoGCCqGSM49
            AwEHoUQDQgAEqIVYZVLCrPZHGHjP17CTW0/+D9Lfw0EkjqF7xB4FivAxzic30tMM
            4GF+hR6Dxh71Z50VGGdldkkDXZCnTNnoXQ==
            -----END EC PRIVATE KEY-----
            """;

        /// <summary>Appendix B.1.3, test-key-ecc-p256, the public key.</summary>
        public const String ECCP256PublicKeyPEM = """
            -----BEGIN PUBLIC KEY-----
            MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEqIVYZVLCrPZHGHjP17CTW0/+D9Lf
            w0EkjqF7xB4FivAxzic30tMM4GF+hR6Dxh71Z50VGGdldkkDXZCnTNnoXQ==
            -----END PUBLIC KEY-----
            """;

        /// <summary>Appendix B.1.2, test-key-rsa-pss, as a JWK.</summary>
        public const String RSAPSSJWK = """
            {
              "kty": "RSA",
              "kid": "test-key-rsa-pss",
              "p": "5V-6ISI5yEaCFXm-fk1EM2xwAWekePVCAyvr9QbTlFOCZwt9WwjUjhtKRusi5Uq-IYZ_tq2WRE4As4b_FHEMtp2AER43IcvmXPqKFBoUktVDS7dThIHrsnRi1U7dHqVdwiMEMe5jxKNgnsKLpnq-4NyhoS6OeWu1SFozG9J9xQk",
              "q": "w-wIde17W5Y0Cphp3ZZ0uM8OUq1AkrV2IKauqYHaDxAT32EM4ci2MMER2nIUEo4g_42lW0zYouFFqONwv0-HyOsgPpdSqKRC5WLgn0VXabjaNcy6KhNPXeJ0AgtqdiDwPeJ2_L_eKwNWQ43RfdQBUquAwSd7SEmmQ8sViqB628M",
              "d": "lAfIqfpCYomVShfAKnwf2lD9I0wKjkHsCtZCif4kAlwQqqW6N-tIL3bdOR-VWf0Q1ZBIDtpO91UrG7pansyrPERbNrRJlPiYEyPTHkCT1nD-l2isuiyGLNBNnFoKfBgA4KAbPJZQatFIV9Cn34JSHnpN5-2ehreGBYHtkwHFtlmzeF3yu5bqRcqOhx8lkYmBzDAEUFyyXjknU5-WjAT9DzuG0MpOTkcU1EnjnIjyVBZLUB5Lxm8puyq8hH8B_E5LNC-1oc8j-tDy98UvRTTiYvZvs87cGCFxg0LijNhg7CE3g9piNqB6DzMgA9MHSOwcElVtfKdYfo4H3OHZXsSmEQ",
              "e": "AQAB",
              "qi": "jRAqfYi_tKCjhP9eM0N2XaRlNeoYCTx06GlSLD8d0zc4ZZuEePY10LMGWI6Y_JC0CvvvQYhNa9sAj4hFjIVLsWeTplVVUezGO1ofLW4kYWVpnMpHgAY1pRM4kyzo1p3MKYY8DE1BA4KqhSOfhdGs6Ov3Dfj0migZeE7Fu7yc7Fc",
              "dp": "otDolkxtJ7Sk8gmRJqZCGx6GAvlGznWJfibXPv6xgUAl-G83dD84YgcNGnoeMxRzEekfDtT5LVMRPF4_AoucsqPqHDyOdfb-dlGBYfOBVxj6w-xF5HE0lV_4J-HrI63Od9fTSn4lY5d1JjyCVJIcnBEAyiD6EUZbUBh23vDzRcE",
              "dq": "iZE1S6CpqmBoQDxOsXGQmaeBdhoCqkDSJhEDuS_dLhBq88FQa0UkcE1QvOK3J2Q21VnfDqGBx7SH1hOFOj-cpz45kNluB832ztxDvnHQ9AIA7h_HY_3VD6YPMNRVN4bfSYS3abdLR0Z7jsmInGJ9X0_fA0E2tkZIgXeas5EFU0M",
              "n": "r4tmm3r20Wd_PbqvP1s2-QEtvpuRaV8Yq40gjUR8y2Rjxa6dpG2GXHbPfvMs8ct-Lh1GH45x28Rw3Ry53mm-oAXjyQ86OnDkZ5N8lYbggD4O3w6M6pAvLkhk95AndTrifbIFPNU8PPMO7OyrFAHqgDsznjPFmTOtCEcN2Z1FpWgchwuYLPL-Wokqltd11nqqzi-bJ9cvSKADYdUAAN5WUtzdpiy6LbTgSxP7ociU4Tn0g5I6aDZJ7A8Lzo0KSyZYoA485mqcO0GVAdVw9lq4aOT9v6d-nb4bnNkQVklLQ3fVAvJm-xdDOp9LCNCN48V2pnDOkFV6-U9nV5oyc6XI2w"
            }
            """;

        /// <summary>Appendix B.1.4, test-key-ed25519, as a JWK.</summary>
        public const String Ed25519JWK = """
            {
              "kty": "OKP",
              "crv": "Ed25519",
              "kid": "test-key-ed25519",
              "d": "n4Ni-HpISpVObnQMW0wOhCKROaIKqKtW_2ZYb2p9KcU",
              "x": "JrQLj5P_89iXES9-vFgrIy29clF9CC_oPPsw3c5D0bs"
            }
            """;

        /// <summary>Appendix B.1.5, test-shared-secret.</summary>
        public const String SharedSecretBase64 = """
            uzvJfB4u3N0Jy4T7NZ75MDVcr8zSTInedJtkgcu46YW4XByzNJjxBdtjUkdJPBtbmHhIDi6pcl8jsasjlTMtDQ==
            """;

    }

}
