namespace LevelDBWrapper;

public class WriteBatch : LevelDBHandle
{
    public WriteBatch()
    {
        Handle = NativeMethods.leveldb_writebatch_create();
    }

    public WriteBatch Put(string key, string value)
    {
        return Put(NativeMethods.Encoding.GetBytes(key), NativeMethods.Encoding.GetBytes(value));
    }

    public WriteBatch Put(string key, byte[] value)
    {
        return Put(NativeMethods.Encoding.GetBytes(key), value);
    }

    public WriteBatch Put(byte[] key, byte[] value)
    {
        NativeMethods.leveldb_writebatch_put(Handle, key, key.Length, value, value.Length);
        return this;
    }
    
    public WriteBatch Delete(string key)
    {
        return Delete(NativeMethods.Encoding.GetBytes(key));
    }

    public WriteBatch Delete(byte[] key)
    {
        NativeMethods.leveldb_writebatch_delete(Handle, key, key.Length);
        return this;
    }

    protected override void FreeUnmanagedObjects()
    {
        NativeMethods.leveldb_writebatch_destroy(this.Handle);
    }
}