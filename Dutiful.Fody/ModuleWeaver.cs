﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
    private string methodNameFormat;
    private TargetTypeLevel TargetLevel;
    private Regex StopWordForDeclaringType;
    private Regex StopWordForMethodName;
    private Regex StopWordForReturnType;
    private Regex StopWordForSignature;

    TypeSystem typeSystem;

    // Init logging delegates to make testing easier
    public ModuleWeaver()
    {
        LogInfo = m => { };
    }

    private static void CloneGenericParametersTo(IGenericParameterProvider from, IGenericParameterProvider to)
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

            CloneGenericParametersTo(param, clone);

            coll.Add(clone);
        }
    }
    private static void LoadArguments(ILProcessor processor, ushort count)
    {
        processor.Emit(OpCodes.Ldarg_0);
        if (count < 1) return;

        for (ushort i = 1; i < count; i++)
            processor.Emit(OpCodes.Ldarg, i);

        processor.Emit(OpCodes.Ldarg, count);
    }
    private string MakeDutifulName(string name)
        => string.Format(methodNameFormat, name);
    private MethodDefinition MakeDutifulVariant(MethodDefinition method)
    {
        var name = string.Format(methodNameFormat, method.Name);
        LogInfo($"Weaving method \"{name}\"...");

        var attributes = method.Attributes & (MemberAccessMask | HideBySig | Static);
        var dutiful = new MethodDefinition(name, attributes, method.DeclaringType);

        var customAttributes = dutiful.CustomAttributes;
        foreach (var attr in method.CustomAttributes)
            customAttributes.Add(attr);

        CloneGenericParametersTo(method, dutiful);

        var parameters = dutiful.Parameters;
        foreach (var param in method.Parameters)
            parameters.Add(param);

        var processor = dutiful.Body.GetILProcessor();
        LoadArguments(processor, (ushort)parameters.Count);

        var generic = new GenericInstanceMethod(method);
        foreach (var item in dutiful.GenericParameters)
            generic.GenericArguments.Add(item);

        method.Resolve().GetBaseMethod();

        processor.Emit(OpCodes.Callvirt, generic);
        if (method.ReturnType != typeSystem.Void)
            processor.Emit(OpCodes.Pop);

        processor.Emit(OpCodes.Ldarg_0);
        processor.Emit(OpCodes.Ret);
        return dutiful;
    }

    private void AddDutifulMethods(TypeDefinition type)
    {
        LogInfo($"Processing type \"{type.FullName}\"...");
        var groups = type.Methods.Where(m => (m.IsPublic || m.IsFamily)
            && !(m.IsStatic || m.IsConstructor) && m.SemanticsAttributes == MethodSemanticsAttributes.None)
            .ToLookup(m => m.Name);

        foreach (var method in groups.SelectMany(g => g))
        {
            var dutifulName = MakeDutifulName(method.Name);
            if (groups.Contains(dutifulName))
            {
                if (groups[dutifulName].Any(m =>
                {
                    if (m.GenericParameters.Count != method.GenericParameters.Count)
                        return false;
                    return m.Parameters.AreMatch(method.Parameters);
                })) continue;
            }

            var returnType = method.ReturnType;
            if (type.IsAssignableFrom(returnType.Resolve()))
                continue;

            if (StopWordForDeclaringType.IsMatch(method.GetOriginalBaseMethod().DeclaringType.FullName))
                continue;

            if (StopWordForReturnType != null)
            {
                if (StopWordForReturnType.IsMatch(returnType.FullName))
                    continue;
            }

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

            type.Methods.Add(MakeDutifulVariant(method));
        }
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
        })) AddDutifulMethods(type);
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
    private void SetupFromConfig()
    {
        const string placeholder = "{0}";
        const string nameAttr = "NameFormat";
        methodNameFormat = (Config?.Attribute(nameAttr)?.Value?.Replace("*", placeholder, StringComparison.Ordinal)) ?? "Dutiful";
        var index = methodNameFormat.IndexOf(placeholder, StringComparison.Ordinal);
        if (index < 0)
            methodNameFormat = placeholder + methodNameFormat;
        else if (methodNameFormat.IndexOf(placeholder, index + placeholder.Length, StringComparison.Ordinal) > index)
            throw new FormatException(nameAttr);

        Enum.TryParse(Config?.Attribute(nameof(TargetTypeLevel))?.Value, out TargetLevel);

        SetupStopWordForDeclaringType();
        StopWordForMethodName = MakeStopWordPatternFromConfig(nameof(StopWordForMethodName));
        StopWordForReturnType = MakeStopWordPatternFromConfig(nameof(StopWordForReturnType));
        SetupStopWordForSignature();
    }
}
