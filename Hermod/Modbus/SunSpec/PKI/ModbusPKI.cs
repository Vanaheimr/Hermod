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

using System.Security.Cryptography.X509Certificates;

using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;

using org.GraphDefined.Vanaheimr.Hermod.PKI;
using org.GraphDefined.Vanaheimr.Hermod.SunSpecModbusTLS.Common;

using BCx509 = Org.BouncyCastle.X509;

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
/// All artefacts are written as PEM (.crt) and, where the platform can hold the
/// key, PKCS#12 (.pfx).
///
///
/// Notes:
///  1. SunSpec verlangt, dass mbaps Devices beim Zertifikat die komplette Zertifikatskette bis zur Root CA senden!
///
/// </summary>
/// <remarks>
/// Every key here is made through <see cref="KeyAlgorithm"/> and every
/// certificate signed through <see cref="PKIFactory"/>, which is what lets this
/// build a chain in any of the kinds this library knows - including the ones
/// .NET cannot make at all. It used to call ECDsa.Create with a curve written
/// into the line, so the one corner of this library that generates a PKI was
/// also the one corner that could only generate what .NET can.
///
/// The defaults are unchanged: P-384 for the certificate authorities and P-256
/// for the leaves, which is what SunSpec deployments interoperate with today.
/// They are two parameters rather than one because they are two decisions - a
/// key that has to stand up for ten years is not the same question as one that
/// is replaced in two.
///
/// Whether the result can be written as PKCS#12 is a question about the
/// platform rather than about the certificate. .NET has no key object for an
/// Ed448 or an SLH-DSA key today, so such a chain is written as PEM and the
/// .pfx is left out with a line saying why, rather than the whole run failing
/// at the last step.
/// </remarks>
public class ModbusPKI
{

    private const String DemoPfxPassword = "demo";

    /// <summary>
    /// The kind of key the certificate authorities get when nothing else is
    /// said.
    /// </summary>
    public const String DefaultCAAlgorithm    = "ecdsa-p384";

    /// <summary>
    /// The kind of key the server and client certificates get when nothing
    /// else is said.
    /// </summary>
    public const String DefaultLeafAlgorithm  = "ecdsa-p256";

    public ModbusPKI()
    {

    }


    /// <summary>
    /// Build the whole PKI into the given directory.
    /// </summary>
    /// <param name="outputDirectory">Where the certificates end up.</param>
    /// <param name="DeviceName">The common name of the mbaps device, and the stem of its ".local" DNS name.</param>
    /// <param name="DNSNames">Further DNS names the device answers to, on top of "localhost" and "&lt;DeviceName&gt;.local".</param>
    /// <param name="IPAddresses">Further IP addresses the device answers on, on top of both loopback addresses.</param>
    /// <param name="CAAlgorithm">The kind of key the three certificate authorities get; P-384 when nothing is said.</param>
    /// <param name="LeafAlgorithm">The kind of key the server and client certificates get; P-256 when nothing is said.</param>
    /// <remarks>
    /// The defaults produce a certificate for a meter on the same machine. A
    /// meter reached over the network needs the name or the address that its
    /// clients dial, because everything but Hermod's own client - PLC4x among
    /// them - checks the subject alternative names.
    /// </remarks>
    public Task BuildPKI(String                             outputDirectory   = "pki",
                         String?                            DeviceName        = null,
                         IEnumerable<String>?               DNSNames          = null,
                         IEnumerable<System.Net.IPAddress>? IPAddresses       = null,
                         String?                            CAAlgorithm       = null,
                         String?                            LeafAlgorithm     = null)
    {

        var deviceName = String.IsNullOrWhiteSpace(DeviceName)
                             ? "EnergyMeter01"
                             : DeviceName.Trim();

        var forCAs     = Chosen(CAAlgorithm,   DefaultCAAlgorithm);
        var forLeaves  = Chosen(LeafAlgorithm, DefaultLeafAlgorithm);

        var outDir = Path.Combine(Environment.CurrentDirectory, outputDirectory);
        Directory.CreateDirectory(outDir);

        Console.WriteLine($"[certgen] writing to {outDir}");
        Console.WriteLine($"[certgen] authorities: {forCAs.Name}, leaves: {forLeaves.Name}");




        // 1) Root CA
        var rootCAKeyPair      = forCAs.Generate();

        var rootCACertificate  = PKIFactory.SignCertificate(
                                     CertificateTypes.RootCA,
                                     "OCC SunSpec Modbus Root CA, O=Open Charging Cloud, C=DE",
                                     rootCAKeyPair.Public,
                                     Issuing(rootCAKeyPair.Private, null),
                                     LifeTime:           TimeSpan.FromDays(3650),
                                     PathLenConstraint:  1
                                 );

        WriteCertAndKey(outDir, "ca", rootCACertificate, rootCAKeyPair.Private, includePfx: false);




        // 2) Issuing Device CA
        var issuingDeviceCAKeyPair      = forCAs.Generate();

        var issuingDeviceCACertificate  = PKIFactory.SignCertificate(
                                              CertificateTypes.IntermediateCA,
                                              "OCC SunSpec Modbus Issuing Device CA, O=Open Charging Cloud, C=DE",
                                              issuingDeviceCAKeyPair.Public,
                                              Issuing(rootCAKeyPair.Private, rootCACertificate),
                                              LifeTime:           TimeSpan.FromDays(1825),
                                              PathLenConstraint:  0
                                          );

        WriteCertAndKey(outDir, "issuing-device-ca", issuingDeviceCACertificate, issuingDeviceCAKeyPair.Private, includePfx: false);




        // 3) Issuing Clients CA
        var issuingClientsCAKeyPair      = forCAs.Generate();

        var issuingClientsCACertificate  = PKIFactory.SignCertificate(
                                               CertificateTypes.IntermediateCA,
                                               "OCC SunSpec Modbus Issuing Clients CA, O=Open Charging Cloud, C=DE",
                                               issuingClientsCAKeyPair.Public,
                                               Issuing(rootCAKeyPair.Private, rootCACertificate),
                                               LifeTime:           TimeSpan.FromDays(1825),
                                               PathLenConstraint:  0
                                           );

        WriteCertAndKey(outDir, "issuing-clients-ca", issuingClientsCACertificate, issuingClientsCAKeyPair.Private, includePfx: false);




        // 4) Modbus Device Certificate
        var deviceDNSNames = new[] { "localhost", $"{deviceName}.local" }.
                                 Concat(DNSNames ?? []).
                                 Where  (dnsName => !String.IsNullOrWhiteSpace(dnsName)).
                                 Select (dnsName => dnsName.Trim()).
                                 Distinct(StringComparer.OrdinalIgnoreCase).
                                 ToArray();

        var deviceIPAddresses = new[] { System.Net.IPAddress.Loopback, System.Net.IPAddress.IPv6Loopback }.
                                    Concat(IPAddresses ?? []).
                                    Distinct().
                                    ToArray();

        var serverKeyPair  = forLeaves.Generate();

        var serverCert     = PKIFactory.SignCertificate(
                                 CertificateTypes.Server,
                                 $"{deviceName}, O=OCC Energy Meters, C=DE",
                                 serverKeyPair.Public,
                                 Issuing(issuingDeviceCAKeyPair.Private, issuingDeviceCACertificate),
                                 SubjectAltNames:  [
                                                       .. deviceDNSNames.   Select(name => new GeneralName(GeneralName.DnsName,   name)),
                                                       .. deviceIPAddresses.Select(one  => new GeneralName(GeneralName.IPAddress, one.ToString()))
                                                   ],
                                 LifeTime:         TimeSpan.FromDays(730)
                             );

        WriteServerCertWithKey(
            outDir,
            "server",
            serverCert,
            serverKeyPair.Private,
            issuingDeviceCACertificate,
            rootCACertificate
        );




        // 5) Four client certs, one per mandatory role
        foreach (var role in SunSpecRoles.AllMandatory)
        {

            var clientKeyPair  = forLeaves.Generate();

            var cert           = IssueClientCert(
                                     issuingClientsCACertificate,
                                     issuingClientsCAKeyPair.Private,
                                     clientKeyPair.Public,
                                     $"{deviceName}-Client-{role}, O=OCC Energy Meters, C=DE",
                                     role
                                 );

            WriteClientCertWithKey(outDir, $"client-{role}", cert, clientKeyPair.Private, issuingClientsCACertificate, rootCACertificate);

        }




        // 6) Bonus: a client cert WITHOUT a role - useful for negative pentests
        var noRoleKeyPair  = forLeaves.Generate();

        var noRoleCert     = IssueClientCert(
                                 issuingClientsCACertificate,
                                 issuingClientsCAKeyPair.Private,
                                 noRoleKeyPair.Public,
                                 $"{deviceName}-Client-NO-ROLE, O=OCC Energy Meters, C=DE",
                                 null
                             );

        WriteClientCertWithKey(outDir, "client-NO-ROLE", noRoleCert, noRoleKeyPair.Private, issuingClientsCACertificate, rootCACertificate);

        Console.WriteLine("[certgen] done.");
        Console.WriteLine();
        Console.WriteLine("Files written:");

        foreach (var f in Directory.EnumerateFiles(outDir).OrderBy(x => x))
            Console.WriteLine("  " + Path.GetFileName(f));


        return Task.CompletedTask;

    }






    // ---------------- Cert building ----------------

    /// <summary>
    /// The kind of key that was asked for, or the one this generator uses when
    /// nothing was said.
    /// </summary>
    private static KeyAlgorithm Chosen(String? Asked, String Default)

        => KeyAlgorithm.Find(Asked ?? Default)
               ?? throw new ArgumentException($"'{Asked}' is not a kind of key this library makes. " +
                                              $"It makes: {String.Join(", ", KeyAlgorithm.All.Select(one => one.Id))}.");


    /// <summary>
    /// The pair PKIFactory wants for "who is signing this".
    /// </summary>
    private static Tuple<AsymmetricKeyParameter, BCx509.X509Certificate?> Issuing(AsymmetricKeyParameter   PrivateKey,
                                                                                  BCx509.X509Certificate?  Certificate)

        => new (PrivateKey, Certificate);


    /// <summary>
    /// Client cert with clientAuth EKU and (optionally) the SunSpec Role Extension.
    /// </summary>
    private static BCx509.X509Certificate IssueClientCert(BCx509.X509Certificate  IssuerCertificate,
                                                          AsymmetricKeyParameter  IssuerKey,
                                                          AsymmetricKeyParameter  ClientPublicKey,
                                                          String                  Subject,
                                                          String?                 Role)

        => PKIFactory.SignCertificate(
               CertificateTypes.Client,
               Subject,
               ClientPublicKey,
               Issuing(IssuerKey, IssuerCertificate),
               LifeTime:              TimeSpan.FromDays(730),
               AdditionalExtensions:  Role is not null
                                          ? [ BuildSunSpecRoleExtension(Role) ]
                                          : null
           );


    /// <summary>
    /// Build the X.509v3 Role Extension per [MBTLS] §8.4 / SunSpecTCP-29..31:
    ///   OID   = 1.3.6.1.4.1.50316.802.1
    ///   value = ASN.1 UTF8String containing the role name
    /// non-critical (so legacy stacks still parse the cert).
    /// </summary>
    public static AdditionalExtension BuildSunSpecRoleExtension(String ModbusRole)

        => new (SunSpecRoles.RoleOid,
                Critical: false,
                Value:    new DerUtf8String(ModbusRole));




    // ---------------- File I/O ----------------

    /// <summary>
    /// A certificate as PEM, which every kind of key can be written as.
    /// </summary>
    private static String AsPem(Object What)
    {

        using var text    = new StringWriter();
        var       writer  = new PemWriter(text);

        writer.WriteObject(What);
        writer.Writer.Flush();

        return text.ToString();

    }


    /// <summary>
    /// The certificate with its key attached, or null where this platform has
    /// no key object for that kind.
    /// </summary>
    /// <remarks>
    /// Null and a line rather than an exception that ends the run: a PKI in a
    /// kind .NET cannot hold is still a PKI, its PEM files are still what a
    /// device is given, and the missing .pfx is a fact about this machine.
    /// </remarks>
    private static X509Certificate2? WithKeyOrNothing(BCx509.X509Certificate  Certificate,
                                                      AsymmetricKeyParameter  PrivateKey,
                                                      String                  BaseName)
    {
        try
        {
            return PKIFactory.WithPrivateKey(
                       X509CertificateLoader.LoadCertificate(Certificate.GetEncoded()),
                       PrivateKey
                   );
        }
        catch (Exception e)
        {
            Console.WriteLine($"  ! {BaseName}.pfx left out: this runtime cannot hold that key ({e.GetType().Name}: {e.Message})");
            return null;
        }
    }


    private static void WriteCertAndKey(String                  dir,
                                        String                  baseName,
                                        BCx509.X509Certificate  cert,
                                        AsymmetricKeyParameter  key,
                                        Boolean                 includePfx)
    {

        File.WriteAllText(Path.Combine(dir, $"{baseName}.crt"), AsPem(cert));
        File.WriteAllText(Path.Combine(dir, $"{baseName}.key"), AsPem(key));

        if (includePfx && WithKeyOrNothing(cert, key, baseName) is X509Certificate2 withKey)
            File.WriteAllBytes(Path.Combine(dir, $"{baseName}.pfx"),
                               withKey.Export(X509ContentType.Pfx, DemoPfxPassword));

        Console.WriteLine($"  + {baseName}.crt / {baseName}.key");

    }

    private static void WriteServerCertWithKey(String                          dir,
                                               String                          baseName,
                                               BCx509.X509Certificate          cert,
                                               AsymmetricKeyParameter          key,
                                               params BCx509.X509Certificate[] chainCertificates)

        => WriteLeafCertWithKey(dir, baseName, cert, key, chainCertificates);

    private static void WriteClientCertWithKey(String                          dir,
                                               String                          baseName,
                                               BCx509.X509Certificate          cert,
                                               AsymmetricKeyParameter          key,
                                               params BCx509.X509Certificate[] chainCertificates)

        => WriteLeafCertWithKey(dir, baseName, cert, key, chainCertificates);

    /// <summary>
    /// A leaf certificate, its chain, its key, and a PKCS#12 where one can be
    /// made.
    /// </summary>
    /// <remarks>
    /// The server and the client used to have a copy each of this, identical
    /// down to the console line. They now share it: two copies of a file
    /// writer is two places to fix the day the layout changes.
    /// </remarks>
    private static void WriteLeafCertWithKey(String                  dir,
                                             String                  baseName,
                                             BCx509.X509Certificate  cert,
                                             AsymmetricKeyParameter  key,
                                             BCx509.X509Certificate[] chainCertificates)
    {

        File.WriteAllText(Path.Combine(dir, $"{baseName}.crt"), AsPem(cert));

        WriteCertificateChain(dir, baseName, cert, chainCertificates);

        File.WriteAllText(Path.Combine(dir, $"{baseName}.key"), AsPem(key));

        if (WithKeyOrNothing(cert, key, baseName) is X509Certificate2 withKey)
            File.WriteAllBytes(Path.Combine(dir, $"{baseName}.pfx"),
                               ExportPfxWithChain(withKey, chainCertificates));

        Console.WriteLine($"  + {baseName}.crt / {baseName}.chain.crt / {baseName}.key / {baseName}.pfx");

    }


    private static void WriteCertificateChain(String                   dir,
                                              String                   baseName,
                                              BCx509.X509Certificate   leafCertificate,
                                              BCx509.X509Certificate[] chainCertificates)
    {

        File.WriteAllText(
            Path.Combine(dir, $"{baseName}.chain.crt"),
            String.Concat(
                new[] { leafCertificate }.
                    Concat(chainCertificates).
                    Select(AsPem)
            )
        );

    }


    private static Byte[] ExportPfxWithChain(X509Certificate2         leafCertificateWithKey,
                                             BCx509.X509Certificate[] chainCertificates)
    {

        var collection = new X509Certificate2Collection {
            leafCertificateWithKey
        };

        foreach (var certificate in chainCertificates)
            collection.Add(X509CertificateLoader.LoadCertificate(certificate.GetEncoded()));

        return collection.Export(X509ContentType.Pfx, DemoPfxPassword);

    }


}
