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
    Type targetType;

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
        targetType = assembly.GetType("AssemblyToProcess.TargetClass");
    }

    [Test]
    public void ValidateSimpleMethods()
    {
        dynamic instance = Activator.CreateInstance(targetType);
        var sw = instance.SpawnStopwatch();

        Assert.IsInstanceOf<Stopwatch>(sw);
        Assert.AreEqual(instance, instance.SpawnStopwatchDutiful());
    }

    [Test]
    public void ValidateOutputMethods()
    {
        dynamic instance = Activator.CreateInstance(targetType);
        var sw = instance.SpawnStopwatch();

        {
            string output;
            var result = instance.TryMakeString("<{0}>", 233, out output);
            Assert.True(result);
            Assert.AreEqual("<233>", output);
        }
        {
            string output;
            var result = instance.TryMakeStringDutiful("<{0}>", 233, out output);
            Assert.AreEqual(instance, result);
            Assert.AreEqual("<233>", output);
        }
    }

    [Test]
    public void ValidateMoreArguments()
    {
        dynamic instance = Activator.CreateInstance(targetType);
        var sw = instance.SpawnStopwatch();

        const string format = "{2}:\t{0}{3}{1}@{4}";
        var expected = $"233:\t{{{new object()}}}@{IntPtr.Zero}";

        {
            string output;
            var result = instance.TryMakeString(format, 233, new object(), IntPtr.Zero, out output);
            Assert.True(result);
            Assert.AreEqual(expected, output);
        }
        {
            string output;
            var result = instance.TryMakeStringDutiful(format, 233, new object(), IntPtr.Zero, out output);
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