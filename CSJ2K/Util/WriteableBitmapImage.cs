// Copyright (c) 2007-2016 CSJ2K contributors.
// Licensed under the BSD 3-Clause License.

namespace CSJ2K.Util
{
#if NETFX_CORE
    using System.Runtime.InteropServices.WindowsRuntime;
    using Windows.UI.Xaml.Media;
    using Windows.UI.Xaml.Media.Imaging;
#elif SILVERLIGHT
    using System;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
#else
    using System.Runtime.InteropServices;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
#endif

    internal class WriteableBitmapImage : ImageBase<ImageSource>
    {
        #region CONSTRUCTORS

        internal WriteableBitmapImage(int width, int height, byte[] bytes)
            : base(width, height, bytes)
        {
        }

        #endregion

        #region METHODS

        protected override object GetImageObject()
        {
#if NETFX_CORE
            var wbm = new WriteableBitmap(this.Width, this.Height);
            this.Bytes.CopyTo(0, wbm.PixelBuffer, 0, this.Bytes.Length);
#elif SILVERLIGHT
            var wbm = new WriteableBitmap(this.Width, this.Height);
            Buffer.BlockCopy(this.Bytes, 0, wbm.Pixels, 0, this.Bytes.Length);
#else
            var wbm = new WriteableBitmap(this.Width, this.Height, 96.0, 96.0, PixelFormats.Pbgra32, null);
            wbm.Lock();
            Marshal.Copy(this.Bytes, 0, wbm.BackBuffer, this.Bytes.Length);
            wbm.AddDirtyRect(new Int32Rect(0, 0, this.Width, this.Height));
            wbm.Unlock();
#endif
            return wbm;
        }

        #endregion
    }
}
