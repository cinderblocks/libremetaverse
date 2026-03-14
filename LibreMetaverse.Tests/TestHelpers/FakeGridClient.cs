using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using OpenMetaverse;

namespace LibreMetaverse.Tests.TestHelpers
{
    /// <summary>
    /// Minimal fake HttpMessageHandler that returns pre-configured responses for test URIs.
    /// </summary>
    internal class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode Status, string Content, string MediaType)> _responses
            = new Dictionary<string, (HttpStatusCode, string, string)>();

        public void AddResponse(Uri uri, HttpStatusCode status, string content, string mediaType = "application/json")
        {
            _responses[uri.ToString()] = (status, content ?? string.Empty, mediaType ?? "application/json");
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request?.RequestUri != null && _responses.TryGetValue(request.RequestUri.ToString(), out var entry))
            {
                var resp = new HttpResponseMessage(entry.Status)
                {
                    Content = new StringContent(entry.Content)
                };
                resp.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(entry.MediaType);
                return Task.FromResult(resp);
            }

            // Default fallback -- NotFound
            var notFound = new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent(string.Empty)
            };
            return Task.FromResult(notFound);
        }
    }

    /// <summary>
    /// FakeGridClient is a lightweight test helper that derives from GridClient
    /// but replaces the HTTP client with a fake handler and shuts down the
    /// network subsystem to avoid background activity during unit tests.
    /// Use AddHttpResponse to configure canned responses for capability HTTP calls.
    /// </summary>
    public class FakeGridClient : GridClient
    {
        private readonly FakeHttpMessageHandler _fakeHandler;

        public FakeGridClient()
            : base()
        {
            // Stop any network activity started by the base constructor but keep the
            // Network object available so managers can register callbacks against it.
            try { Network?.Shutdown(NetworkManager.DisconnectType.ClientInitiated, "FakeGridClient"); } catch { }

            // Replace the HttpCapsClient with a fake-backed instance
            try { HttpCapsClient?.Dispose(); } catch { }
            _fakeHandler = new FakeHttpMessageHandler();
            HttpCapsClient = new LibreMetaverse.HttpCapsClient(_fakeHandler);
        }

        /// <summary>
        /// Configure a canned HTTP response for a specific URI.
        /// </summary>
        public void AddHttpResponse(Uri uri, HttpStatusCode status, string content, string mediaType = "application/json")
        {
            _fakeHandler.AddResponse(uri, status, content, mediaType);
        }

        /// <summary>
        /// Configure the simulated CurrentSim and set Inventory/Library capability URIs
        /// so tests that call capability lookup APIs will find the fake endpoints.
        /// </summary>
        public void SetInventoryAndLibraryCaps(Uri inventoryCap, Uri libraryCap)
        {
            // Ensure there is a CurrentSim object available
            if (Network.CurrentSim == null)
            {
                try
                {
                    var loopback = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0);
                    var sim = new OpenMetaverse.Simulator(this, loopback, 0);
                    Network.CurrentSim = sim;
                }
                catch { /* best-effort: if simulator construction fails, continue */ }
            }

            if (Network.CurrentSim != null)
            {
                // Initialize Caps for the simulator using a fake seed URI. The Caps
                // constructor will not attempt network seed requests if Network.Connected is false.
                try
                {
                    Network.CurrentSim.SetSeedCaps(new Uri("http://fake/seed"), true);
                }
                catch { }

                // Use reflection to inject capability URIs into the internal _Caps dictionary
                try
                {
                    var caps = Network.CurrentSim.Caps;
                    if (caps != null)
                    {
                        var capsType = caps.GetType();
                        var capsField = capsType.GetField("_Caps", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (capsField != null)
                        {
                            var dict = capsField.GetValue(caps) as System.Collections.Generic.Dictionary<string, Uri>;
                            if (dict != null)
                            {
                                if (inventoryCap != null) dict["InventoryAPIv3"] = inventoryCap;
                                if (libraryCap != null) dict["LibraryAPIv3"] = libraryCap;
                            }
                        }
                    }
                }
                catch { }
            }
        }
    }
}
