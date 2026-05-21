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

using System.Formats.Asn1;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using org.GraphDefined.Vanaheimr.Hermod.SunSpecModbusTLS.Common;

namespace org.GraphDefined.Vanaheimr.Hermod.SunSpecModbusTLS.PKI;

/// <summary>
/// Generates a self-contained PKI for the demo:
///   1) Root CA  (offline trust anchor)
///   2) Issuing Device CA  (signs mbaps device/server leaf certificates)
///   3) Issuing Clients CA  (signs mbaps client leaf certificates)
///   4) Server cert  (issued by Issuing Device CA, has SAN incl. dNSName + 127.0.0.1)
///   5) Four client certs, one for each mandatory SunSpec role,
///      with the X.509v3 Role Extension (OID 1.3.6.1.4.1.50316.802.1, UTF8String).
///
/// All artefacts are written as PEM (.crt) and PKCS#12 (.pfx).
/// 
/// 
/// Notes:
///  1. SunSpec verlangt, dass mbaps Devices beim Zertifikat die komplette Zertifikatskette bis zur Root CA senden!
/// 
/// </summary>
public class ModbusPKI
{

    private const String DemoPfxPassword = "demo";

    public ModbusPKI()
    {

    }


    public Task BuildPKI(String outputDirectory = "pki")
    {

        var outDir = Path.Combine(Environment.CurrentDirectory, outputDirectory);
        Directory.CreateDirectory(outDir);

        Console.WriteLine($"[certgen] writing to {outDir}");




        // 1) Root CA
        var (rootCAPrivateKey, rootCACertificate) = BuildRootCA("CN=OCC SunSpec Modbus Root CA, O=Open Charging Cloud, C=DE");
        WriteCertAndKey(outDir, "ca", rootCACertificate, rootCAPrivateKey, includePfx: false);




        // 2) Issuing Device CA
        var (issuingDeviceCAPrivateKey, issuingDeviceCACertificate) = BuildIssuingCA(
                                                                        rootCAPrivateKey,
                                                                        rootCACertificate,
                                                                        "CN=OCC SunSpec Modbus Issuing Device CA, O=Open Charging Cloud, C=DE"
                                                                    );

        WriteCertAndKey(outDir, "issuing-device-ca", issuingDeviceCACertificate, issuingDeviceCAPrivateKey, includePfx: false);




        // 3) Issuing Clients CA
        var (issuingClientsCAPrivateKey, issuingClientsCACertificate) = BuildIssuingCA(
                                                                         rootCAPrivateKey,
                                                                         rootCACertificate,
                                                                         "CN=OCC SunSpec Modbus Issuing Clients CA, O=Open Charging Cloud, C=DE"
                                                                     );

        WriteCertAndKey(outDir, "issuing-clients-ca", issuingClientsCACertificate, issuingClientsCAPrivateKey, includePfx: false);




        // 4) Modbus Device Certificate
        var deviceDNSNames = new[] { "localhost", "EnergyMeter01.local" };

        using (var serverKey = ECDsa.Create(ECCurve.NamedCurves.nistP256))
        {

            var serverCert = IssueServerCert(
                                 issuingDeviceCAPrivateKey,
                                 issuingDeviceCACertificate,
                                 serverKey,
                                 Subject:      "CN=EnergyMeter01, O=OCC Energy Meters, C=DE",
                                 DNSNames:      deviceDNSNames,
                                 IPAddresses:   [ System.Net.IPAddress.Loopback, System.Net.IPAddress.IPv6Loopback ]
                             );

            WriteServerCertWithKey(
                outDir,
                "server",
                serverCert,
                serverKey,
                issuingDeviceCACertificate,
                rootCACertificate
            );

        }




        // 5) Four client certs, one per mandatory role
        foreach (var role in SunSpecRoles.AllMandatory)
        {

            using var clientKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);

            var cert = IssueClientCert(
                           issuingClientsCACertificate,
                           issuingClientsCAPrivateKey,
                           clientKey,
                           subject: $"CN=EnergyMeter-Client-{role}, O=OCC Energy Meters, C=DE",
                           role: role
                       );

            WriteClientCertWithKey(outDir, $"client-{role}", cert, clientKey, issuingClientsCACertificate, rootCACertificate);

        }




        // 6) Bonus: a client cert WITHOUT a role - useful for negative pentests
        using (var noRoleKey = ECDsa.Create(ECCurve.NamedCurves.nistP256))
        {

            var noRoleCert = IssueClientCert(
                                 issuingClientsCACertificate,
                                 issuingClientsCAPrivateKey,
                                 noRoleKey,
                                 subject: "CN=EnergyMeter-Client-NO-ROLE, O=OCC Energy Meters, C=DE",
                                 role: null
                              );

            WriteClientCertWithKey(outDir, "client-NO-ROLE", noRoleCert, noRoleKey, issuingClientsCACertificate, rootCACertificate);

        }

        Console.WriteLine("[certgen] done.");
        Console.WriteLine();
        Console.WriteLine("Files written:");

        foreach (var f in Directory.EnumerateFiles(outDir).OrderBy(x => x))
            Console.WriteLine("  " + Path.GetFileName(f));


        return Task.CompletedTask;

    }






    // ---------------- Cert building ----------------

    /// <summary>
    /// Build a self-signed Root CA with EC P-384.
    /// </summary>
    private static (ECDsa key, X509Certificate2 cert) BuildRootCA(String Subject)
    {

        var key                 = ECDsa.Create(
                                      ECCurve.NamedCurves.nistP384
                                  );

        var certificateRequest  = new CertificateRequest(
                                      Subject,
                                      key,
                                      HashAlgorithmName.SHA384
                                  );

        certificateRequest.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(
                certificateAuthority:     true,
                hasPathLengthConstraint:  true,
                pathLengthConstraint:     1,
                critical:                 true
            )
        );

        certificateRequest.CertificateExtensions.Add(
            new X509KeyUsageExtension(

                X509KeyUsageFlags.KeyCertSign |
                X509KeyUsageFlags.CrlSign,

                critical:                 true

            )
        );

        certificateRequest.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(

                certificateRequest.PublicKey,

                critical:                 false

            )
        );

        var notBefore  = DateTimeOffset.UtcNow.AddDays(-1);
        var notAfter   = DateTimeOffset.UtcNow.AddYears(10);

        var cert       = certificateRequest.CreateSelfSigned(
                             notBefore,
                             notAfter
                         );

        return (key, cert);

    }


    /// <summary>
    /// Build an issuing CA with EC P-384.
    /// </summary>
    private static (ECDsa key, X509Certificate2 cert) BuildIssuingCA(ECDsa             RootCAKey,
                                                                     X509Certificate2  RootCACert,
                                                                     String            Subject)
    {

        var key                 = ECDsa.Create(
                                      ECCurve.NamedCurves.nistP384
                                  );

        var certificateRequest  = new CertificateRequest(
                                      Subject,
                                      key,
                                      HashAlgorithmName.SHA384
                                  );

        certificateRequest.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(
                certificateAuthority:     true,
                hasPathLengthConstraint:  true,
                pathLengthConstraint:     0,
                critical:                 true
            )
        );

        certificateRequest.CertificateExtensions.Add(
            new X509KeyUsageExtension(

                X509KeyUsageFlags.KeyCertSign |
                X509KeyUsageFlags.CrlSign,

                critical: true

            )
        );

        certificateRequest.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(
                certificateRequest.PublicKey,
                critical: false
            )
        );

        certificateRequest.CertificateExtensions.Add(
            BuildAuthorityKeyIdentifier(RootCACert)
        );

        var serial     = RandomNumberGenerator.GetBytes(16);
        serial[0]     &= 0x7F;

        var notBefore  = DateTimeOffset.UtcNow.AddDays(-1);
        var notAfter   = DateTimeOffset.UtcNow.AddYears(5);

        var cert       = certificateRequest.Create(
                             RootCACert.SubjectName,
                             X509SignatureGenerator.CreateForECDsa(RootCAKey),
                             notBefore,
                             notAfter,
                             serial
                         );

        return (key, cert);

    }


    /// <summary>
    /// Server cert with TLS Web Server Authentication EKU and SAN.
    /// </summary>
    private static X509Certificate2 IssueServerCert(ECDsa                   IssuerKey,
                                                    X509Certificate2        IssuerCert,
                                                    ECDsa                   ServerKey,
                                                    String                  Subject,
                                                    String[]                DNSNames,
                                                    System.Net.IPAddress[]  IPAddresses)
    {

        var certificateRequest  = new CertificateRequest(
                                      Subject,
                                      ServerKey,
                                      HashAlgorithmName.SHA256
                                  );

        certificateRequest.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(

                certificateAuthority:     false,
                hasPathLengthConstraint:  false,
                pathLengthConstraint:     0,

                critical:                 true

            )
        );

        certificateRequest.CertificateExtensions.Add(
            new X509KeyUsageExtension(

                X509KeyUsageFlags.DigitalSignature,

                critical:                 true

            )
        );

        certificateRequest.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(

                [
                    new("1.3.6.1.5.5.7.3.1") /* serverAuth */
                ],

                critical: false

            )
        );


        var sanBuilder = new SubjectAlternativeNameBuilder();

        foreach (var dnsName   in DNSNames)
            sanBuilder.AddDnsName(dnsName);

        foreach (var ipAddress in IPAddresses)
            sanBuilder.AddIpAddress(ipAddress);

        certificateRequest.CertificateExtensions.Add(
            sanBuilder.Build()
        );


        certificateRequest.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(
                certificateRequest.PublicKey,
                false
            )
        );

        certificateRequest.CertificateExtensions.Add(
            BuildAuthorityKeyIdentifier(IssuerCert)
        );


        // Server certs MAY also carry a role (server's own role) per [MBTLS] §6.1
        // but per spec the client does NOT use it. We omit it here for clarity.


        var serial = RandomNumberGenerator.GetBytes(16);
        serial[0] &= 0x7F; // ensure positive
        var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
        var notAfter  = DateTimeOffset.UtcNow.AddYears(2);

        return certificateRequest.Create(
                   IssuerCert.SubjectName,
                   X509SignatureGenerator.CreateForECDsa(IssuerKey),
                   notBefore,
                   notAfter,
                   serial
               );

    }


    /// <summary>
    /// Client cert with clientAuth EKU and (optionally) the SunSpec Role Extension.
    /// </summary>
    private static X509Certificate2 IssueClientCert(X509Certificate2  issuerCert,
                                                    ECDsa             issuerKey,
                                                    ECDsa             clientKey,
                                                    String            subject,
                                                    String?           role)
    {

        var certificateRequest  = new CertificateRequest(subject, clientKey, HashAlgorithmName.SHA256);

        certificateRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, critical: true));

        certificateRequest.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature,
            critical: true));

        certificateRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new("1.3.6.1.5.5.7.3.2") /* clientAuth */ }, critical: false));

        if (role is not null)
            certificateRequest.CertificateExtensions.Add(BuildSunSpecRoleExtension(role));

        certificateRequest.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(certificateRequest.PublicKey, false));
        certificateRequest.CertificateExtensions.Add(BuildAuthorityKeyIdentifier(issuerCert));

        var serial = RandomNumberGenerator.GetBytes(16);
        serial[0] &= 0x7F;

        var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
        var notAfter  = DateTimeOffset.UtcNow.AddYears(2);

        return certificateRequest.Create(
                   issuerCert.SubjectName,
                   X509SignatureGenerator.CreateForECDsa(issuerKey),
                   notBefore,
                   notAfter,
                   serial
               );

    }







    /// <summary>
    /// Build the X.509v3 Role Extension per [MBTLS] §8.4 / SunSpecTCP-29..31:
    ///   OID   = 1.3.6.1.4.1.50316.802.1
    ///   value = ASN.1 UTF8String containing the role name
    /// non-critical (so legacy stacks still parse the cert).
    /// </summary>
    public static X509Extension BuildSunSpecRoleExtension(String ModbusRole)
    {

        var writer = new AsnWriter(
                         AsnEncodingRules.DER
                     );

        writer.WriteCharacterString(
            UniversalTagNumber.UTF8String,
            ModbusRole
        );

        return new X509Extension(
                   new Oid(
                       SunSpecRoles.RoleOid
                   ),
                   writer.Encode(),
                   critical: false
               );

    }

    /// <summary>
    /// Authority Key Identifier built from the issuer's SubjectKeyIdentifier.
    /// </summary>
    private static X509Extension BuildAuthorityKeyIdentifier(X509Certificate2 issuer)
    {

        var ski  = issuer.Extensions["2.5.29.14"] as X509SubjectKeyIdentifierExtension
                       ?? throw new InvalidOperationException("Issuer has no SubjectKeyIdentifier extension.");

        // AKI ::= SEQUENCE { keyIdentifier [0] OCTET STRING OPTIONAL, ... }
        var w    = new AsnWriter(AsnEncodingRules.DER);

        using (w.PushSequence())
        {
            w.WriteOctetString(
                Convert.FromHexString(ski.SubjectKeyIdentifier!),
                new Asn1Tag(
                    TagClass.ContextSpecific,
                    0,
                    isConstructed: false
                )
            );
        }

        return new X509Extension(
                   new Oid("2.5.29.35"),
                   w.Encode(),
                   critical: false
               );

    }









    // ---------------- File I/O ----------------

    private static void WriteCertAndKey(
        String dir, String baseName, X509Certificate2 cert, ECDsa key, Boolean includePfx)
    {
        File.WriteAllText(Path.Combine(dir, $"{baseName}.crt"), cert.ExportCertificatePem() + "\n");
        File.WriteAllText(Path.Combine(dir, $"{baseName}.key"), key.ExportPkcs8PrivateKeyPem() + "\n");
        if (includePfx)
            File.WriteAllBytes(Path.Combine(dir, $"{baseName}.pfx"),
                cert.CopyWithPrivateKey(key).Export(X509ContentType.Pfx, DemoPfxPassword));
        Console.WriteLine($"  + {baseName}.crt / {baseName}.key");
    }

    private static void WriteServerCertWithKey(String dir,
                                               String baseName,
                                               X509Certificate2 cert,
                                               ECDsa key,
                                               params X509Certificate2[] chainCertificates)
    {
        File.WriteAllText(Path.Combine(dir, $"{baseName}.crt"), cert.ExportCertificatePem() + "\n");
        WriteCertificateChain(dir, baseName, cert, chainCertificates);
        File.WriteAllText(Path.Combine(dir, $"{baseName}.key"), key.ExportPkcs8PrivateKeyPem() + "\n");
        var withKey = cert.CopyWithPrivateKey(key);
        File.WriteAllBytes(Path.Combine(dir, $"{baseName}.pfx"), ExportPfxWithChain(withKey, chainCertificates));
        Console.WriteLine($"  + {baseName}.crt / {baseName}.chain.crt / {baseName}.key / {baseName}.pfx");
    }

    private static void WriteClientCertWithKey(String dir,
                                               String baseName,
                                               X509Certificate2 cert,
                                               ECDsa key,
                                               params X509Certificate2[] chainCertificates)
    {
        File.WriteAllText(Path.Combine(dir, $"{baseName}.crt"), cert.ExportCertificatePem() + "\n");
        WriteCertificateChain(dir, baseName, cert, chainCertificates);
        File.WriteAllText(Path.Combine(dir, $"{baseName}.key"), key.ExportPkcs8PrivateKeyPem() + "\n");
        var withKey = cert.CopyWithPrivateKey(key);
        File.WriteAllBytes(Path.Combine(dir, $"{baseName}.pfx"), ExportPfxWithChain(withKey, chainCertificates));
        Console.WriteLine($"  + {baseName}.crt / {baseName}.chain.crt / {baseName}.key / {baseName}.pfx");
    }


    private static void WriteCertificateChain(String dir,
                                              String baseName,
                                              X509Certificate2 leafCertificate,
                                              params X509Certificate2[] chainCertificates)
    {

        File.WriteAllText(
            Path.Combine(dir, $"{baseName}.chain.crt"),
            String.Concat(
                new[] { leafCertificate }.
                    Concat(chainCertificates).
                    Select(certificate => certificate.ExportCertificatePem() + "\n")
            )
        );

    }


    private static Byte[] ExportPfxWithChain(X509Certificate2 leafCertificateWithKey,
                                             params X509Certificate2[] chainCertificates)
    {

        var collection = new X509Certificate2Collection {
            leafCertificateWithKey
        };

        foreach (var certificate in chainCertificates)
            collection.Add(certificate);

        return collection.Export(X509ContentType.Pfx, DemoPfxPassword);

    }


}
