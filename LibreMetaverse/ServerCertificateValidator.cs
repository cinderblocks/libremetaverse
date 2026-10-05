using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace LibreMetaverse
{
    /// <summary>
    /// Decides whether to trust the certificate a server presents, following <see cref="SecuritySettings"/>.
    /// </summary>
    internal static class ServerCertificateValidator
    {
        private static readonly Regex PemBlock = new Regex(
            @"-----BEGIN CERTIFICATE-----(?<body>[^-]+)-----END CERTIFICATE-----", RegexOptions.Compiled);

        private static readonly ConcurrentDictionary<string, byte> Reported = new ConcurrentDictionary<string, byte>();

        private static readonly object BundleLock = new object();
        private static string? _bundlePath;
        private static DateTime _bundleStamp;
        private static List<X509Certificate2> _bundle = new List<X509Certificate2>();

        public static bool Validate(SecuritySettings security, string host, X509Certificate2? cert,
            X509Chain? chain, SslPolicyErrors errors)
        {
            if (errors == SslPolicyErrors.None) { return true; }

            if (!security.VerifyServerCertificates)
            {
                Report(host, cert, errors, "Server certificate verification is disabled, accepting");
                return true;
            }

            // No certificate at all: nothing to pin or chain.
            if (cert == null || (errors & SslPolicyErrors.RemoteCertificateNotAvailable) != 0)
            {
                Report(host, cert, errors, "Rejecting server without a certificate");
                return false;
            }

            if (security.IsCertificateTrusted(Fingerprint(cert))) { return true; }

            if (!string.IsNullOrEmpty(security.CaBundlePath)
                && (errors & ~SslPolicyErrors.RemoteCertificateChainErrors) == 0
                && ChainsToBundle(cert, chain, security.CaBundlePath!))
            {
                return true;
            }

            var callback = security.CertificateValidationCallback;
            if (callback != null)
            {
                try
                {
                    if (callback(host, cert, errors)) { return true; }
                }
                catch (Exception ex)
                {
                    Logger.Warn("Certificate validation callback failed, rejecting certificate", ex);
                }
            }

            Report(host, cert, errors,
                "Rejecting server certificate. To connect anyway, add its fingerprint with " +
                "Settings.Security.TrustCertificate, supply Settings.Security.CaBundlePath, or set " +
                "Settings.Security.CertificateValidationCallback");
            return false;
        }

        public static string Fingerprint(X509Certificate2 cert)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(cert.RawData);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.Append(b.ToString("X2"));
                return sb.ToString();
            }
        }

        private static void Report(string host, X509Certificate2? cert, SslPolicyErrors errors, string message)
        {
            // once per server and certificate, so a failing server does not flood the log
            string fingerprint = cert == null ? "none" : Fingerprint(cert);
            if (Reported.TryAdd($"{host}|{fingerprint}|{errors}", 0))
                Logger.Warn($"{message}: host {host}, errors {errors}, SHA-256 {fingerprint}");
        }

        /// <summary>
        /// Does the certificate chain to a root in the bundle? Name mismatches have already been ruled out
        /// by the caller, so this only needs to establish the chain.
        /// </summary>
        private static bool ChainsToBundle(X509Certificate2 cert, X509Chain? presented, string bundlePath)
        {
            List<X509Certificate2> roots = LoadBundle(bundlePath);
            if (roots.Count == 0) { return false; }

            using (var chain = new X509Chain())
            {
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
                foreach (var root in roots) chain.ChainPolicy.ExtraStore.Add(root);
                if (presented != null)
                {
                    // intermediates the server sent
                    foreach (var element in presented.ChainElements)
                        chain.ChainPolicy.ExtraStore.Add(element.Certificate);
                }

                chain.Build(cert);

                if (chain.ChainElements.Count == 0) { return false; }

                // Any problem other than not trusting the root is a real failure (expired, bad signature, ...)
                foreach (var status in chain.ChainStatus)
                {
                    if (status.Status != X509ChainStatusFlags.NoError
                        && status.Status != X509ChainStatusFlags.UntrustedRoot)
                        return false;
                }

                // The chain has to end in a root from the bundle, not in whatever the server supplied.
                var top = chain.ChainElements[chain.ChainElements.Count - 1].Certificate;
                foreach (var root in roots)
                {
                    if (root.Thumbprint != null && root.Thumbprint.Equals(top.Thumbprint, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        private static List<X509Certificate2> LoadBundle(string path)
        {
            lock (BundleLock)
            {
                try
                {
                    DateTime stamp = File.GetLastWriteTimeUtc(path);
                    if (path == _bundlePath && stamp == _bundleStamp) { return _bundle; }

                    var certs = new List<X509Certificate2>();
                    string text = File.ReadAllText(path);
                    foreach (Match m in PemBlock.Matches(text))
                    {
                        try
                        {
                            certs.Add(new X509Certificate2(Convert.FromBase64String(
                                Regex.Replace(m.Groups["body"].Value, @"\s+", ""))));
                        }
                        catch (Exception ex) when (ex is FormatException || ex is CryptographicException)
                        {
                            Logger.Warn($"Skipping unreadable certificate in {path}: {ex.Message}");
                        }
                    }

                    _bundlePath = path;
                    _bundleStamp = stamp;
                    _bundle = certs;
                    return certs;
                }
                catch (Exception ex)
                {
                    Logger.Warn($"Could not read CA bundle {path}", ex);
                    return new List<X509Certificate2>();
                }
            }
        }
    }
}
