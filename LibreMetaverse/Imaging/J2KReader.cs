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
using System.Runtime.InteropServices;
using OpenJpegDotNet;

namespace LibreMetaverse.Imaging
{
    public sealed class J2KReader : IDisposable
    {

        #region Fields

        private readonly J2KBuffer _Buffer;

        private readonly IntPtr _UserData;

        private readonly DelegateHandler<StreamRead> _ReadCallback;

        private readonly DelegateHandler<StreamSeek> _SeekCallback;

        private readonly DelegateHandler<StreamSkip> _SkipCallback;

        private Codec _Codec;

        private DecompressionParameters _DecompressionParameters;

        private OpenJpegDotNet.Image _Image;

        private readonly Stream _Stream;

        #endregion

        #region Constructors

        public J2KReader(byte[] data)
        {
            this._Buffer = new J2KBuffer
            {
                Data = Marshal.AllocHGlobal(data.Length),
                Length = data.Length,
                Position = 0
            };

            Marshal.Copy(data, 0, this._Buffer.Data, this._Buffer.Length);

            var size = Marshal.SizeOf(this._Buffer);
            this._UserData = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(this._Buffer, this._UserData, false);

            this._ReadCallback = new DelegateHandler<StreamRead>(Read);
            this._SeekCallback = new DelegateHandler<StreamSeek>(Seek);
            this._SkipCallback = new DelegateHandler<StreamSkip>(Skip);

            this._Stream = OpenJpeg.StreamDefaultCreate(true);
            OpenJpeg.StreamSetUserData(this._Stream, this._UserData);
            OpenJpeg.StreamSetUserDataLength(this._Stream, this._Buffer.Length);
            OpenJpeg.StreamSetReadFunction(this._Stream, this._ReadCallback);
            OpenJpeg.StreamSetSeekFunction(this._Stream, this._SeekCallback);
            OpenJpeg.StreamSetSkipFunction(this._Stream, this._SkipCallback);
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

        public bool ReadHeader()
        {
            this._Codec?.Dispose();
            this._DecompressionParameters?.Dispose();
            this._Image?.Dispose();

            this._Codec = null;
            this._DecompressionParameters = null;
            this._Image = null;

            this._Codec = OpenJpeg.CreateDecompress(CodecFormat.J2k);
            this._DecompressionParameters = new DecompressionParameters();
            OpenJpeg.SetDefaultDecoderParameters(this._DecompressionParameters);

            if (!OpenJpeg.SetupDecoder(this._Codec, this._DecompressionParameters))
                return false;

            if (!OpenJpeg.ReadHeader(this._Stream, this._Codec, out var image))
                return false;

            this.Width = (int)(image.X1 - image.X0);
            this.Height = (int)(image.Y1 - image.Y0);
            this._Image = image;

            return true;
        }

        public OpenJpegDotNet.Image Decode()
        {
            if (this._Image == null || this._Image.IsDisposed)
                throw new InvalidOperationException();

            if (!OpenJpeg.Decode(this._Codec, this._Stream, this._Image))
                throw new InvalidOperationException();

            return this._Image;
        }

        public Bitmap DecodeToBitmap()
        {
            if (this._Image == null || this._Image.IsDisposed)
                throw new InvalidOperationException();

            if (!OpenJpeg.Decode(this._Codec, this._Stream, this._Image))
                throw new InvalidOperationException();

            return this._Image.ToBitmap();
        }

        #region Event Handlers

        private static ulong Read(IntPtr buffer, ulong bytes, IntPtr userData)
        {
            unsafe
            {
                var buf = (J2KBuffer*)userData;
                var bytesToRead = (int)Math.Min((ulong)(buf->Length - buf->Position), bytes);
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
                this._DecompressionParameters?.Dispose();
                this._Stream.Dispose();

                Marshal.FreeHGlobal(this._Buffer.Data);
                Marshal.FreeHGlobal(this._UserData);
            }
        }

        #endregion

    }
}
