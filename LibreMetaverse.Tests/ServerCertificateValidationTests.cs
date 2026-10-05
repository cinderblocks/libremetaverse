using System;
using System.IO;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    [TestFixture]
    public class ServerCertificateValidationTests
    {
        private const SslPolicyErrors ChainErrors = SslPolicyErrors.RemoteCertificateChainErrors;
        private readonly System.Collections.Generic.List<string> tempFiles = new System.Collections.Generic.List<string>();

        [TearDown]
        public void Cleanup()
        {
            foreach (var f in tempFiles) { try { File.Delete(f); } catch { } }
            tempFiles.Clear();
        }

        #region helpers

        private static X509Certificate2 SelfSigned(string name, bool isCa = false)
        {
            using (var key = RSA.Create(2048))
            {
                var req = new CertificateRequest("CN=" + name, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                req.CertificateExtensions.Add(new X509BasicConstraintsExtension(isCa, false, 0, true));
                return req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-400), DateTimeOffset.UtcNow.AddDays(400));
            }
        }

        private static X509Certificate2 Leaf(X509Certificate2 issuer, string dns, int validDays = 30)
        {
            using (var key = RSA.Create(2048))
            {
                var req = new CertificateRequest("CN=" + dns, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                var san = new SubjectAlternativeNameBuilder();
                san.AddDnsName(dns);
                req.CertificateExtensions.Add(san.Build());
                var serial = new byte[8];
                new Random(5).NextBytes(serial);
                var notBefore = DateTimeOffset.UtcNow.AddDays(validDays > 0 ? -1 : -30);
                var notAfter = validDays > 0 ? DateTimeOffset.UtcNow.AddDays(validDays) : DateTimeOffset.UtcNow.AddDays(-5);
                using (var signed = req.Create(issuer, notBefore, notAfter, serial))
                    return new X509Certificate2(signed.RawData);
            }
        }

        private string Bundle(params X509Certificate2[] certs)
        {
            string path = Path.GetTempFileName();
            tempFiles.Add(path);
            var sb = new System.Text.StringBuilder();
            foreach (var c in certs)
            {
                sb.AppendLine("-----BEGIN CERTIFICATE-----");
                sb.AppendLine(Convert.ToBase64String(c.RawData, Base64FormattingOptions.InsertLineBreaks));
                sb.AppendLine("-----END CERTIFICATE-----");
            }
            File.WriteAllText(path, sb.ToString());
            return path;
        }

        private static bool Validate(SecuritySettings s, X509Certificate2 cert, SslPolicyErrors errors, string host = "sim.example.org")
        {
            using (var chain = new X509Chain())
            {
                chain.Build(cert);
                return ServerCertificateValidator.Validate(s, host, cert, chain, errors);
            }
        }

        #endregion

        [Test]
        public void ValidCertificate_IsAccepted()
        {
            Assert.That(ServerCertificateValidator.Validate(new SecuritySettings(), "h", null, null, SslPolicyErrors.None), Is.True);
        }

        [Test]
        public void SelfSignedCertificate_IsRejectedByDefault()
        {
            using (var cert = SelfSigned("sim.example.org"))
                Assert.That(Validate(new SecuritySettings(), cert, ChainErrors), Is.False);
        }

        [Test]
        public void VerificationDisabled_AcceptsAnything()
        {
            using (var cert = SelfSigned("sim.example.org"))
                Assert.That(Validate(new SecuritySettings { VerifyServerCertificates = false }, cert, ChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch), Is.True);
        }

        [Test]
        public void MissingCertificate_IsRejected()
        {
            Assert.That(ServerCertificateValidator.Validate(new SecuritySettings(), "h", null, null,
                SslPolicyErrors.RemoteCertificateNotAvailable), Is.False);
        }

        #region pinning

        [TestCase("")]
        [TestCase(":")]
        [TestCase("lower")]
        [TestCase("-")]
        public void PinnedSelfSignedCertificate_IsAccepted_InAnyFingerprintFormat(string format)
        {
            using (var cert = SelfSigned("sim.example.org"))
            {
                string fp = ServerCertificateValidator.Fingerprint(cert);
                string formatted = format == "lower" ? fp.ToLowerInvariant()
                    : format == "" ? fp
                    : string.Join(format, System.Linq.Enumerable.Select(System.Linq.Enumerable.Range(0, 32), i => fp.Substring(i * 2, 2)));

                var s = new SecuritySettings();
                s.TrustCertificate(formatted);

                Assert.That(Validate(s, cert, ChainErrors), Is.True);
                Assert.That(Validate(s, cert, ChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch), Is.True,
                    "a pinned certificate identifies the server whatever name it is reached by");
            }
        }

        [Test]
        public void PinningOneCertificate_DoesNotTrustAnother()
        {
            using (var pinned = SelfSigned("a.example.org"))
            using (var other = SelfSigned("a.example.org"))
            {
                var s = new SecuritySettings();
                s.TrustCertificate(ServerCertificateValidator.Fingerprint(pinned));

                Assert.That(Validate(s, pinned, ChainErrors), Is.True);
                Assert.That(Validate(s, other, ChainErrors), Is.False);
            }
        }

        [Test]
        public void Untrusting_RemovesThePin()
        {
            using (var cert = SelfSigned("sim.example.org"))
            {
                var s = new SecuritySettings();
                string fp = ServerCertificateValidator.Fingerprint(cert);
                s.TrustCertificate(fp);
                Assert.That(s.IsCertificateTrusted(fp.ToLowerInvariant()), Is.True);

                s.UntrustCertificate(fp);

                Assert.That(Validate(s, cert, ChainErrors), Is.False);
            }
        }

        [Test]
        public void TrustCertificate_RejectsAnythingButASha256Fingerprint()
        {
            var s = new SecuritySettings();
            Assert.Throws<ArgumentException>(() => s.TrustCertificate("DEADBEEF"));
            Assert.Throws<ArgumentException>(() => s.TrustCertificate(new string('A', 40))); // SHA-1 length
        }

        #endregion

        #region CA bundle

        [Test]
        public void CertificateIssuedByABundleRoot_IsAccepted()
        {
            using (var ca = SelfSigned("Test Grid CA", isCa: true))
            using (var leaf = Leaf(ca, "sim.example.org"))
            {
                var s = new SecuritySettings { CaBundlePath = Bundle(ca) };
                Assert.That(Validate(s, leaf, ChainErrors), Is.True);
            }
        }

        [Test]
        public void CertificateIssuedByARootNotInTheBundle_IsRejected()
        {
            using (var ca = SelfSigned("Test Grid CA", isCa: true))
            using (var otherCa = SelfSigned("Some Other CA", isCa: true))
            using (var leaf = Leaf(ca, "sim.example.org"))
            {
                Assert.That(Validate(new SecuritySettings(), leaf, ChainErrors), Is.False, "no bundle");
                Assert.That(Validate(new SecuritySettings { CaBundlePath = Bundle(otherCa) }, leaf, ChainErrors), Is.False, "wrong bundle");
            }
        }

        [Test]
        public void BundleDoesNotExcuseANameMismatch()
        {
            using (var ca = SelfSigned("Test Grid CA", isCa: true))
            using (var leaf = Leaf(ca, "sim.example.org"))
            {
                var s = new SecuritySettings { CaBundlePath = Bundle(ca) };
                Assert.That(Validate(s, leaf, ChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch), Is.False);
            }
        }

        [Test]
        public void BundleDoesNotExcuseAnExpiredCertificate()
        {
            using (var ca = SelfSigned("Test Grid CA", isCa: true))
            using (var leaf = Leaf(ca, "sim.example.org", validDays: -1))
            {
                var s = new SecuritySettings { CaBundlePath = Bundle(ca) };
                Assert.That(Validate(s, leaf, ChainErrors), Is.False);
            }
        }

        [Test]
        public void SelfSignedLeafInTheBundle_IsAcceptedAsItsOwnRoot()
        {
            using (var cert = SelfSigned("sim.example.org", isCa: true))
            {
                var s = new SecuritySettings { CaBundlePath = Bundle(cert) };
                Assert.That(Validate(s, cert, ChainErrors), Is.True);
            }
        }

        [Test]
        public void MissingOrEmptyBundle_RejectsWithoutThrowing()
        {
            using (var cert = SelfSigned("sim.example.org"))
            {
                Assert.That(Validate(new SecuritySettings { CaBundlePath = "/nonexistent/ca.pem" }, cert, ChainErrors), Is.False);
                string empty = Path.GetTempFileName();
                tempFiles.Add(empty);
                Assert.That(Validate(new SecuritySettings { CaBundlePath = empty }, cert, ChainErrors), Is.False);
            }
        }

        [Test]
        public void BundleIsReloadedWhenItChanges()
        {
            using (var ca = SelfSigned("Test Grid CA", isCa: true))
            using (var leaf = Leaf(ca, "sim.example.org"))
            {
                string path = Bundle();
                var s = new SecuritySettings { CaBundlePath = path };
                Assert.That(Validate(s, leaf, ChainErrors), Is.False);

                File.Copy(Bundle(ca), path, true);
                File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));

                Assert.That(Validate(s, leaf, ChainErrors), Is.True);
            }
        }

        #endregion

        #region callback

        [Test]
        public void Callback_CanAcceptARejectedCertificate()
        {
            using (var cert = SelfSigned("sim.example.org"))
            {
                string seenHost = null;
                SslPolicyErrors seenErrors = 0;
                var s = new SecuritySettings
                {
                    CertificateValidationCallback = (host, c, errors) => { seenHost = host; seenErrors = errors; return true; }
                };

                Assert.That(Validate(s, cert, ChainErrors, "grid.example.org"), Is.True);
                Assert.That(seenHost, Is.EqualTo("grid.example.org"));
                Assert.That(seenErrors, Is.EqualTo(ChainErrors));
            }
        }

        [Test]
        public void Callback_CanDecline_AndFailureCountsAsDeclining()
        {
            using (var cert = SelfSigned("sim.example.org"))
            {
                Assert.That(Validate(new SecuritySettings { CertificateValidationCallback = (h, c, e) => false }, cert, ChainErrors), Is.False);
                Assert.That(Validate(new SecuritySettings { CertificateValidationCallback = (h, c, e) => throw new InvalidOperationException() }, cert, ChainErrors), Is.False);
            }
        }

        [Test]
        public void Callback_IsNotConsultedForValidOrPinnedCertificates()
        {
            using (var cert = SelfSigned("sim.example.org"))
            {
                int calls = 0;
                var s = new SecuritySettings { CertificateValidationCallback = (h, c, e) => { calls++; return false; } };
                s.TrustCertificate(ServerCertificateValidator.Fingerprint(cert));

                Assert.That(Validate(s, cert, ChainErrors), Is.True);
                Assert.That(ServerCertificateValidator.Validate(s, "h", cert, null, SslPolicyErrors.None), Is.True);
                Assert.That(calls, Is.EqualTo(0));
            }
        }

        #endregion

        [Test]
        public void DefaultSettings_AreStrict()
        {
            var s = new SecuritySettings();
            Assert.That(s.VerifyServerCertificates, Is.True);
            Assert.That(s.CaBundlePath, Is.Null);
            Assert.That(s.CertificateValidationCallback, Is.Null);
        }
    }
}
