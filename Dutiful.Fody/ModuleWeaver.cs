using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

using static Mono.Cecil.MethodAttributes;

public class ModuleWeaver
{
    // Will log an informational message to MSBuild
    public Action<string> LogInfo { get; set; }

    // An instance of Mono.Cecil.ModuleDefinition for processing
    public XElement Config { get; set; }
    public ModuleDefinition ModuleDefinition { get; set; }
    private bool? useAssemblyFullName = true;
    private string methodNameFormat;
    private string syncNameFormat;
    private TargetTypeLevel TargetLevel;
    private Regex StopWordForDeclaringType;
    private Regex StopWordForMethodName;
    private Regex StopWordForReturnType;
    private Regex StopWordForSignature;

    TypeSystem typeSystem;
    TypeDefinition voidType;

    TypeDefinition asyncContextType;
    TypeDefinition func1;

    MethodReference asyncContextRun0;
    MethodReference funcTask0Ctor;
    //TypeReference funcTask0Type;
    TypeDefinition task0Type;

    //MethodReference asyncContextRun1;
    //MethodReference funcTask1Ctor;
    //TypeReference funcTask1Type;
    TypeDefinition task1Type;

    // Init logging delegates to make testing easier
    public ModuleWeaver()
    {
        LogInfo = m => { };


    }

    private static void CopyGenericParametersTo(IGenericParameterProvider from, IGenericParameterProvider to)
    {
        var coll = to.GenericParameters;
        foreach (var param in from.GenericParameters)
        {
            var clone = new GenericParameter(to);

            clone.Name = param.Name;
            clone.Attributes = param.Attributes;

            foreach (var attr in param.CustomAttributes)
                clone.CustomAttributes.Add(attr);

            foreach (var constraint in param.Constraints)
                clone.Constraints.Add(constraint);

            CopyGenericParametersTo(param, clone);

            coll.Add(clone);
        }
    }
    private static void LoadArguments(ILProcessor processor, ushort count, bool? loadNull = null)
    {
        if (!loadNull.HasValue)
        {
            processor.Emit(OpCodes.Ldarg_0);
            if (count < 1) return;

            for (ushort i = 1; i < count; i++)
                processor.Emit(OpCodes.Ldarg, i);

            processor.Emit(OpCodes.Ldarg, count);
        }
        else
        {
            if (loadNull.Value)
                processor.Emit(OpCodes.Ldnull);
            if (count < 1) return;

            processor.Emit(OpCodes.Ldarg_0);

            for (ushort i = 1; i < count; i++)
                processor.Emit(OpCodes.Ldarg, i);
        }
    }
    private string MakeDutifulName(string name)
        => string.Format(methodNameFormat, name);
    private string MakeSyncName(string name)
        => string.Format(syncNameFormat, name);

    private MethodDefinition CloneMethodSignature(MethodDefinition source, string name, TypeReference type)
    {
        var result = new MethodDefinition(name, source.Attributes, type);

        var customAttributes = result.CustomAttributes;
        foreach (var attr in source.CustomAttributes)
            customAttributes.Add(attr);

        CopyGenericParametersTo(source, result);

        var parameters = result.Parameters;
        foreach (var param in source.Parameters)
            parameters.Add(param);

        return result;
    }

    private MethodDefinition DefineDutifulVariant(MethodDefinition method)
    {
        var name = string.Format(methodNameFormat, method.Name);
        LogInfo($"Weaving method \"{name}\"...");

        var dutiful = CloneMethodSignature(method, name, method.DeclaringType);
        dutiful.Attributes &= (MemberAccessMask | HideBySig | Static);

        var processor = dutiful.Body.GetILProcessor();
        LoadArguments(processor, (ushort)method.Parameters.Count);

        if (!method.HasGenericParameters)
            processor.Emit(OpCodes.Callvirt, method);
        else
        {
            var generic = new GenericInstanceMethod(method);
            foreach (var item in dutiful.GenericParameters)
                generic.GenericArguments.Add(item);
            processor.Emit(OpCodes.Callvirt, generic);
        }

        if (!method.ReturnType.IsSameAs(typeSystem.Void))
            processor.Emit(OpCodes.Pop);

        processor.Emit(OpCodes.Ldarg_0);
        processor.Emit(OpCodes.Ret);

        method.DeclaringType.Methods.Add(dutiful);

        return dutiful;
    }

    private void GenerateDutifulMethods(TypeDefinition type)
    {
        LogInfo($"Processing type \"{type.FullName}\"...");
        var groups = type.Methods.Where(m => (m.IsPublic || m.IsFamily)
            && !m.IsConstructor && m.SemanticsAttributes == MethodSemanticsAttributes.None)
            .ToLookup(m => m.Name);

        foreach (var method in groups.SelectMany(g => g))
        {
            if (StopWordForDeclaringType.IsMatch(method.GetOriginalBaseMethod().DeclaringType.FullName))
                continue;

            if (StopWordForMethodName != null)
            {
                if (StopWordForMethodName.IsMatch(method.Name))
                    continue;
            }


            if (StopWordForSignature != null)
            {
                if (StopWordForSignature.IsMatch(method.FullName))
                    continue;
            }

            var returnType = method.ReturnType;

            if (StopWordForReturnType != null)
            {
                if (returnType.IsAssignableTo(task0Type, useAssemblyFullName))
                {
                    // TODO: check return type
                }
                else if (StopWordForReturnType.IsMatch(returnType.FullName))
                {
                    continue;
                }
            }

            Func<string, bool> isSlotOpen = n =>
            {
                if (!groups.Contains(n))
                    return true;

                foreach (var m in groups[n])
                {
                    if (m.GenericParameters.Count != method.GenericParameters.Count)
                        continue;

                    if (m.Parameters.SequenceEqual(method.Parameters, (a, b)
                        => a.ParameterType.IsSameAs(b.ParameterType, useAssemblyFullName)))
                    {
                        return false;
                    }
                }

                return true;
            };

            #region sync
            if (syncNameFormat != null)
            {
                var syncName = MakeSyncName(method.Name);
                if (returnType.IsAssignableTo(task0Type, useAssemblyFullName)
                    && isSlotOpen(syncName))
                {
                    var sync = DefineSyncVariant(method);
                    if (sync != null && !method.IsStatic)
                    {
                        syncName = MakeDutifulName(syncName);
                        if (isSlotOpen(syncName))
                            DefineDutifulVariant(sync);
                    }
                }
            }
            #endregion

            if (method.IsStatic) continue;

            var dutifulName = MakeDutifulName(method.Name);
            if (!isSlotOpen(dutifulName))
                continue;

            if (returnType.IsAssignableTo(type, useAssemblyFullName))
                continue;

            DefineDutifulVariant(method);
        }
    }

    private MethodDefinition DefineSyncVariant(MethodDefinition method)
    {
        var syncName = MakeSyncName(method.Name);

        var funcCtor = funcTask0Ctor;
        var asyncContextRun = asyncContextRun0;

        var sync = CloneMethodSignature(method, syncName, voidType);

        var returnType = method.ReturnType;
        if (returnType.IsGenericInstance)
        {
            var git = returnType as GenericInstanceType;
            if (git.ElementType.IsSameAs(task1Type, useAssemblyFullName)) // TODO: what can task be?
            {
                var ga = git.GenericArguments[0];
                if (!ga.IsGenericParameter)
                {
                    var func = func1.MakeGenericInstanceType(git); // returns Task<T>
                    funcCtor = new MethodReference(".ctor", voidType, func) { HasThis = true };
                    funcCtor.Parameters.Add(new ParameterDefinition(typeSystem.Object));
                    funcCtor.Parameters.Add(new ParameterDefinition(typeSystem.IntPtr));
                    funcCtor = ModuleDefinition.ImportReference(funcCtor);

                    asyncContextRun = new MethodReference("Run", voidType, asyncContextType); // returns T
                    var gp = new GenericParameter(asyncContextRun);
                    asyncContextRun.GenericParameters.Add(gp);
                    asyncContextRun.ReturnType = gp;

                    asyncContextRun.Parameters.Add(
                        new ParameterDefinition(
                            func1.MakeGenericInstanceType(
                                task1Type.MakeGenericInstanceType(gp))));
                    var gim = new GenericInstanceMethod(asyncContextRun);
                    gim.GenericArguments.Add(ga);
                    asyncContextRun = ModuleDefinition.ImportReference(gim);

                    sync.ReturnType = ga;
                }
                else
                {
                    //var gp = ga as GenericParameter;
                    //if (gp.Owner == method)
                    //{
                    //    ga = gp = sync.GenericParameters[gp.Position];
                    //    returnType = git = task1Type.MakeGenericInstanceType(gp);
                    //}
                    //else if (gp.Owner == method.DeclaringType)
                    //{ }
                    //else {
                    //    throw new InvalidOperationException(); // WTH!?
                    //}

                    ////var func = func1.MakeGenericInstanceType(git); // returns Task<T>
                    ////funcCtor = new MethodReference(".ctor", voidType) { HasThis = true };
                    ////funcCtor.Parameters.Add(new ParameterDefinition(typeSystem.Object));
                    ////funcCtor.Parameters.Add(new ParameterDefinition(typeSystem.IntPtr));
                    ////ModuleDefinition.ImportReference(funcCtor, func);

                    //asyncContextRun = new MethodReference("Run", voidType, asyncContextType); // returns T
                    //gp = new GenericParameter(asyncContextRun);
                    //asyncContextRun.GenericParameters.Add(gp);
                    //asyncContextRun.ReturnType = gp;

                    //asyncContextRun.Parameters.Add(
                    //    new ParameterDefinition(
                    //        func1.MakeGenericInstanceType(
                    //            task1Type.MakeGenericInstanceType(gp))));
                    //var gim = new GenericInstanceMethod(asyncContextRun);
                    //gim.GenericArguments.Add(ga);
                    //asyncContextRun = ModuleDefinition.ImportReference(gim, asyncContextRun);

                    //sync.ReturnType = ga;
                }
                //else
                //{

                //    //returnType = null;
                //}
                ////sync.ReturnType = ga;
            }
        }

        sync.Attributes &= (MemberAccessMask | HideBySig | Static);

        var processor = sync.Body.GetILProcessor();
        var loadNull = method.IsStatic ? true : (bool?)null;
        LoadArguments(processor, (ushort)method.Parameters.Count, loadNull);

        if (!method.HasParameters)
        {
            if (!method.HasGenericParameters)
                processor.Emit(OpCodes.Ldftn, method);
            else
            {
                var generic = new GenericInstanceMethod(method);
                foreach (var item in sync.GenericParameters)
                    generic.GenericArguments.Add(item);
                processor.Emit(OpCodes.Ldftn, generic);
            }
            processor.Emit(OpCodes.Newobj, funcCtor);

            processor.Emit(OpCodes.Call, asyncContextRun);
            processor.Emit(OpCodes.Ret);

            method.DeclaringType.Methods.Add(sync);

            return sync;
        }

        return null;
    }

    public void Execute()
    {
        typeSystem = ModuleDefinition.TypeSystem;

        SetupFromConfig();

        foreach (var type in ModuleDefinition.Types.Where(t =>
        {
            if (!t.IsEventuallyAccessible())
                return false;

            if (t.IsClass)
            {
                if (t.IsValueType)
                {
                    if (t.IsEnum) return false;
                    if (TargetLevel < TargetTypeLevel.Struct)
                        return false;
                }

                return true;
            }

            return false;
        })) GenerateDutifulMethods(type);
    }

    private void SetupStopWordForDeclaringType()
    {
        string tmp;
        IEnumerable<string> lines = null;

        tmp = Config?.Element("StopWordForDeclaringType")?.Value;
        if (!tmp.IsNullOrWhiteSpace())
        {
            using (var sr = new StringReader(tmp))
                lines = sr.NonWhiteSpaceLines().ToArray();
        }

        tmp = Config?.Attribute("StopWordForDeclaringType")?.Value;
        if (tmp == null)
            tmp = typeSystem.Object.FullName;

        if (lines == null)
            lines = new[] { tmp };
        else
            lines = new[] { tmp }.Concat(lines);

        if (lines == null) return;

        lines = lines.Select(s =>
        {
            if (s[0] == '@')
                return Regex.Escape(s.Substring(1).Trim());
            new Regex(s);
            return s;
        }).Where(s => s != "").Distinct();

        var sb = new StringBuilder();
        sb.Append("^(?:");
        foreach (var line in lines)
        {
            sb.Append(line);
            sb.Append('|');
        }
        sb.Length--;
        sb.Append(")$");

        StopWordForDeclaringType = new Regex(sb.ToString());
    }
    private Regex MakeStopWordPatternFromConfig(string key)
    {
        if (Config == null) return null;

        string tmp;
        IEnumerable<string> lines = null;

        tmp = Config.Element(key)?.Value;
        if (!tmp.IsNullOrWhiteSpace())
        {
            using (var sr = new StringReader(tmp))
                lines = sr.NonWhiteSpaceLines()
                    .Select(s => s.Trim()).Where(s => s != "").ToArray();
        }

        tmp = Config.Attribute(key)?.Value;
        if (!tmp.IsNullOrWhiteSpace())
        {
            if (lines == null)
                lines = new[] { tmp };
            else
                lines = new[] { tmp }.Concat(lines);
        }

        if (lines == null) return null;

        lines = lines.Select(s =>
        {
            if (s[0] == '@')
                return Regex.Escape(s.Substring(1).TrimStart());
            new Regex(s);
            return s;
        }).Where(s => s != "").Distinct();

        var sb = new StringBuilder();
        sb.Append("^(?:");
        foreach (var line in lines)
        {
            sb.Append(line);
            sb.Append('|');
        }
        sb.Length--;
        sb.Append(")$");

        return new Regex(sb.ToString());
    }
    private void SetupStopWordForSignature()
    {
        if (Config == null) return;

        string tmp;
        IEnumerable<string> lines = null;

        tmp = Config.Element(nameof(StopWordForSignature))?.Value;
        if (!tmp.IsNullOrWhiteSpace())
        {
            using (var sr = new StringReader(tmp))
                lines = sr.NonWhiteSpaceLines()
                    .Select(s => s.Trim()).Where(s => s != "").ToArray();
        }

        tmp = Config.Attribute(nameof(StopWordForSignature))?.Value;
        if (!tmp.IsNullOrWhiteSpace())
        {
            if (lines == null)
                lines = new[] { tmp };
            else
                lines = new[] { tmp }.Concat(lines);
        }

        if (lines == null) return;

        lines = lines.Select(s =>
        {
            if (s[0] == '@')
                return Regex.Escape(s.Substring(1).TrimStart());
            throw new FormatException(nameof(StopWordForSignature));
        }).Where(s => s != "").Distinct();

        var sb = new StringBuilder();
        sb.Append("^(?:");
        foreach (var line in lines)
        {
            sb.Append(line);
            sb.Append('|');
        }
        sb.Length--;
        sb.Append(")$");

        StopWordForSignature = new Regex(sb.ToString());
    }

    const string phName = "{0}";
    private string GetNamePattern(string attributeName, string defaultName = null)
    {
        var pattern = (Config?.Attribute(attributeName)?
            .Value?.Replace("*", phName, StringComparison.Ordinal)) ?? defaultName;

        if (pattern == null)
            return null;

        var index = pattern.IndexOf(phName, StringComparison.Ordinal);
        if (index < 0)
            pattern = phName + pattern;
        else if (pattern.IndexOf(phName, index + phName.Length, StringComparison.Ordinal) > index)
            throw new FormatException(attributeName);

        return pattern;
    }
    private void SetupFromConfig()
    {
        methodNameFormat = GetNamePattern("NameFormat", "Dutiful");
        syncNameFormat = GetNamePattern("SyncNameFormat");

        Enum.TryParse(Config?.Attribute(nameof(TargetTypeLevel))?.Value, out TargetLevel);

        SetupStopWordForDeclaringType();
        StopWordForMethodName = MakeStopWordPatternFromConfig(nameof(StopWordForMethodName));
        StopWordForReturnType = MakeStopWordPatternFromConfig(nameof(StopWordForReturnType));
        SetupStopWordForSignature();

        if (syncNameFormat == null)
        {
            voidType = null;
            asyncContextType = null;
            func1 = null;

            asyncContextRun0 = null;
            //funcTask0Type = null;
            funcTask0Ctor = null;
            task0Type = null;
        }
        else
        {
            voidType = typeSystem.Void.Resolve();

            var asyncExANR = new AssemblyNameReference("Nito.AsyncEx", new Version());
            asyncContextType = new TypeReference("Nito.AsyncEx", "AsyncContext", ModuleDefinition, asyncExANR).Resolve();
            func1 = ModuleDefinition.ImportReference(typeof(Func<>)).Resolve();

            task0Type = ModuleDefinition.ImportReference(typeof(Task)).Resolve();
            var funcTask0Type = func1.MakeGenericInstanceType(task0Type);
            //ModuleDefinition.ImportReference(funcTask0Type);

            funcTask0Ctor = new MethodReference(".ctor", typeSystem.Void, funcTask0Type) { HasThis = true };
            funcTask0Ctor.Parameters.Add(new ParameterDefinition(typeSystem.Object));
            funcTask0Ctor.Parameters.Add(new ParameterDefinition(typeSystem.IntPtr));
            funcTask0Ctor = ModuleDefinition.ImportReference(funcTask0Ctor);

            asyncContextRun0 = new MethodReference("Run", typeSystem.Void, asyncContextType);
            asyncContextRun0.Parameters.Add(new ParameterDefinition(funcTask0Type));
            asyncContextRun0 = ModuleDefinition.ImportReference(asyncContextRun0);

            task1Type = ModuleDefinition.ImportReference(typeof(Task<>)).Resolve();
            //asyncContextRun1 = new MethodReference("Run", typeSystem.Void, asyncContextType);
            //var gp1 = new GenericParameter(asyncContextRun1);
            //asyncContextRun1.GenericParameters.Add(gp1);

            //var funcTask1Type = func1.MakeGenericInstanceType(task1Type);
            ////ModuleDefinition.ImportReference(funcTask1Type);

            //funcTask1Ctor = new MethodReference(".ctor", typeSystem.Void, funcTask1Type) { HasThis = true };
            //funcTask1Ctor.Parameters.Add(new ParameterDefinition(typeSystem.Object));
            //funcTask1Ctor.Parameters.Add(new ParameterDefinition(typeSystem.IntPtr));
            ////funcTask1Ctor = ModuleDefinition.ImportReference(funcTask1Ctor);

            //asyncContextRun1.ReturnType = gp1;
            //asyncContextRun1.Parameters.Add(new ParameterDefinition(funcTask1Type));
            //asyncContextRun1 = ModuleDefinition.ImportReference(asyncContextRun1);
        }
    }
}
