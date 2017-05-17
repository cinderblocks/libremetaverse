// Copyright (c) 2007-2016 CSJ2K contributors.
// Licensed under the BSD 3-Clause License.

namespace CSJ2K.Util
{
    using System;
    using System.Reflection;


    public abstract class ImageBase<TBase> : IImage
    {
        #region FIELDS

        protected const int SizeOfArgb = 4;

        #endregion

        #region CONSTRUCTORS

        protected ImageBase(int width, int height, byte[] bytes)
        {
            Width = width;
            Height = height;
            Bytes = bytes;
        }

        #endregion

        #region PROPERTIES

        protected int Width { get; }

        protected int Height { get; }

        protected byte[] Bytes { get; }

        #endregion

        #region METHODS

        public virtual T As<T>()
        {
#if NETFX_CORE || NETSTANDARD
            if (!typeof(TBase).GetTypeInfo().IsAssignableFrom(typeof(T).GetTypeInfo()))
#else
            if (!typeof(TBase).IsAssignableFrom(typeof(T)))
#endif
            {
                throw new InvalidCastException(
                    $"Cannot cast to '{typeof(T).Name}'; type must be assignable from '{typeof(TBase).Name}'");
            }

            return (T)GetImageObject();
        }

        protected abstract object GetImageObject();

        #endregion
    }
}
