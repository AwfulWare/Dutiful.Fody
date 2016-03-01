using System;
using System.Linq;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using Mono.Cecil.Cil;

static partial class CecilEx
{
    private const FieldAttributes fieldAttributes = FieldAttributes.Public;

    public static void AddAsIsGenericParameters(this IDictionary<GenericParameter, GenericParameter> dict, IGenericParameterProvider source)
    {
        if (source.HasGenericParameters)
        {
            foreach (var gp in source.GenericParameters)
                dict[gp] = gp;
        }

        var method = source as MethodReference;
        if (method != null)
        {
            dict.AddAsIsGenericParameters(method);
            return;
        }
        var type = source as TypeReference;
        if(type != null)
        {
            dict.AddAsIsGenericParameters(type);
            return;
        }

        throw new InvalidOperationException();
    }

    private static void CloneGenericParameters(this IDictionary<GenericParameter, GenericParameter> dict, IGenericParameterProvider from, IGenericParameterProvider to)
    {
        var coll = to.GenericParameters;

        foreach (var param in from.GenericParameters)
        {
            if (param.HasGenericParameters)
                throw new InvalidOperationException();

            var clone = new GenericParameter(to);

            clone.Name = param.Name;
            clone.Attributes = param.Attributes;

            foreach (var attr in param.CustomAttributes)
                clone.CustomAttributes.Add(attr);

            foreach (var constraint in param.Constraints)
                clone.Constraints.Add(constraint);

            coll.Add(clone);

            dict.Add(param, clone);
        }
    }

    public static TypeReference TranslateGenerics(this TypeReference type, IDictionary<GenericParameter, GenericParameter> map)
    {
        if (!type.ContainsGenericParameter)
            return type;

        if (type.IsGenericParameter)
            return map[(GenericParameter)type];

        if (type.IsGenericInstance)
        {
            var typeGit = (GenericInstanceType)type;

            var git = new GenericInstanceType(typeGit.ElementType);
            var generics = git.GenericArguments;
            foreach (var ga in typeGit.GenericArguments)
                generics.Add(ga.TranslateGenerics(map));

            return git;
        }

        throw new InvalidOperationException();
    }

    public static TypeDefinition MakeClosureType(this MethodDefinition method, out MethodDefinition constructor)
    {
        var thisType = method.DeclaringType;
        var methodParams = method.Parameters;
        var signature = string.Join(",", methodParams.Select(p => p.ParameterType.FullName));
        signature = $".{method.Name}({signature}):{Guid.NewGuid()}:$CLOSURE";

        var dict = new Dictionary<GenericParameter, GenericParameter>();

        var type = new TypeDefinition("", signature, TypeAttributes.NestedPrivate);

        var fields = type.Fields;
        var paramsColl = methodParams as ICollection<ParameterReference>;
        if (paramsColl != null)
            fields.Capacity = paramsColl.Count + 1;

        var ctor = new MethodDefinition(".ctor", MethodAttributes.RTSpecialName | MethodAttributes.SpecialName, thisType.Module.TypeSystem.Void);
        type.Methods.Add(ctor);

        var @params = ctor.Parameters;
        var il = ctor.Body.GetILProcessor();

        var thisField = new FieldDefinition("this", fieldAttributes, thisType);
        fields.Add(thisField);
        @params.Add(new ParameterDefinition(thisType));
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Stfld, thisField);

        ushort i = 1;
        foreach (var param in methodParams)
        {
            var paramType = param.ParameterType.TranslateGenerics(dict);

            var field = new FieldDefinition("@" + param.Name, fieldAttributes, paramType);
            fields.Add(field);
            @params.Add(new ParameterDefinition(paramType));
            il.Emit(OpCodes.Ldarg, i);
            il.Emit(OpCodes.Stfld, field);
            i++;
        }

        il.Emit(OpCodes.Ret);

        constructor = ctor;


        thisType.NestedTypes.Add(type);

        return type;
    }
}
