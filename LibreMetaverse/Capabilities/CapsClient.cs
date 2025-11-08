/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2019-2024, Sjofn, LLC.
 * All rights reserved.
 *
 * - Redistribution and use in source and binary forms, with or without
 *   modification, are permitted provided that the following conditions are met:
 *
 * - Redistributions of source code must retain the above copyright notice, this
 *   list of conditions and the following disclaimer.
 * - Neither the name of the openmetaverse.co nor the names
 *   of its contributors may be used to endorse or promote products derived from
 *   this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
 * LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
 * CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
 * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
 * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
 * POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using LibreMetaverse;
using OpenMetaverse.StructuredData;

namespace OpenMetaverse.Http
{
    [Obsolete("Obsolete due to reliance on HttpWebRequest. Use LibreMetaverse.HttpCapsClient instead.")]
    public class CapsClient
    {
        public delegate void DownloadProgressCallback(CapsClient client, int bytesReceived, int totalBytesToReceive);
        public delegate void CompleteCallback(CapsClient client, OSD result, Exception error);

        public event DownloadProgressCallback OnDownloadProgress;
        public event CompleteCallback OnComplete;

        public object UserData;

        protected Uri _Address;
        protected string _CapName;
        protected byte[] _PostData;
        protected X509Certificate2 _ClientCert;
        protected string _ContentType;
        protected HttpWebRequest _Request;
        protected OSD _Response;
        protected AutoResetEvent _ResponseEvent = new AutoResetEvent(false);

        #region Constructors

        /// <summary>
        /// CapsClient Ctor 
        /// </summary>
        /// <param name="capability"><see cref="Uri"/> for simulator capability</param>
        public CapsClient(Uri capability)
            : this(capability, null, null)
        {
        }

        /// <summary>
        /// CapsClient Ctor with name
        /// </summary>
        /// <param name="capability"><see cref="Uri"/> for simulator capability</param>
        /// <param name="cap_name">Simulator capability name</param>
        public CapsClient(Uri capability, string cap_name)
            : this(capability, cap_name, null)
        {
        }

        /// <summary>
        /// CapsClient Ctor with name and certificate
        /// </summary>
        /// <param name="capability"><see cref="Uri"/> for simulator capability</param>
        /// <param name="cap_name">Simulator capability name</param>
        /// <param name="clientCert"><see cref="X509Certificate2"/> client certificate</param>
        public CapsClient(Uri capability, string cap_name, X509Certificate2 clientCert)
        {
            _Address = capability;
            _CapName = cap_name;
            _ClientCert = clientCert;
        }

        #endregion Constructors

        #region GET requests

        public void GetRequestAsync(int msTimeout)
        {
            if (_Request != null)
            {
                _Request.Abort();
                _Request = null;
            }

            _Request = CapsBase.GetStringAsync(_Address, _ClientCert, msTimeout, DownloadProgressHandler,
                    RequestCompletedHandler);
        }

        public OSD GetRequest(int msTimeout)
        {
            GetRequestAsync(msTimeout);
            _ResponseEvent.WaitOne(msTimeout, false);
            return _Response;
        }

        #endregion GET requests

        #region POST requests

        public void PostRequestAsync(OSD data, OSDFormat format, int msTimeout)
        {
            serializeData(data, format, out byte[] serializedData, out string contentType);
            PostRequestAsync(serializedData, contentType, msTimeout);
        }

        public OSD PostRequest(OSD data, OSDFormat format, int msTimeout)
        {
            PostRequestAsync(data, format, msTimeout);
            _ResponseEvent.WaitOne(msTimeout, false);
            return _Response;
        }

        public void PostRequestAsync(byte[] postData, string contentType, int msTimeout)
        {
            _PostData = postData;
            _ContentType = contentType;

            if (_Request != null)
            {
                _Request.Abort();
                _Request = null;
            }

            _Request = CapsBase.PostDataAsync(_Address, _ClientCert, contentType, postData, msTimeout, 
                null, DownloadProgressHandler, RequestCompletedHandler);
        }

        public OSD PostRequest(byte[] postData, string contentType, int msTimeout)
        {
            PostRequestAsync(postData, contentType, msTimeout);
            _ResponseEvent.WaitOne(msTimeout, false);
            return _Response;
        }

        #endregion POST requests

        #region PUT requests

        public void PutRequestAsync(OSD data, OSDFormat format, int msTimeout)
        {
            serializeData(data, format, out byte[] serializedData, out string contentType);
            PutRequestAsync(serializedData, contentType, msTimeout);
        }

        public OSD PutRequest(OSD data, OSDFormat format, int msTimeout)
        {
            PutRequestAsync(data, format, msTimeout);
            _ResponseEvent.WaitOne(msTimeout, false);
            return _Response;
        }

        public void PutRequestAsync(byte[] postData, string contentType, int msTimeout)
        {
            _PostData = postData;
            _ContentType = contentType;

            if (_Request != null)
            {
                _Request.Abort();
                _Request = null;
            }

            _Request = CapsBase.PutDataAsync(_Address, _ClientCert, contentType, postData, msTimeout,
                null, DownloadProgressHandler, RequestCompletedHandler);
        }

        public OSD PutRequest(byte[] postData, string contentType, int msTimeout)
        {
            PutRequestAsync(postData, contentType, msTimeout);
            _ResponseEvent.WaitOne(msTimeout, false);
            return _Response;
        }

        #endregion PUT requests

        #region PATCH requests

        public void PatchRequestAsync(OSD data, OSDFormat format, int msTimeout)
        {
            serializeData(data, format, out byte[] serializedData, out string contentType);
            PatchRequestAsync(serializedData, contentType, msTimeout);
        }

        public OSD PatchRequest(OSD data, OSDFormat format, int msTimeout)
        {
            PatchRequestAsync(data, format, msTimeout);
            _ResponseEvent.WaitOne(msTimeout, false);
            return _Response;
        }

        public void PatchRequestAsync(byte[] postData, string contentType, int msTimeout)
        {
            _PostData = postData;
            _ContentType = contentType;

            if (_Request != null)
            {
                _Request.Abort();
                _Request = null;
            }

            _Request = CapsBase.PatchDataAsync(_Address, _ClientCert, contentType, postData, msTimeout,
                null, DownloadProgressHandler, RequestCompletedHandler);
        }

        public OSD PatchRequest(byte[] postData, string contentType, int msTimeout)
        {
            PatchRequestAsync(postData, contentType, msTimeout);
            _ResponseEvent.WaitOne(msTimeout, false);
            return _Response;
        }

        #endregion PATCH requests

        #region DELETE requests

        public void DeleteRequestAsync(OSD data, OSDFormat format, int msTimeout)
        {
            serializeData(data, format, out byte[] serializedData, out string contentType);
            DeleteRequestAsync(serializedData, contentType, msTimeout);
        }

        public OSD DeleteRequest(OSD data, OSDFormat format, int msTimeout)
        {
            DeleteRequestAsync(data, format, msTimeout);
            _ResponseEvent.WaitOne(msTimeout, false);
            return _Response;
        }

        public void DeleteRequestAsync(byte[] postData, string contentType, int msTimeout)
        {
            _PostData = postData;
            _ContentType = contentType;

            if (_Request != null)
            {
                _Request.Abort();
                _Request = null;
            }

            _Request = CapsBase.DeleteDataAsync(_Address, _ClientCert, contentType, postData, msTimeout,
                null, DownloadProgressHandler, RequestCompletedHandler);
        }

        public OSD DeleteRequest(byte[] postData, string contentType, int msTimeout)
        {
            DeleteRequestAsync(postData, contentType, msTimeout);
            _ResponseEvent.WaitOne(msTimeout, false);
            return _Response;
        }

        #endregion DELETE requests

        public void Cancel()
        {
            _Request?.Abort();
        }

        void DownloadProgressHandler(HttpWebRequest request, HttpWebResponse response, int bytesReceived, int totalBytesToReceive)
        {
            _Request = request;

            if (OnDownloadProgress != null)
            {
                try { OnDownloadProgress(this, bytesReceived, totalBytesToReceive); }
                catch (Exception ex) { Logger.Log(ex.Message, Helpers.LogLevel.Error, ex); }
            }
        }

        void RequestCompletedHandler(HttpWebRequest request, HttpWebResponse response, byte[] responseData, Exception error)
        {
            _Request = request;

            OSD result = null;

            if (responseData != null)
            {
                try { result = OSDParser.Deserialize(responseData); }
                catch (Exception ex) { error = ex; }
            }

            FireCompleteCallback(result, error);
        }

        /// <summary>
        /// Serializes OSD data for http request
        /// </summary>
        /// <param name="data"><see cref="OSD"/>formatted data for input</param>
        /// <param name="format">Format to serialize data to</param>
        /// <param name="serializedData">Output serialized data as byte array</param>
        /// <param name="contentType">content-type string of serialized data</param>
        private void serializeData(OSD data, OSDFormat format, out byte[] serializedData, out string contentType)
        {
            switch (format)
            {
                case OSDFormat.Xml:
                    serializedData = OSDParser.SerializeLLSDXmlBytes(data);
                    contentType = HttpCapsClient.LLSD_XML;
                    break;
                case OSDFormat.Binary:
                    serializedData = OSDParser.SerializeLLSDBinary(data);
                    contentType = HttpCapsClient.LLSD_BINARY;
                    break;
                case OSDFormat.Json:
                default:
                    serializedData = System.Text.Encoding.UTF8.GetBytes(OSDParser.SerializeJsonString(data));
                    contentType = HttpCapsClient.LLSD_JSON;
                    break;
            }
        }

        private void FireCompleteCallback(OSD result, Exception error)
        {
            CompleteCallback callback = OnComplete;
            if (callback != null)
            {
                try
                {
                    callback(this, result, error);
                }
                catch (Exception ex)
                {
                    Logger.DebugLog($"CapsBase.GetResponse() {_CapName} : {ex.Message}");
                    Logger.Log(ex.Message, Helpers.LogLevel.Error, ex);
                }
            }

            _Response = result;
            _ResponseEvent.Set();
        }
    }
}
