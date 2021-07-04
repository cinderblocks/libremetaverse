/**
 * Copyright (c) 2021, Sjofn LLC.
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
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using OpenJpegDotNet;

namespace LibreMetaverse.Imaging
{
    public sealed class J2KWriter : IDisposable
    {

        #region Fields

        private readonly J2KBuffer _Buffer;

        private readonly IntPtr _UserData;

        private readonly DelegateHandler<StreamWrite> _WriteCallback;

        private readonly DelegateHandler<StreamSeek> _SeekCallback;

        private readonly DelegateHandler<StreamSkip> _SkipCallback;

        private Codec _Codec;

        private CompressionParameters _CompressionParameters;

        private OpenJpegDotNet.Image _Image;

        private readonly Stream _Stream;

        #endregion

        #region Constructors

        public J2KWriter(Bitmap bitmap)
        {
            _Image = ImageHelper.FromBitmap(bitmap);
            int datalen = (int)(_Image.X1 * _Image.Y1 * _Image.NumberOfComponents + 1024);

            this._Buffer = new J2KBuffer
            {
                Data = Marshal.AllocHGlobal(datalen),
                Length = datalen,
                Position = 0
            };

            var size = Marshal.SizeOf(this._Buffer);
            this._UserData = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(this._Buffer, this._UserData, false);

            this._WriteCallback = new DelegateHandler<StreamWrite>(Write);
            this._SeekCallback = new DelegateHandler<StreamSeek>(Seek);
            this._SkipCallback = new DelegateHandler<StreamSkip>(Skip);

            this._Stream = OpenJpeg.StreamCreate((ulong)_Buffer.Length, false);
            OpenJpeg.StreamSetUserData(this._Stream, this._UserData);
            OpenJpeg.StreamSetUserDataLength(this._Stream, this._Buffer.Length);
            OpenJpeg.StreamSetWriteFunction(this._Stream, this._WriteCallback);
            OpenJpeg.StreamSetSeekFunction(this._Stream, this._SeekCallback);
            OpenJpeg.StreamSetSkipFunction(this._Stream, this._SkipCallback);

            var compressionParameters = new CompressionParameters();
            OpenJpeg.SetDefaultEncoderParameters(compressionParameters);
            compressionParameters.TcpNumLayers = 1;
            compressionParameters.CodingParameterDistortionAllocation = 1;

            _Codec = OpenJpeg.CreateCompress(CodecFormat.J2k);
            OpenJpeg.SetupEncoder(_Codec, compressionParameters, _Image);
        }

        #endregion

        #region Properties

        public int Height
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets a value indicating whether this instance has been disposed.
        /// </summary>
        /// <returns>true if this instance has been disposed; otherwise, false.</returns>
        public bool IsDisposed
        {
            get;
            private set;
        }

        public int Width
        {
            get;
            private set;
        }

        #endregion

        #region Methods

        public byte[] Encode()
        {
            OpenJpeg.StartCompress(_Codec, _Image, _Stream);
            OpenJpeg.Encode(_Codec, _Stream);
            OpenJpeg.EndCompress(_Codec, _Stream);

            var datast = Marshal.PtrToStructure<J2KBuffer>(_UserData);
            var output = new byte[datast.Position];
            Marshal.Copy(_Buffer.Data, output, 0, output.Length);

            return output;
        }

        #region Event Handlers

        private static int Seek(ulong bytes, IntPtr userData)
        {
            unsafe
            {
                var buf = (J2KBuffer*)userData;
                var position = Math.Min((ulong)buf->Length, bytes);
                buf->Position = (int)position;
                return 1;
            }
        }

        private static long Skip(ulong bytes, IntPtr userData)
        {
            unsafe
            {
                var buf = (J2KBuffer*)userData;
                var bytesToSkip = (int)Math.Min((ulong)buf->Length, bytes);
                if (bytesToSkip > 0)
                {
                    buf->Position += bytesToSkip;
                    return bytesToSkip;
                }
                else
                {
                    return unchecked(-1);
                }
            }
        }

        private static ulong Write(IntPtr buffer, ulong bytes, IntPtr userData)
        {
            unsafe
            {
                var buf = (J2KBuffer*)userData;
                var bytesToRead = (int)Math.Min((ulong)buf->Length, bytes);
                if (bytesToRead > 0)
                {
                    NativeMethods.cstd_memcpy(buffer, IntPtr.Add(buf->Data, buf->Position), bytesToRead);
                    buf->Position += bytesToRead;
                    return (ulong)bytesToRead;
                }
                else
                {
                    return unchecked((ulong)-1);
                }
            }
        }

        #endregion

        #region Helpers

        private CompressionParameters SetupEncoderParameters(OpenJpegDotNet.IO.Parameter parameter)
        {
            var compressionParameters = new CompressionParameters();
            OpenJpeg.SetDefaultEncoderParameters(compressionParameters);

            if (parameter.Compression.HasValue)
                compressionParameters.TcpRates[0] = 1000f / Math.Min(Math.Max(parameter.Compression.Value, 1), 1000);

            compressionParameters.TcpNumLayers = 1;
            compressionParameters.CodingParameterDistortionAllocation = 1;

            if (!parameter.Compression.HasValue)
                compressionParameters.TcpRates[0] = 4;

            return compressionParameters;
        }

        #endregion

        #endregion

        #region IDisposable Members

        /// <summary>
        /// Releases all resources used by this <see cref="Reader"/>.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            //GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases all resources used by this <see cref="Reader"/>.
        /// </summary>
        /// <param name="disposing">Indicate value whether <see cref="IDisposable.Dispose"/> method was called.</param>
        private void Dispose(bool disposing)
        {
            if (this.IsDisposed)
            {
                return;
            }

            this.IsDisposed = true;

            if (disposing)
            {
                this._Codec?.Dispose();
                this._CompressionParameters?.Dispose();
                this._Stream.Dispose();

                Marshal.FreeHGlobal(this._Buffer.Data);
                Marshal.FreeHGlobal(this._UserData);
            }
        }

        #endregion

    }
}
