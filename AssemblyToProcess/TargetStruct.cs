using System;
using System.Diagnostics;

public struct TargetStruct
{
    public void NOOP() { }

    public bool TryMakeString(string format, decimal arg2, IntPtr arg3, out string result)
    {
        result = string.Format(format, "{", "}", arg2, this, arg3);
        Debug.WriteLine(result);
        return true;
    }

    private void PrivateNOOP() { }
    internal void AssemblyNOOP() { }
}
