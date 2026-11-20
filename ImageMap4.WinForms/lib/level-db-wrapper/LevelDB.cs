using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace LevelDBWrapper;

public class LevelDB : LevelDBHandle
{
    public LevelDB(string path, Options options)
    {
        IntPtr error;
        Handle = NativeMethods.leveldb_open(options.Handle, Encoding.UTF8.GetBytes(path), out error);
        Throw(error);
    }

    private static void Throw(IntPtr error)
    {
        if (error != IntPtr.Zero)
        {
            try
            {
                var msg = Marshal.PtrToStringAnsi(error);
                throw new Exception(msg);
            }
            finally
            {
                NativeMethods.leveldb_free(error);
            }
        }
    }

    public void Write(WriteBatch batch)
    {
        IntPtr error;
        var options = new WriteOptions();
        NativeMethods.leveldb_write(this.Handle, options.Handle, batch.Handle, out error);
        Throw(error);
    }

    public void Delete(string key)
    {
        Delete(NativeMethods.Encoding.GetBytes(key));
    }

    public void Delete(byte[] key)
    {
        IntPtr error;
        var options = new WriteOptions();
        NativeMethods.leveldb_delete(this.Handle, options.Handle, key, (IntPtr)key.LongLength, out error);
        Throw(error);
    }

    public byte[] Get(string key)
    {
        return Get(NativeMethods.Encoding.GetBytes(key));
    }

    public byte[] Get(byte[] key)
    {
        IntPtr error;
        IntPtr length;
        var options = new ReadOptions();
        var v = NativeMethods.leveldb_get(this.Handle, options.Handle, key, (IntPtr)key.LongLength, out length, out error);
        Throw(error);

        if (v != IntPtr.Zero)
        {
            try
            {
                var bytes = new byte[(long)length];
                Marshal.Copy(v, bytes, 0, checked((int)length));
                return bytes;
            }
            finally
            {
                NativeMethods.leveldb_free(v);
            }
        }
        return null;
    }

    public void Put(string key, string value)
    {
        this.Put(NativeMethods.Encoding.GetBytes(key), NativeMethods.Encoding.GetBytes(value));
    }


    public void Put(string key, byte[] value)
    {
        this.Put(NativeMethods.Encoding.GetBytes(key), value);
    }

    public void Put(byte[] key, byte[] value)
    {
        IntPtr error;
        var options = new WriteOptions();
        NativeMethods.leveldb_put(this.Handle, options.Handle, key, (IntPtr)key.LongLength, value, (IntPtr)value.LongLength, out error);
        Throw(error);
    }

    public void Close()
    {
        Dispose();
    }

    protected override void FreeUnmanagedObjects()
    {
        if (this.Handle != IntPtr.Zero)
            NativeMethods.leveldb_close(this.Handle);
    }

    public Snapshot CreateSnapshot()
    {
        return new Snapshot(NativeMethods.leveldb_create_snapshot(this.Handle), this);
    }

    public Iterator CreateIterator()
    {
        return CreateIterator(new ReadOptions());
    }

    public Iterator CreateIterator(ReadOptions options)
    {
        return new Iterator(NativeMethods.leveldb_create_iterator(this.Handle, options.Handle));
    }

    public IEnumerable<string> StringKeys()
    {
        using (var sn = this.CreateSnapshot())
        using (var iterator = this.CreateIterator(new ReadOptions { Snapshot = sn }))
        {
            iterator.SeekToFirst();
            while (iterator.IsValid())
            {
                yield return iterator.StringKey();
                iterator.Next();
            }
        }
    }

    public IEnumerable<byte[]> ByteKeys()
    {
        using (var sn = this.CreateSnapshot())
        using (var iterator = this.CreateIterator(new ReadOptions { Snapshot = sn }))
        {
            iterator.SeekToFirst();
            while (iterator.IsValid())
            {
                yield return iterator.Key();
                iterator.Next();
            }
        }
    }

    public IEnumerable<KeyValuePair<string, byte[]>> StringBytePairs()
    {
        using (var sn = this.CreateSnapshot())
        using (var iterator = this.CreateIterator(new ReadOptions { Snapshot = sn }))
        {
            iterator.SeekToFirst();
            while (iterator.IsValid())
            {
                yield return new KeyValuePair<string, byte[]>(iterator.StringKey(), iterator.Value());
                iterator.Next();
            }
        }
    }

    public IEnumerable<KeyValuePair<byte[], byte[]>> ByteBytePairs()
    {
        using (var sn = this.CreateSnapshot())
        using (var iterator = this.CreateIterator(new ReadOptions { Snapshot = sn }))
        {
            iterator.SeekToFirst();
            while (iterator.IsValid())
            {
                yield return new KeyValuePair<byte[], byte[]>(iterator.Key(), iterator.Value());
                iterator.Next();
            }
        }
    }

    public void Compact()
    {
        NativeMethods.leveldb_compact_range(this.Handle, null, IntPtr.Zero, null, IntPtr.Zero);
    }
}