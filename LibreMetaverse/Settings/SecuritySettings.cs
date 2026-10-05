using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace LibreMetaverse
{
    /// <summary>
    /// Controls how the certificates presented by servers (login, capabilities, asset servers)
    /// are checked. By default a certificate has to be valid for the host and chain to a root
    /// the platform trusts, as it does in the Second Life viewer. The other settings allow
    /// specific exceptions for grids whose certificates do not.
    /// </summary>
    public class SecuritySettings
    {
        private readonly object _lock = new object();
        private readonly HashSet<string> _trustedThumbprints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Check the certificates of servers. Setting this to false accepts any certificate,
        /// which allows anyone on the network path to impersonate the grid and read login
        /// credentials and session capabilities. Prefer <see cref="CaBundlePath"/>,
        /// <see cref="TrustCertificate"/> or <see cref="CertificateValidationCallback"/>.
        /// Equivalent to the viewer's <c>NoVerifySSLCert</c> setting.
        /// </summary>
        public bool VerifyServerCertificates = true;

        /// <summary>
        /// Path to a PEM file of additional trusted root certificates, like the viewer's
        /// <c>ca-bundle.crt</c>. A server certificate that is valid for the host and chains to one
        /// of these roots is accepted even if the platform does not trust it, which is useful
        /// where the platform's own trust store is missing or incomplete, or for a grid with
        /// its own certificate authority. The file is re-read when it changes.
        /// </summary>
        public string? CaBundlePath;

        /// <summary>
        /// Called when a certificate fails the checks above, to let the application accept it,
        /// for example after asking the user. Receives the host name, the certificate and the
        /// reasons it was rejected. Called on a network thread, and not at all for certificates
        /// that pass or are pinned. The default (null) rejects.
        /// </summary>
        public Func<string, X509Certificate2, SslPolicyErrors, bool>? CertificateValidationCallback;

        /// <summary>
        /// Trust exactly one certificate, identified by its SHA-256 fingerprint (hex, with or without
        /// separators), whatever its chain or name, like a certificate the viewer's user has chosen
        /// to trust. This is the way to connect to a server with a self-signed certificate: the
        /// handshake proves the server holds that certificate's private key, which an
        /// impersonator would not.
        /// </summary>
        public void TrustCertificate(string sha256Fingerprint)
        {
            string normalized = NormalizeFingerprint(sha256Fingerprint);
            if (normalized.Length != 64)
                throw new ArgumentException("Expected a SHA-256 fingerprint (64 hex digits)", nameof(sha256Fingerprint));
            lock (_lock) _trustedThumbprints.Add(normalized);
        }

        /// <summary>Stop trusting a certificate added with <see cref="TrustCertificate"/></summary>
        public void UntrustCertificate(string sha256Fingerprint)
        {
            lock (_lock) _trustedThumbprints.Remove(NormalizeFingerprint(sha256Fingerprint));
        }

        /// <summary>Is this SHA-256 fingerprint trusted by <see cref="TrustCertificate"/></summary>
        public bool IsCertificateTrusted(string sha256Fingerprint)
        {
            lock (_lock) return _trustedThumbprints.Contains(NormalizeFingerprint(sha256Fingerprint));
        }

        internal static string NormalizeFingerprint(string fingerprint)
        {
            if (fingerprint == null) throw new ArgumentNullException(nameof(fingerprint));
            return fingerprint.Replace(":", "").Replace(" ", "").Replace("-", "").ToUpperInvariant();
        }
    }
}
