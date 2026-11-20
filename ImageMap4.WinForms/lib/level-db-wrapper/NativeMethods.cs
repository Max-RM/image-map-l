using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LevelDBWrapper
{
    internal static class NativeMethods
    {
        public static readonly Encoding Encoding = Encoding.UTF8;

        static NativeMethods()
        {
            // 15035 / .NET 4.5-4.6 mscorlib has no RuntimeInformation.Architecture.
            string cpu = (Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") ?? "").ToUpperInvariant();
            int platform = (int)Environment.OSVersion.Platform;
            bool unix = platform == 4 || platform == 6 || platform == 128;
            string source;
            string target;
            if (!unix)
            {
                if (cpu == "ARM64")
                    source = "leveldb_mcpe_win_arm64.dll";
                else if (cpu == "ARM")
                    source = "leveldb_mcpe_win_arm.dll";
                else
                    source = Environment.Is64BitProcess ? "leveldb_mcpe_win_amd64.dll" : "leveldb_mcpe_win32.dll";
                target = "leveldb.dll";
            }
            else if (cpu == "ARM64" || cpu == "AARCH64")
            {
                source = "leveldb_mcpe_linux_arm.so";
                target = "leveldb.so";
            }
            else if (Directory.Exists("/System/Library"))
            {
                source = "leveldb_mcpe_macosx_10_9_x86_64.dylib";
                target = "leveldb.dylib";
            }
            else
            {
                source = "leveldb_mcpe_linux_x86_64.so";
                target = "leveldb.so";
            }

            string folder = AppDomain.CurrentDomain.BaseDirectory;
            string dest = Path.Combine(folder, target);
            string srcPath = Path.Combine(folder, source);
            if (!File.Exists(dest) && File.Exists(srcPath))
            {
                try { File.Copy(srcPath, dest); }
                catch { }
            }
        }

        [DllImport("leveldb", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr leveldb_open(IntPtr options, byte[] path, out IntPtr error);

        [DllImport("leveldb", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr leveldb_options_create();

        [DllImport("leveldb", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr leveldb_options_destroy(IntPtr handle);

        [DllImport("leveldb", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr leveldb_writeoptions_create();

        [DllImport("leveldb", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr leveldb_writeoptions_destroy(IntPtr handle);

        [DllImport("leveldb", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr leveldb_writebatch_create();

        [DllImport("leveldb", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern void leveldb_writebatch_put(IntPtr batch, byte[] key, int keylen, byte[] val, int vallen);

        [DllImport("leveldb", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern void leveldb_writebatch_delete(IntPtr batch, byte[] key, int keylen);

        [DllImport("leveldb", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern void leveldb_write(IntPtr db, IntPtr options, IntPtr batch, out IntPtr errptr);

        [DllImport("leveldb", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern void leveldb_delete(IntPtr db, IntPtr options, byte[] key, IntPtr keylen,
            out IntPtr errptr);

        [DllImport("leveldb", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr leveldb_get(IntPtr db, IntPtr options, byte[] key, IntPtr keylen, out IntPtr vallen,
            out IntPtr errptr);

        [DllImport("leveldb", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern void leveldb_put(IntPtr db, IntPtr options, byte[] key, IntPtr keylen, byte[] val,
            IntPtr vallen, out IntPtr errptr);

        [DllImport("leveldb", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr leveldb_readoptions_create();

        [DllImport("leveldb", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr leveldb_readoptions_destroy(IntPtr handle);

        [DllImport("leveldb", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern void leveldb_free(IntPtr ptr);

        [DllImport("leveldb", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr leveldb_create_snapshot(IntPtr db);

        [DllImport("leveldb", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr leveldb_create_iterator(IntPtr db, IntPtr options);

        [DllImport("leveldb", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern void leveldb_readoptions_set_snapshot(IntPtr options, IntPtr snapshot);

        [DllImport("leveldb", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern void leveldb_release_snapshot(IntPtr db, IntPtr snapshot);

        [DllImport("leveldb", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern byte leveldb_iter_valid(IntPtr iterator);

        [DllImport("leveldb", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern void leveldb_iter_seek_to_first(IntPtr iterator);

        [DllImport("leveldb", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern void leveldb_iter_seek_to_last(IntPtr iterator);

        [DllImport("leveldb", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern void leveldb_iter_seek(IntPtr iterator, byte[] key, int length);

        [DllImport("leveldb", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern void leveldb_iter_seek(IntPtr iterator, ref int key, int length);

        [DllImport("leveldb", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern void leveldb_iter_next(IntPtr iterator);

        [DllImport("leveldb", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern void leveldb_iter_prev(IntPtr iterator);

        [DllImport("leveldb", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr leveldb_iter_key(IntPtr iterator, out int length);

        [DllImport("leveldb", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr leveldb_iter_value(IntPtr iterator, out int length);

        [DllImport("leveldb", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern void leveldb_iter_destroy(IntPtr iterator);

        [DllImport("leveldb", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern void leveldb_options_set_compression(IntPtr options, int n);

        [DllImport("leveldb", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern void leveldb_close(IntPtr db);

        [DllImport("leveldb", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern void leveldb_writebatch_destroy(IntPtr batch);
        [DllImport("leveldb", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern void leveldb_compact_range(IntPtr db, byte[] start_key, IntPtr start_key_len, byte[] limit_key, IntPtr limit_key_len);
    }
}