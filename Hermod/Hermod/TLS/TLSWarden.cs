/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    public enum CertTypes
    {
        Root,
        Intermediate,
        Leaf
    }

    public class CertificateInfo(X509Certificate2  Certificate,
                                 CertTypes         CertType,
                                 String            DisplayName,
                                 DateTimeOffset    ExpiryDate,
                                 UInt32            DaysUntilExpiry,
                                 Boolean           IsExpired)
    {

        public X509Certificate2             Certificate        { get; } = Certificate;
        public CertTypes                    CertType           { get; } = CertType;
        public String                       DisplayName        { get; } = DisplayName;
        public DateTimeOffset               ExpiryDate         { get; } = ExpiryDate;
        public UInt32                       DaysUntilExpiry    { get; } = DaysUntilExpiry;
        public Boolean                      IsExpired          { get; } = IsExpired;

    }


    public class Check(String  URL,
                       Byte    ExpireOk = 14)
    {

        public String                        URL                 { get; } = URL;
        public Byte                          ExpireOk            { get; } = ExpireOk;

    }


    public class CheckResult(String  URL,
                             Byte    ExpireOk = 14)

        : Check(URL,
                ExpireOk)

    {

        public X509Certificate2?             Certificate         { get; set; }
        public X509Chain?                    Chain               { get; set; }

        public Boolean?                      IsChainValid        { get; set; }

        public IEnumerable<X509ChainStatus>  ChainStatus         { get; set; } = [];

        public List<CertificateInfo>         CertificateInfos    { get; set; } = [];

        public List<String>                  Messages            { get; set; } = [];
        public List<String>                  Warnings            { get; set; } = [];
        public List<String>                  ErrorMessages       { get; set; } = [];


    }


    public static class TLS_Warden
    {

        #region CreateRemoteCertificateValidationFunc(Check)

        public static Func<HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors, Boolean>? CreateRemoteCertificateValidationFunc(CheckResult Check)

            => (message, cert, chain, errors) => {

                var check = Check;
                check.Certificate = cert;
                check.Chain = chain;

                if (chain is not null)
                {

                    var chainValidator = new X509Chain();
                    chainValidator.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                    check.IsChainValid = chainValidator.Build(chain.ChainElements[0].Certificate);
                    check.ChainStatus = chainValidator.ChainStatus;

                    if (!check.IsChainValid.HasValue || check.IsChainValid.Value == false)
                    {

                        Check.ErrorMessages.Add($"The certificate chain for {Check} is invalid!");

                        foreach (var status in chainValidator.ChainStatus)
                            Check.ErrorMessages.Add($" - {status.Status}: {status.StatusInformation}");

                    }

                    for (var i = 0; i < chain.ChainElements.Count; i++)
                    {

                        var daysUntilExpiry = (UInt32)Math.Floor((chain.ChainElements[i].Certificate.NotAfter - DateTime.Now).TotalDays);

                        var certificateInfo = new CertificateInfo(
                                                   Certificate: chain.ChainElements[i].Certificate,
                                                   CertType: i == 0 ? CertTypes.Leaf : i == chain.ChainElements.Count - 1 ? CertTypes.Root : CertTypes.Intermediate,
                                                   DisplayName: GetCertificateDisplayName(chain.ChainElements[i].Certificate, i == 0),
                                                   ExpiryDate: chain.ChainElements[i].Certificate.NotAfter,
                                                   DaysUntilExpiry: daysUntilExpiry,
                                                   IsExpired: daysUntilExpiry <= Check.ExpireOk
                                               );

                        Check.CertificateInfos.Add(certificateInfo);


                        if (daysUntilExpiry <= 14)
                            Check.Warnings.Add($"{certificateInfo.CertType} certificate '{certificateInfo.DisplayName}' expires in {daysUntilExpiry} days on: {certificateInfo.ExpiryDate})");
                        else
                            Check.Messages.Add($"{certificateInfo.CertType} certificate '{certificateInfo.DisplayName}' valid until: {certificateInfo.ExpiryDate}");

                    }

                }

                return true;

            };


        public static HttpClientHandler CreateHTTPClientHandler(CheckResult Check)

            => new() {
                   ServerCertificateCustomValidationCallback = CreateRemoteCertificateValidationFunc(Check)
               };


        public static RemoteCertificateValidationCallback CreateRemoteCertificateValidationCallback(CheckResult Check)
            => new ((sender, certificate, chain, sslPolicyErrors) => {
                   var func = CreateRemoteCertificateValidationFunc(Check);
                   return func?.Invoke(null, certificate as X509Certificate2, chain, sslPolicyErrors) ?? false;
               });

        #endregion


        #region StartCheckAll (Checks, DelayBetweenIterations, HandleCheckResult, ConcurrencyLimit = 10, ...)

        public static void StartCheckAll(IEnumerable<Check>   Checks,
                                         TimeSpan             DelayBetweenIterations,
                                         Action<CheckResult>  HandleCheckResult,
                                         UInt16               ConcurrencyLimit    = 10,
                                         CancellationToken    CancellationToken   = default)
        {

            _ = Task.Run(async () => {

                try
                {
                    await foreach (var checkResult in CheckAllAsync(
                                                          Checks,
                                                          DelayBetweenIterations,
                                                          ConcurrencyLimit,
                                                          CancellationToken
                                                      ))
                    {

                        if (checkResult is not null)
                            HandleCheckResult(checkResult);

                        // else logger?.LogWarning("Null check result for task.");

                    }
                }
                catch (Exception e)
                {
                    // logger?.LogError(ex, "Error in RepeatCheckAllAsync background task.");
                }

            }, CancellationToken);

        }

        #endregion

        #region CheckAllAsync (Checks, DelayBetweenIterations, ConcurrencyLimit = 10, ...)

        public static async IAsyncEnumerable<CheckResult> CheckAllAsync(IEnumerable<Check>                          Checks,
                                                                        TimeSpan                                    DelayBetweenIterations,
                                                                        UInt16                                      ConcurrencyLimit    = 10,
                                                                        [EnumeratorCancellation] CancellationToken  CancellationToken   = default)
        {
            while (!CancellationToken.IsCancellationRequested)
            {

                await foreach (var checkResult in CheckAllAsync(
                                                      Checks,
                                                      ConcurrencyLimit,
                                                      CancellationToken
                                                  ))
                {
                    yield return checkResult;
                }

                if (DelayBetweenIterations > TimeSpan.Zero &&
                   !CancellationToken.IsCancellationRequested)
                {

                    await Task.Delay(
                              DelayBetweenIterations,
                              CancellationToken
                          );

                }

            }
        }

        #endregion

        #region CheckAllAsync (Checks, ConcurrencyLimit = 10, ...)

        public static async IAsyncEnumerable<CheckResult> CheckAllAsync(IEnumerable<Check>                          Checks,
                                                                        UInt16                                      ConcurrencyLimit    = 10,
                                                                        [EnumeratorCancellation] CancellationToken  CancellationToken   = default)
        {

            using var semaphore = new SemaphoreSlim(ConcurrencyLimit);

            await foreach (var task in Task.WhenEach(
                                           Checks.Select(async check => {
                                               await semaphore.WaitAsync(CancellationToken);
                                               try
                                               {
                                                   return await Check(check, CancellationToken);
                                               }
                                               catch (Exception ex)
                                               {
                                                   return new CheckResult(check.URL) {
                                                              ErrorMessages = [ $"Error processing check for {check.URL}: {ex.Message}" ]
                                                          };
                                               }
                                               finally
                                               {
                                                   semaphore.Release();
                                               }
                                           })
                                       ).
                                       WithCancellation(CancellationToken))
            {

                var checkResult = await task;

                if (checkResult is not null)
                    yield return checkResult;

            }

        }

        #endregion

        #region CheckAll      (Checks, ConcurrencyLimit = 10, ...)

        public static async Task<IEnumerable<CheckResult>> CheckAll(IEnumerable<Check>  Checks,
                                                                    UInt16              ConcurrencyLimit    = 10,
                                                                    CancellationToken   CancellationToken   = default)
        {

            var checkResults = new List<CheckResult>(Checks.Count());

            using var semaphore = new SemaphoreSlim(ConcurrencyLimit);

            await foreach (var task in Task.WhenEach(
                Checks.Select(async check => {

                    await semaphore.WaitAsync();

                    try
                    {
                        return await Check(check, CancellationToken);
                    }

                    finally
                    {
                        semaphore.Release();
                    }

                })))
            {

                try
                {

                    var checkResult = await task;

                    if (checkResult is not null)
                        checkResults.Add(checkResult);

                    else {
                        // logger.LogError(ex, "Error processing task.");
                        continue;
                    }

                }
                catch (Exception ex)
                {
                    // logger.LogError(ex, "Error processing task.");
                    continue;
                }

            }

            return checkResults;

        }

        #endregion

        #region Check         (Check, ...)

        public static Task<CheckResult> Check(Check              Check,
                                              CancellationToken  CancellationToken   = default)
        {

            if      (Check.URL.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return CheckHTTPS(Check, CancellationToken);

            else if (Check.URL.StartsWith("smtp://",  StringComparison.OrdinalIgnoreCase))
                return CheckSMTP (Check, CancellationToken);

            else if (Check.URL.StartsWith("imap://",  StringComparison.OrdinalIgnoreCase))
                return CheckIMAP (Check, CancellationToken);

            else
                throw new ArgumentException($"Unsupported URL scheme in {Check.URL}", nameof(Check));

        }

        #endregion

        #region CheckHTTPS    (Check, ...)

        public static async Task<CheckResult> CheckHTTPS(Check              Check,
                                                         CancellationToken  CancellationToken   = default)
        {

            var checkResult = new CheckResult(
                                  Check.URL,
                                  Check.ExpireOk
                              );

            try
            {

                using (var certClient = new HttpClient(CreateHTTPClientHandler(checkResult)))
                {

                    var response = await certClient.GetAsync(Check.URL, CancellationToken);

                    if (response.IsSuccessStatusCode)
                        checkResult.Messages.Add($"HTTPS check successful for {Check.URL}");

                    else
                        checkResult.ErrorMessages.Add($"HTTPS check for {Check.URL} failed: {response.StatusCode}");

                }

            }
            catch (HttpRequestException ex)
            {
                checkResult.ErrorMessages.Add($"Connection to {Check.URL} failed: {ex.Message}");
            }
            catch (Exception ex)
            {
                checkResult.ErrorMessages.Add($"HTTPS check for {Check.URL} failed: {ex.Message}");
            }

            return checkResult;

        }

        #endregion

        #region CheckSMTP     (Check, ...)

        public static async Task<CheckResult> CheckSMTP(Check              Check,
                                                        CancellationToken  CancellationToken   = default)
        {

            var checkResult = new CheckResult(Check.URL, Check.ExpireOk);

            try
            {

                var cleanUrl  = Check.URL.Replace("smtp://", "");
                var parts     = cleanUrl.Split(':');
                var host      = parts[0];
                var port      = UInt16.Parse(parts[1]);

                using (var client = new TcpClient())
                {

                    await client.ConnectAsync(host, port);

                    using (var stream = client.GetStream())
                    using (var reader = new StreamReader(stream))
                    using (var writer = new StreamWriter(stream) { AutoFlush = true })
                    {

                        var response = await reader.ReadLineAsync(CancellationToken);
                        if (response?.StartsWith("220") == false)
                        {
                            checkResult.ErrorMessages.Add($"SMTP connection to {Check.URL} failed: {response}");
                            return checkResult;
                        }

                        await writer.WriteLineAsync($"EHLO {Environment.MachineName}");

                        // Read all 250-Response lines
                        var isLastLine       = false;
                        var supportsStartTls = false;
                        while (!isLastLine)
                        {

                            response = await reader.ReadLineAsync();

                            if (response is null)
                            {
                                checkResult.ErrorMessages.Add($"No response after EHLO for {Check.URL}");
                                return checkResult;
                            }
                            if (response.StartsWith("250-"))
                            {
                                if (response.Contains("STARTTLS"))
                                {
                                    supportsStartTls = true;
                                }
                            }
                            else if (response.StartsWith("250 "))
                            {
                                isLastLine = true;
                                if (response.Contains("STARTTLS"))
                                {
                                    supportsStartTls = true;
                                }
                            }
                            else
                            {
                                checkResult.ErrorMessages.Add($"EHLO for {Check.URL} failed: {response}");
                                return checkResult;
                            }
                        }

                        if (!supportsStartTls)
                        {
                            checkResult.ErrorMessages.Add($"Server {Check.URL} does not support the 'STARTTLS' command!");
                            return checkResult;
                        }

                        await writer.WriteLineAsync("STARTTLS");
                        response = await reader.ReadLineAsync(CancellationToken);
                        if (response?.StartsWith("220") == false)
                        {
                            checkResult.ErrorMessages.Add($"STARTTLS for {Check.URL} failed: {response}");
                            return checkResult;
                        }

                        using (var sslStream = new SslStream(stream, false, CreateRemoteCertificateValidationCallback(checkResult)))
                        {
                            await sslStream.AuthenticateAsClientAsync(host);
                            checkResult.Messages.Add($"SMTP STARTTLS connection successful for {Check.URL}");
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                checkResult.ErrorMessages.Add($"SMTP check for {Check.URL} failed: {ex.Message}");
            }

            return checkResult;

        }

        #endregion

        #region CheckIMAP     (Check, ...)

        public static async Task<CheckResult> CheckIMAP(Check              Check,
                                                        CancellationToken  CancellationToken   = default)
        {

            var checkResult = new CheckResult(Check.URL, Check.ExpireOk);

            try
            {

                var cleanUrl  = Check.URL.Replace("imap://", "");
                var parts     = cleanUrl.Split(':');
                var host      = parts[0];
                var port      = UInt16.Parse(parts[1]);

                using (var client = new TcpClient())
                {

                    await client.ConnectAsync(host, port);

                    using (var stream = client.GetStream())
                    using (var reader = new StreamReader(stream))
                    using (var writer = new StreamWriter(stream) { AutoFlush = true })
                    {

                        var response = await reader.ReadLineAsync(CancellationToken) ?? "";
                        // * OK [CAPABILITY IMAP4rev1 LITERAL+ ID ENABLE STARTTLS AUTH=DIGEST-MD5 AUTH=NTLM AUTH=CRAM-MD5 AUTH=LOGIN AUTH=PLAIN SASL-IR] mail Cyrus IMAP 3.4.2-dirty-Debian-3.4.2-2 server ready
                        if (response.StartsWith("* OK") == false)
                        {
                            checkResult.ErrorMessages.Add($"IMAP connection to {Check.URL} failed: {response}");
                            return checkResult;
                        }

                        if (response.Contains(" STARTTLS ") == false)
                        {
                            checkResult.ErrorMessages.Add($"Server {Check.URL} does not support the 'STARTTLS' command!");
                            return checkResult;
                        }

                        // "a1" is a tag to correlate requests and responses
                        await writer.WriteLineAsync("a1 STARTTLS");

                        response = await reader.ReadLineAsync(CancellationToken) ?? "";
                        if (response.StartsWith("a1 OK") == false)
                        {
                            checkResult.ErrorMessages.Add($"STARTTLS for {Check.URL} failed: {response}");
                            return checkResult;
                        }

                        using (var sslStream = new SslStream(stream, false, CreateRemoteCertificateValidationCallback(checkResult)))
                        {
                            await sslStream.AuthenticateAsClientAsync(host);
                            checkResult.Messages.Add($"IMAP STARTTLS connection successful for {Check.URL}");
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                checkResult.ErrorMessages.Add($"IMAP check for {Check.URL} failed: {ex.Message}");
            }

            return checkResult;

        }

        #endregion


        #region GetCertificateDisplayName (Certificate, IsLeaf)

        public static String GetCertificateDisplayName(X509Certificate2  Certificate,
                                                        Boolean           IsLeaf)
        {
            if (IsLeaf)
            {

                // Try the Subject Alternative Name (SAN)...
                var sanExtension = Certificate.Extensions.OfType<X509SubjectAlternativeNameExtension>().FirstOrDefault();
                if (sanExtension is not null)
                {

                    var dnsNames = sanExtension.EnumerateDnsNames();

                    if (dnsNames.Any())
                        return $"DNS: {string.Join(", ", dnsNames)}";

                }

                // ...or fallback to the common name (CN) in the subject
                var cn = GetCommonName(Certificate.Subject);
                return cn is not null ? $"CN: {cn}" : $"Subject: {Certificate.Subject}";

            }
            else
            {

                //var cn = GetCommonName(certificate.Subject);
                //return cn is not null ? $"CN: {cn}" : $"Issuer: {certificate.Issuer}";

                var organizationName = GetOrganization(Certificate.Subject);

                return organizationName is not null
                           ? organizationName
                           : Certificate.Issuer;

            }
        }

        #endregion

        #region GetCommonName             (DistinguishedName)

        public static String? GetCommonName(String DistinguishedName)
        {

            if (string.IsNullOrEmpty(DistinguishedName))
                return null;

            var parts = DistinguishedName.Split(',').Select(_ => _.Trim());
            foreach (var part in parts)
            {
                if (part.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
                    return part[3..];
            }

            return null;

        }

        #endregion

        #region GetOrganization           (DistinguishedName)

        public static String? GetOrganization(String DistinguishedName)
        {

            if (string.IsNullOrEmpty(DistinguishedName))
                return null;

            var parts = DistinguishedName.Split(',').Select(_ => _.Trim());
            foreach (var part in parts)
            {
                if (part.StartsWith("O=", StringComparison.OrdinalIgnoreCase))
                    return part[2..];
            }

            return null;

        }

        #endregion


    }

}
