using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.CSharp.RuntimeBinder;
using Mono.Cecil;
using NUnit.Framework;

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
        
        var config = XElement.Parse(@"<Dutiful NameFormat=""{0}Careless""/>");
        config.SetAttributeValue("StopWordForReturnType", @".+\.UIntPtr");
        config.Add(new XElement("StopWordForReturnType") { Value = @"
            @System.IntPtr
            .+\.StringBuilder
        " });
        config.SetAttributeValue("StopWordForMethodName", ".+NoDutiful");
        config.Add(new XElement("StopWordForMethodName") { Value = @"
            @NoDutiful
            .+_+.+
        " });
        var moduleDefinition = ModuleDefinition.ReadModule(newAssemblyPath);
        var weavingTask = new ModuleWeaver
        {
            Config = config,
            ModuleDefinition = moduleDefinition,
        };

        weavingTask.Execute();
        moduleDefinition.Write(newAssemblyPath);

        assembly = Assembly.LoadFile(newAssemblyPath);
        targetClass = assembly.GetType("TargetClass");
        targetStruct = assembly.GetType("TargetStruct");
    }

    [Test]
    public void ValidateJustMe()
    {
        dynamic instance = Activator.CreateInstance(targetClass);

        Assert.AreEqual(instance, instance.JustMe());

        Assert.Throws<RuntimeBinderException>(() => instance.JustMeCareless());
    }

    [Test]
    public void ValidateStructStopWords()
    {
        dynamic instance = Activator.CreateInstance(targetStruct);

        instance.No_Thanks();
        instance.NoDutiful();
        instance.NoopNoDutiful();
        Assert.AreEqual(instance, instance.NOOPCareless());

        Assert.Throws<RuntimeBinderException>(() => instance.EqualsCareless(null));
        Assert.Throws<RuntimeBinderException>(() => instance.ToStringCareless());
        Assert.Throws<RuntimeBinderException>(() => instance.No_ThanksCareless());
        Assert.Throws<RuntimeBinderException>(() => instance.NoDutifulCareless());
        Assert.Throws<RuntimeBinderException>(() => instance.NoopNoDutifulCareless());
    }

    [Test]
    public void ValidateClassStopWords()
    {
        dynamic instance = Activator.CreateInstance(targetClass);
        Assert.IsInstanceOf<Stopwatch>(instance.SpawnStopwatch());

        Assert.IsInstanceOf<IntPtr>(instance.GetIntPtr());
        Assert.IsInstanceOf<UIntPtr>(instance.GetUIntPtr());
        Assert.Null(instance.GetStringBuilder());

        Assert.Throws<RuntimeBinderException>(() => instance.GetIntPtrCareless());
        Assert.Throws<RuntimeBinderException>(() => instance.GetUIntPtrCareless());
        Assert.Throws<RuntimeBinderException>(() => instance.GetStringBuilderCareless());

        Assert.AreEqual(instance, instance.SpawnStopwatchCareless());
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
            var result = instance.TryMakeStringCareless(format, 233, IntPtr.Zero, out output);
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
            var result = instance.TryMakeStringCareless(format, 233, IntPtr.Zero, out output);
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