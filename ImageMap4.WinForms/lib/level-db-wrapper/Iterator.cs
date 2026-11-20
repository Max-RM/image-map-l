using System;
using System.Runtime.InteropServices;

namespace LevelDBWrapper;

public class Iterator : LevelDBHandle
{
    internal Iterator(IntPtr Handle)
    {
        this.Handle = Handle;
    }

    public bool IsValid()
    {
        return NativeMethods.leveldb_iter_valid(this.Handle) != 0;
    }

    public void SeekToFirst()
    {
        NativeMethods.leveldb_iter_seek_to_first(this.Handle);
    }

    public void SeekToLast()
    {
        NativeMethods.leveldb_iter_seek_to_last(this.Handle);
    }

    public void Seek(byte[] key)
    {
        NativeMethods.leveldb_iter_seek(this.Handle, key, key.Length);
    }

    public void Seek(string key)
    {
        Seek(NativeMethods.Encoding.GetBytes(key));
    }

    public void Next()
    {
        NativeMethods.leveldb_iter_next(this.Handle);
    }

    public void Prev()
    {
        NativeMethods.leveldb_iter_prev(this.Handle);
    }


    public string StringKey()
    {
        return NativeMethods.Encoding.GetString(this.Key());
    }

    public byte[] Key()
    {
        int length;
        var key = NativeMethods.leveldb_iter_key(this.Handle, out length);
        var bytes = new byte[length];
        Marshal.Copy(key, bytes, 0, length);
        return bytes;
    }

    public string StringValue()
    {
        return NativeMethods.Encoding.GetString(this.Value());
    }

    public byte[] Value()
    {
        int length;
        var value = NativeMethods.leveldb_iter_value(this.Handle, out length);

        var bytes = new byte[length];
        Marshal.Copy(value, bytes, 0, length);
        return bytes;
    }

    protected override void FreeUnmanagedObjects()
    {
        NativeMethods.leveldb_iter_destroy(this.Handle);
    }
}