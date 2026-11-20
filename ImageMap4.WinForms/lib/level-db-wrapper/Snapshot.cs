using System;

namespace LevelDBWrapper;

public class Snapshot : LevelDBHandle
{
    public WeakReference Parent;

    internal Snapshot(IntPtr Handle, LevelDB parent)
    {
        this.Handle = Handle;
        this.Parent = new WeakReference(parent);
    }

    internal Snapshot(IntPtr Handle)
    {
        this.Handle = Handle;
        Parent = new WeakReference(null);
    }

    protected override void FreeUnmanagedObjects()
    {
        if (Parent.IsAlive && Parent.Target is LevelDB db)
            NativeMethods.leveldb_release_snapshot(db.Handle, this.Handle);
    }
}