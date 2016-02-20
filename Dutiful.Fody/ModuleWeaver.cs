using System;
using System.Linq;
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
    private Regex StopTypeForDeclaring;
    private Regex StopNameForMethod;
    private Regex StopTypeForReturn;

    TypeSystem typeSystem;

    // Init logging delegates to make testing easier
    public ModuleWeaver()
    {
        LogInfo = m => { };
    }

    private static void LoadArguments(ILProcessor processor, ushort count)
    {
        processor.Emit(OpCodes.Ldarg_0);
        if (count < 1) return;

        for (ushort i = 1; i < count; i++)
            processor.Emit(OpCodes.Ldarg, i);

        processor.Emit(OpCodes.Ldarg, count);
    }
    private MethodDefinition MakeDutifulVariant(MethodDefinition method)
    {
        var name = string.Format(methodNameFormat, method.Name);
        LogInfo($"Weaving method \"{name}\"...");

        var attributes = method.Attributes & (MemberAccessMask | HideBySig | Static);
        var dutiful = new MethodDefinition(name, attributes, method.DeclaringType);

        var customAttributes = dutiful.CustomAttributes;
        foreach (var attr in method.CustomAttributes)
            customAttributes.Add(attr);

        var parameters = dutiful.Parameters;
        foreach (var param in method.Parameters)
            parameters.Add(param);

        var processor = dutiful.Body.GetILProcessor();
        LoadArguments(processor, (ushort)parameters.Count);

        processor.Emit(OpCodes.Callvirt, method);
        if (method.ReturnType != typeSystem.Void)
            processor.Emit(OpCodes.Pop);

        processor.Emit(OpCodes.Ldarg_0);
        processor.Emit(OpCodes.Ret);
        return dutiful;
    }

    private void AddDutifulMethods(TypeDefinition type)
    {
        LogInfo($"Processing type \"{type.FullName}\"...");

        foreach (var method in type.Methods.Where(m => (m.IsPublic || m.IsFamily)
            && !(m.IsStatic || m.IsConstructor) && m.SemanticsAttributes == MethodSemanticsAttributes.None).ToArray())
        {
            var returnType = method.ReturnType;
            if (returnType == type)
                continue;

            if (StopTypeForDeclaring.IsMatch(method.GetOriginalBaseMethod().DeclaringType.FullName))
                continue;

            if (StopNameForMethod != null)
            {
                if (StopNameForMethod.IsMatch(method.Name))
                    continue;
            }

            if (StopTypeForReturn != null)
            {
                if (StopTypeForReturn.IsMatch(returnType.FullName))
                    continue;
            }

            type.Methods.Add(MakeDutifulVariant(method));
        }
    }

    public void Execute()
    {
        typeSystem = ModuleDefinition.TypeSystem;

        SetupFromConfig();

        foreach (var type in ModuleDefinition.Types.Where(t => t.IsPublic && !(t.IsEnum || t.IsInterface)))
            AddDutifulMethods(type);
    }

    private void SetupFromConfig()
    {
        methodNameFormat = (Config?.Attribute("NameFormat")?.Value) ?? "Dutiful{0}";

        {
            var stopDeclaringPattern = Config?.Attribute("StopNameForDeclaringType")?.Value;
            if (stopDeclaringPattern == null)
                stopDeclaringPattern = '@' + typeSystem.Object.FullName;
            else
                stopDeclaringPattern = stopDeclaringPattern.Trim();

            if (stopDeclaringPattern != "" && stopDeclaringPattern[0] == '@')
                stopDeclaringPattern = Regex.Escape(stopDeclaringPattern.Substring(1).TrimStart());
            else
                new Regex(stopDeclaringPattern);

            StopTypeForDeclaring = new Regex("^(?:" + stopDeclaringPattern + ")$");
        }

        {
            var stopMethodPattern = Config?.Attribute("StopNameForMethod")?.Value;
            if (string.IsNullOrWhiteSpace(stopMethodPattern))
                StopNameForMethod = null;
            else
            {
                stopMethodPattern = stopMethodPattern.Trim();
                new Regex(stopMethodPattern);
                StopNameForMethod = new Regex("^(?:" + stopMethodPattern + ")$");
            }
        }

        {
            var stopReturnPattern = Config?.Attribute("StopNameForReturnType")?.Value;
            if (string.IsNullOrWhiteSpace(stopReturnPattern))
                StopTypeForReturn = null;
            else
            {
                stopReturnPattern = stopReturnPattern.Trim();

                if (stopReturnPattern != "" && stopReturnPattern[0] == '@')
                    stopReturnPattern = Regex.Escape(stopReturnPattern.Substring(1).TrimStart());
                else
                    new Regex(stopReturnPattern);
            }
        }
    }
}