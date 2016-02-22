using System;
using System.Diagnostics;
using System.Text;

public class TargetClass
{
    public class Derived : TargetClass { }

    public event EventHandler ExampleEvent;

    public TargetClass JustMe() => this;
    public Derived GetDerived() => new Derived();

    [Obsolete]
    public virtual Stopwatch SpawnStopwatch()
        => Stopwatch.StartNew();

    public IntPtr GetIntPtr() => IntPtr.Zero;
    public UIntPtr GetUIntPtr() => UIntPtr.Zero;
    public StringBuilder GetStringBuilder() => null;

    public bool TryMakeString(string format, decimal arg2, IntPtr arg3, out string result)
    {
        result = string.Format(format, "{", "}", arg2, this, arg3);
        Debug.WriteLine(result);
        return true;
    }

    protected Stopwatch FamilySpawnStopwatch()
        => SpawnStopwatch();
    protected internal Stopwatch FamilyOrAssemblySpawnStopwatch()
        => SpawnStopwatch();

    private void PrivateNOOP() { }
    internal void AssemblyNOOP() { }
}

