// Copyright (c) 2007-2016 CSJ2K contributors.
// Licensed under the BSD 3-Clause License.

using System;
using System.Linq;

namespace CSJ2K.Util
{
    public sealed class PortableImage : IImage
    {
        #region FIELDS

        private const int SizeOfArgb = 4;

        private readonly double[] _byteScaling;

        #endregion

        #region CONSTRUCTORS

        internal PortableImage(int width, int height, int numberOfComponents, int[] bitsUsed)
        {
            Width = width;
            Height = height;
            NumberOfComponents = numberOfComponents;
            _byteScaling = bitsUsed.Select(b => 255.0 / (1 << b)).ToArray();

            Data = new int[numberOfComponents * width * height];
        }

        #endregion

        #region PROPERTIES

        internal int Width { get; }

        internal int Height { get; }

        public int NumberOfComponents { get; }

        internal int[] Data { get; }

        #endregion

        #region METHODS

        public T As<T>()
        {
            var image = ImageFactory.New(Width, Height, ToBytes(Width, Height, NumberOfComponents, _byteScaling, Data));
            return image.As<T>();
        }

        public int[] GetComponent(int number)
        {
            if (number < 0 || number >= NumberOfComponents)
            {
                throw new ArgumentOutOfRangeException(nameof(number));
            }

            var length = Width * Height;
            var component = new int[length];

            for (int i = number, k = 0; k < length; i += NumberOfComponents, ++k)
            {
                component[k] = Data[i];
            }

            return component;
        }

        internal void FillRow(int rowIndex, int lineIndex, int rowWidth, int[] rowValues)
        {
            Array.Copy(
                rowValues,
                0,
                Data,
                NumberOfComponents * (rowIndex + lineIndex * rowWidth),
                rowValues.Length);
        }

        private static byte[] ToBytes(int width, int height, int numberOfComponents, double[] byteScaling, int[] data)
        {
            var count = numberOfComponents * width * height;
            var bytes = new byte[SizeOfArgb * width * height];

            switch (numberOfComponents)
            {
                case 1:
                    var scale = byteScaling[0];
                    for (int i = 0, j = 0; i < count; ++i)
                    {
                        var b = (byte)(scale * data[i]);
                        bytes[j++] = b;
                        bytes[j++] = b;
                        bytes[j++] = b;
                        bytes[j++] = 0xff;
                    }
                    break;
                case 3:
                    {
                        var scale0 = byteScaling[0];
                        var scale1 = byteScaling[1];
                        var scale2 = byteScaling[2];
                        for (int i = 0, j = 0; i < count;)
                        {
                            bytes[j++] = (byte)(scale0 * data[i++]);
                            bytes[j++] = (byte)(scale1 * data[i++]);
                            bytes[j++] = (byte)(scale2 * data[i++]);
                            bytes[j++] = 0xff;
                        }
                    }
                    break;
                case 4:
                    {
                        var scale0 = byteScaling[0];
                        var scale1 = byteScaling[1];
                        var scale2 = byteScaling[2];
                        var scale3 = byteScaling[3];
                        for (int i = 0, j = 0; i < count;)
                        {
                            bytes[j++] = (byte)(scale0 * data[i++]);
                            bytes[j++] = (byte)(scale1 * data[i++]);
                            bytes[j++] = (byte)(scale2 * data[i++]);
                            bytes[j++] = (byte)(scale3 * data[i++]);
                        }
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(numberOfComponents), $"Invalid number of components: {numberOfComponents}");
            }

            return bytes;
        }

        #endregion
    }
}
