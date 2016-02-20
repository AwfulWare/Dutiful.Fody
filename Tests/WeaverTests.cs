using System;
using System.IO;
using System.Reflection;
using Mono.Cecil;
using NUnit.Framework;
using System.Diagnostics;

[TestFixture]
public class WeaverTests
{
    Assembly assembly;
    string newAssemblyPath;
    string assemblyPath;
    Type targetClass;
    Type targetStruct;

    [TestFixtureSetUp]
    public void Setup()
    {
        var projectPath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, @"..\..\..\AssemblyToProcess\AssemblyToProcess.csproj"));
        assemblyPath = Path.Combine(Path.GetDirectoryName(projectPath), @"bin\Debug\AssemblyToProcess.dll");
#if (!DEBUG)
        assemblyPath = assemblyPath.Replace("Debug", "Release");
#endif

        newAssemblyPath = assemblyPath.Replace(".dll", "2.dll");
        File.Copy(assemblyPath, newAssemblyPath, true);

        var moduleDefinition = ModuleDefinition.ReadModule(newAssemblyPath);
        var weavingTask = new ModuleWeaver
        {
            ModuleDefinition = moduleDefinition
        };

        weavingTask.Execute();
        moduleDefinition.Write(newAssemblyPath);

        assembly = Assembly.LoadFile(newAssemblyPath);
        targetClass = assembly.GetType("TargetClass");
        targetStruct = assembly.GetType("TargetStruct");
    }

    [Test]
    public void ValidateSimpleMethods()
    {
        dynamic instance = Activator.CreateInstance(targetClass);
        var sw = instance.SpawnStopwatch();

        Assert.IsInstanceOf<Stopwatch>(sw);
        Assert.AreEqual(instance, instance.SpawnStopwatchDutiful());

        Assert.AreEqual(instance, instance.NOOPDutiful());

        Assert.IsInstanceOf(targetStruct, ((dynamic)Activator.CreateInstance(targetStruct)).NOOPDutiful());
    }

    [Test]
    public void ValidateClassArguments()
    {
        dynamic instance = Activator.CreateInstance(targetClass);

        const string format = "{2}:\t{0}{3}{1}@{4}";
        var expected = $"233:\t{{{targetClass.FullName}}}@{IntPtr.Zero}";

        {
            string output;
            var result = instance.TryMakeString(format, 233, IntPtr.Zero, out output);
            Assert.True(result);
            Assert.AreEqual(expected, output);
        }
        {
            string output;
            var result = instance.TryMakeStringDutiful(format, 233, IntPtr.Zero, out output);
            Assert.AreEqual(instance, result);
            Assert.AreEqual(expected, output);
        }
    }

    [Test]
    public void ValidateStructArguments()
    {
        dynamic instance = Activator.CreateInstance(targetStruct);

        const string format = "{2}:\t{0}{3}{1}@{4}";
        var expected = $"233:\t{{{targetStruct.FullName}}}@{IntPtr.Zero}";

        {
            string output;
            var result = instance.TryMakeString(format, 233, IntPtr.Zero, out output);
            Assert.True(result);
            Assert.AreEqual(expected, output);
        }
        {
            string output;
            var result = instance.TryMakeStringDutiful(format, 233, IntPtr.Zero, out output);
            Assert.AreEqual(instance, result);
            Assert.AreEqual(expected, output);
        }
    }

#if (DEBUG)
    [Test]
    public void PeVerify()
    {
        Verifier.Verify(assemblyPath, newAssemblyPath);
    }
#endif
}