using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

public class TargetClass
{
    public class Derived : TargetClass { }

    public event EventHandler ExampleEvent;

    public TargetClass JustMe() => this;
    public Derived GetDerived() => new Derived();

    [Obsolete]
    public virtual Stopwatch SpawnStopwatch()
        => Stopwatch.StartNew();

    public virtual void TypeOf<T>(out Type type)
    {
        type = typeof(T);
    }

    public static Task GetTaskStatic() => Task.Delay(1234);
    public static void GetTaskStaticSyncRef()
        => Nito.AsyncEx.AsyncContext.Run(GetTaskStatic);
    public Task GetTask() => GetTaskStatic();
    public void GetTaskSyncRef()
        => Nito.AsyncEx.AsyncContext.Run(GetTask);
    public async Task GetTask<T>()
    {
        await GetTask();
    }

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

