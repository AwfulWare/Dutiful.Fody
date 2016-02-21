﻿using System;
using System.Diagnostics;

public struct TargetStruct : TargetInterface
{
    public void NOOP() { }

    public void No_Thanks() { }
    public void NoDutiful() { }
    public void NoopNoDutiful() { }

    public object DontWrapThis(object obj) => obj;

    public bool TryMakeString(string format, decimal arg2, IntPtr arg3, out string result)
    {
        result = string.Format(format, "{", "}", arg2, this, arg3);
        Debug.WriteLine(result);
        return true;
    }

    private void PrivateNOOP() { }
    internal void AssemblyNOOP() { }

    public override string ToString()
        => base.ToString();

    public override bool Equals(object obj)
        => base.Equals(obj);
}
