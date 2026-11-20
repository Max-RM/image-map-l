namespace LevelDBWrapper;

public class Options : LevelDBHandle
{
    public Options()
    {
        Handle = NativeMethods.leveldb_options_create();
    }
    
    public CompressionLevel CompressionLevel {
        set => NativeMethods.leveldb_options_set_compression(this.Handle, (int)value);
    }

    protected override void FreeUnmanagedObjects()
    {
        NativeMethods.leveldb_options_destroy(this.Handle);
    }
}

public enum CompressionLevel
{
    NoCompression = 0,
    SnappyCompression = 1,
    ZlibCompression = 2,
    ZlibRawCompression = 4
}

public class WriteOptions : LevelDBHandle
{
    public WriteOptions()
    {
        Handle = NativeMethods.leveldb_writeoptions_create();
    }
    
    protected override void FreeUnmanagedObjects()
    {
        NativeMethods.leveldb_writeoptions_destroy(this.Handle);
    }
}

public class ReadOptions : LevelDBHandle
{
    public ReadOptions()
    {
        Handle = NativeMethods.leveldb_readoptions_create();
    }

    public Snapshot Snapshot
    {
        set { NativeMethods.leveldb_readoptions_set_snapshot(this.Handle, value.Handle); }
    }
    
    protected override void FreeUnmanagedObjects()
    {
        NativeMethods.leveldb_readoptions_destroy(this.Handle);
    }
}