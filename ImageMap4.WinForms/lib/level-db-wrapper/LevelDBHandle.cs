using System;

namespace LevelDBWrapper;

public abstract class LevelDBHandle : IDisposable
{
    public IntPtr Handle { protected set; get; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void FreeManagedObjects() { }

    protected virtual void FreeUnmanagedObjects() { }

    public bool Disposed { get; private set; } = false;
    void Dispose(bool disposing)
    {
        if (!Disposed)
        {
            if (disposing)
                FreeManagedObjects();
            if (this.Handle != IntPtr.Zero)
            {
                FreeUnmanagedObjects();
                this.Handle = IntPtr.Zero;
            }
            Disposed = true;
        }
    }

    ~LevelDBHandle()
    {
        Dispose(false);
    }
}