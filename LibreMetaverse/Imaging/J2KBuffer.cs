using System;
using System.Runtime.InteropServices;

namespace LibreMetaverse.Imaging
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct J2KBuffer
    {

        public IntPtr Data;

        public int Length;

        public int Position;

    }
}
