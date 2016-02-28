using Nito.AsyncEx;
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

    public static Task GetTaskStatic() => TaskEx.Delay(1234);
    public static void GetTaskStaticSyncRef()
        => Nito.AsyncEx.AsyncContext.Run(GetTaskStatic);
    public Task GetTask() => GetTaskStatic();
    public void GetTaskSyncRef()
        => Nito.AsyncEx.AsyncContext.Run(GetTask);
    public async Task<T> GetTask<T>()
        where T : new()
    {
        await GetTask();
        return new T();
    }
    public async Task<T> GetTask<T>(T input)
    {
        await GetTask();
        return input;
    }
    public async Task<int> GetTaskInt()
    {
        await GetTask();
        return new Random().Next();
    }
    public int GetTaskIntSyncRef()
        => AsyncContext.Run(GetTaskInt);
    public Task<object> GetTask(object input)
        => GetTask<object>(input);

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

