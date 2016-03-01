using System;
using System.Linq;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using Mono.Cecil.Cil;

static partial class CecilEx
{
    private const FieldAttributes fieldAttributes = FieldAttributes.Public;

    //public static MethodDefinition MakeEnclosureMethods(this TypeDefinition type,
    //        string methodName, MethodReference constructor,
    //        IList<FieldReference> fields, FieldReference thisField = null)
    //{
    //    const MethodAttributes enclosureAttributes = MethodAttributes.Public | MethodAttributes.Static;

    //    var hasThis = thisField != null;

    //    var paramTypes = fields.Select(f => f.FieldType);
    //    if (hasThis)
    //        paramTypes = new[] { thisField.FieldType }.Concat(paramTypes);

    //    var method = new MethodDefinition(methodName, enclosureAttributes, type);
    //    type.Methods.Add(method);
    //    var parameters = method.Parameters;

    //    var encIL = method.Body.GetILProcessor();
    //    method.Body.Variables.Add(new VariableDefinition(type));

    //    encIL.Emit(OpCodes.Newobj, constructor);
    //    encIL.Emit(OpCodes.Stloc_0);

    //    ushort argIndex = 0;

    //    if (hasThis)
    //    {
    //        parameters.Add(new ParameterDefinition(thisField.FieldType) { Name = "this" });

    //        encIL.Emit(OpCodes.Ldloc_0);
    //        encIL.Emit(OpCodes.Ldarg_0);
    //        encIL.Emit(OpCodes.Stfld, thisField);

    //        argIndex++;
    //    }

    //    foreach (var field in fields)
    //    {
    //        parameters.Add(new ParameterDefinition(thisField.FieldType) { Name = "@" + field.Name });

    //        encIL.Emit(OpCodes.Ldloc_0);
    //        encIL.Emit(OpCodes.Ldarg, argIndex);
    //        encIL.Emit(OpCodes.Stfld, field);

    //        argIndex++;
    //    }

    //    encIL.Emit(OpCodes.Ldloc_0);
    //    encIL.Emit(OpCodes.Ret);

    //    return method;
    //}

    public static TypeDefinition MakeClosureType(this ModuleDefinition module,
        string @namespace, string typeName, TypeAttributes attributes, IEnumerable<ParameterReference> parameters,
        out MethodDefinition constructor, TypeReference thisType)
    {
        var voidType = module.TypeSystem.Void;
        if (thisType == null || thisType.IsSameAs(voidType))
            throw new ArgumentOutOfRangeException();

        var type = new TypeDefinition(@namespace, typeName, attributes);
        module.Types.Add(type);

        var fields = type.Fields;
        var paramsColl = parameters as ICollection<ParameterReference>;
        if (paramsColl != null)
            fields.Capacity = paramsColl.Count + 1;

        var ctor = new MethodDefinition(".ctor", MethodAttributes.RTSpecialName | MethodAttributes.SpecialName, module.TypeSystem.Void);
        type.Methods.Add(ctor);

        var @params = ctor.Parameters;
        var il = ctor.Body.GetILProcessor();

        var thisField = new FieldDefinition("this", fieldAttributes, thisType);
        fields.Add(thisField);
        @params.Add(new ParameterDefinition(thisType));
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Stfld, thisField);

        ushort i = 1;
        foreach(var param in parameters)
        {
            var field = new FieldDefinition("@" + param.Name, fieldAttributes, param.ParameterType);
            fields.Add(field);
            @params.Add(new ParameterDefinition(param.ParameterType));
            il.Emit(OpCodes.Ldarg, i);
            il.Emit(OpCodes.Stfld, field);
            i++;
        }

        il.Emit(OpCodes.Ret);

        constructor = ctor;

        return type;
    }
}
