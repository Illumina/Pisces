using System;
using System.Runtime.InteropServices;
using System.Security;

namespace Common.IO.Compression
{
    //[SuppressUnmanagedCodeSecurity] not supported in .net core
    public static class SafeNativeMethods
    {
        [DllImport("FileCompression", CallingConvention = CallingConvention.Cdecl)]
        public static extern int CompressBlock(byte[] uncompressedBlock, ref int uncompressedLen, byte[] compressedBlock,
                                               uint compressedLen, int compressionLevel);

        [DllImport("FileCompression", CallingConvention = CallingConvention.Cdecl)]
        public static extern int UncompressBlock(byte[] compressedBlock, uint compressedLen, byte[] uncompressedBlock,
                                                 uint uncompressedLen);

        [DllImport("FileCompression", CallingConvention = CallingConvention.Cdecl)]
        public static extern int gzclose(IntPtr fh);

        [DllImport("FileCompression", CallingConvention = CallingConvention.Cdecl)]
        public static extern int gzeof(IntPtr fh);

        [DllImport("FileCompression", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi,
            BestFitMapping = false)]
        public static extern IntPtr gzopen(string path, string mode);

        [DllImport("FileCompression", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int gzreadOffset(IntPtr fh, byte[] buffer, int offset, uint len);

        [DllImport("FileCompression", CallingConvention = CallingConvention.Cdecl)]
        public static extern int gzrewind(IntPtr fh);

        [DllImport("FileCompression", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int gzwriteOffset(IntPtr fh, byte[] buffer, int offset, uint len);
    }
}