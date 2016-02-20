using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using Mono.Cecil.Cil;

using static Mono.Cecil.MethodAttributes;

public class ModuleWeaver
{
    // Will log an informational message to MSBuild
    public Action<string> LogInfo { get; set; }

    // An instance of Mono.Cecil.ModuleDefinition for processing
    public ModuleDefinition ModuleDefinition { get; set; }

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
        var name = method.Name + "Dutiful";
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
            & !m.IsStatic | m.SemanticsAttributes == MethodSemanticsAttributes.None).ToArray())
        {
            var returnType = method.ReturnType;
            if (returnType == type)
                continue;

            type.Methods.Add(MakeDutifulVariant(method));
        }
    }

    public void Execute()
    {
        typeSystem = ModuleDefinition.TypeSystem;

        foreach (var type in ModuleDefinition.Types.Where(t => t.IsPublic))
            AddDutifulMethods(type);
    }

}