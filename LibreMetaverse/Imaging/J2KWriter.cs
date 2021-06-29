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

        private readonly DelegateHandler<StreamRead> _ReadCallback;

        private readonly DelegateHandler<StreamWrite> _WriteCallback;

        private readonly DelegateHandler<StreamSeek> _SeekCallback;

        private readonly DelegateHandler<StreamSkip> _SkipCallback;

        private Codec _Codec;

        private CompressionParameters _CompressionParameters;

        private OpenJpegDotNet.Image _Image;

        private readonly Stream _Stream;

        #endregion

        #region Constructors

        public J2KWriter(byte[] data)
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

            this._WriteCallback = new DelegateHandler<StreamWrite>(Write);
            this._ReadCallback = new DelegateHandler<StreamRead>(Read);
            this._SeekCallback = new DelegateHandler<StreamSeek>(Seek);
            this._SkipCallback = new DelegateHandler<StreamSkip>(Skip);

            this._Stream = OpenJpeg.StreamDefaultCreate(true);
            OpenJpeg.StreamSetUserData(this._Stream, this._UserData);
            OpenJpeg.StreamSetUserDataLength(this._Stream, this._Buffer.Length);
            OpenJpeg.StreamSetReadFunction(this._Stream, this._ReadCallback);
            OpenJpeg.StreamSetWriteFunction(this._Stream, this._WriteCallback);
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

        private OpenJpegDotNet.Image Decode()
        {
            if (this._Image == null || this._Image.IsDisposed)
                throw new InvalidOperationException();

            if (!OpenJpeg.Decode(this._Codec, this._Stream, this._Image))
                throw new InvalidOperationException();

            return this._Image;
        }

        private Bitmap DecodeToBitmap()
        {
            if (this._Image == null || this._Image.IsDisposed)
                throw new InvalidOperationException();

            if (!OpenJpeg.Decode(this._Codec, this._Stream, this._Image))
                throw new InvalidOperationException();

            return this._Image.ToBitmap();
        }

        public bool WriteHeader(OpenJpegDotNet.IO.Parameter parameter)
        {
            if (parameter == null)
                throw new ArgumentNullException(nameof(parameter));

            this._Codec?.Dispose();
            this._CompressionParameters?.Dispose();
            this._Image?.Dispose();

            this._Codec = null;
            this._CompressionParameters = null;
            this._Image = null;

            this._Codec = OpenJpeg.CreateDecompress(CodecFormat.J2k);
            this._CompressionParameters = this.SetupEncoderParameters(parameter);

            return true;
        }

        public byte[] Encode(Bitmap bitmap)
        {
            if (bitmap == null) { throw new ArgumentNullException(nameof(bitmap)); }

            this._Codec?.Dispose();
            this._CompressionParameters?.Dispose();
            this._Image?.Dispose();

            var channels = 0;
            var outPrecision = 0u;
            var colorSpace = ColorSpace.Gray;
            var format = bitmap.PixelFormat;
            var width = bitmap.Width;
            var height = bitmap.Height;
            switch (format)
            {
                case PixelFormat.Format24bppRgb:
                    channels = 3;
                    colorSpace = ColorSpace.Srgb;
                    outPrecision = 24u / (uint)channels;
                    break;
                case PixelFormat.Format32bppArgb:
                    channels = 4;
                    colorSpace = ColorSpace.Srgb;
                    outPrecision = 32u / (uint)channels;
                    break;
                case PixelFormat.Format8bppIndexed:
                    channels = 1;
                    colorSpace = ColorSpace.Srgb;
                    outPrecision = 8u / (uint)channels;
                    break;
                default:
                    throw new NotSupportedException();
            }

            var componentParametersArray = new ImageComponentParameters[channels];
            for (var i = 0; i < channels; i++)
            {
                componentParametersArray[i].Precision = outPrecision;
                componentParametersArray[i].Bpp = outPrecision;
                componentParametersArray[i].Signed = false;
                componentParametersArray[i].Dx = (uint)this._CompressionParameters.SubsamplingDx;
                componentParametersArray[i].Dy = (uint)this._CompressionParameters.SubsamplingDy;
                componentParametersArray[i].Width = (uint)width;
                componentParametersArray[i].Height = (uint)height;
            }

            // ToDo: throw proper exception
            _Image = OpenJpeg.ImageCreate((uint)channels, componentParametersArray, colorSpace);
            if (_Image == null)
                throw new ArgumentException();

            // ToDo: support alpha components
            //switch (channels)
            //{
            //    case 2:
            //    case 4:
            //        image.Components[(int)(channels - 1)].Alpha = 1;
            //        break;
            //}

            _Image.X0 = 0;
            _Image.Y0 = 0;
            _Image.X1 = componentParametersArray[0].Dx * componentParametersArray[0].Width;
            _Image.Y1 = componentParametersArray[0].Dy * componentParametersArray[0].Height;


            //std::vector<OPJ_INT32*> outcomps(channels, nullptr);
            //switch (channels)
            //{
            //    case 1:
            //        outcomps.assign({ image.Components[0].data });
            //        break;
            //    // Reversed order for BGR -> RGB conversion
            //    case 2:
            //        outcomps.assign({ image.Components[0].data, image.Components[1].data });
            //        break;
            //    case 3:
            //        outcomps.assign({ image.Components[2].data, image.Components[1].data, image.Components[0].data });
            //        break;
            //    case 4:
            //        outcomps.assign({
            //        image.Components[2].data, image.Components[1].data, image.Components[0].data,
            //        image.Components[3].data });
            //        break;
            //}
            OpenJpeg.StartCompress(_Codec, _Image, _Stream);
            OpenJpeg.Encode(this._Codec, this._Stream);
            OpenJpeg.EndCompress(_Codec, _Stream);

            byte[] raw = new byte[_Buffer.Position];
            Marshal.Copy(_Buffer.Data, raw, 0, _Buffer.Position);
            return raw;
        }

        public OpenJpegDotNet.Image EncodeToJ2KImg(Bitmap bitmap)
        {
            if (bitmap == null) { throw new ArgumentNullException(nameof(bitmap)); }

            this._Codec?.Dispose();
            this._CompressionParameters?.Dispose();
            this._Image?.Dispose();

            var channels = 0;
            var outPrecision = 0u;
            var colorSpace = ColorSpace.Gray;
            var format = bitmap.PixelFormat;
            var width = bitmap.Width;
            var height = bitmap.Height;
            switch (format)
            {
                case PixelFormat.Format24bppRgb:
                    channels = 3;
                    colorSpace = ColorSpace.Srgb;
                    outPrecision = 24u / (uint)channels;
                    break;
                case PixelFormat.Format32bppArgb:
                    channels = 4;
                    colorSpace = ColorSpace.Srgb;
                    outPrecision = 32u / (uint)channels;
                    break;
                case PixelFormat.Format8bppIndexed:
                    channels = 1;
                    colorSpace = ColorSpace.Srgb;
                    outPrecision = 8u / (uint)channels;
                    break;
                default:
                    throw new NotSupportedException();
            }

            var componentParametersArray = new ImageComponentParameters[channels];
            for (var i = 0; i < channels; i++)
            {
                componentParametersArray[i].Precision = outPrecision;
                componentParametersArray[i].Bpp = outPrecision;
                componentParametersArray[i].Signed = false;
                componentParametersArray[i].Dx = (uint)this._CompressionParameters.SubsamplingDx;
                componentParametersArray[i].Dy = (uint)this._CompressionParameters.SubsamplingDy;
                componentParametersArray[i].Width = (uint)width;
                componentParametersArray[i].Height = (uint)height;
            }

            // ToDo: throw proper exception
            _Image = OpenJpeg.ImageCreate((uint)channels, componentParametersArray, colorSpace);
            if (_Image == null)
                throw new ArgumentException();

            // ToDo: support alpha components
            //switch (channels)
            //{
            //    case 2:
            //    case 4:
            //        image.Components[(int)(channels - 1)].Alpha = 1;
            //        break;
            //}

            _Image.X0 = 0;
            _Image.Y0 = 0;
            _Image.X1 = componentParametersArray[0].Dx * componentParametersArray[0].Width;
            _Image.Y1 = componentParametersArray[0].Dy * componentParametersArray[0].Height;


            //std::vector<OPJ_INT32*> outcomps(channels, nullptr);
            //switch (channels)
            //{
            //    case 1:
            //        outcomps.assign({ image.Components[0].data });
            //        break;
            //    // Reversed order for BGR -> RGB conversion
            //    case 2:
            //        outcomps.assign({ image.Components[0].data, image.Components[1].data });
            //        break;
            //    case 3:
            //        outcomps.assign({ image.Components[2].data, image.Components[1].data, image.Components[0].data });
            //        break;
            //    case 4:
            //        outcomps.assign({
            //        image.Components[2].data, image.Components[1].data, image.Components[0].data,
            //        image.Components[3].data });
            //        break;
            //}
            OpenJpeg.StartCompress(_Codec, _Image, _Stream);
            OpenJpeg.Encode(this._Codec, this._Stream);
            OpenJpeg.EndCompress(_Codec, _Stream);

            return _Image;
        }

        #region Event Handlers

        private static ulong Read(IntPtr buffer, ulong bytes, IntPtr userData)
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
