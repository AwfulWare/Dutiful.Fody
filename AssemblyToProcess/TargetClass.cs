using System;
using System.Diagnostics;

namespace AssemblyToProcess
{
    public class TargetClass
    {
        [Obsolete]
        public Stopwatch SpawnStopwatch()
            => Stopwatch.StartNew();

        public bool TryMakeString(string format, decimal arg2, out string result)
        {
            result = string.Format(format, arg2);
            Debug.WriteLine(result);
            return true;
        }
        public bool TryMakeString(string format, decimal arg2, object arg3, IntPtr arg4, out string result)
        {
            result = string.Format(format, "{", "}", arg2, arg3, arg4);
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
}
