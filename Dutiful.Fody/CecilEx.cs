using System;
using System.Linq;
using Mono.Cecil;
using System.Collections;
using System.Collections.Generic;
using Mono.Cecil.Rocks;

static class CecilEx
{
    public static bool AreSame(TypeReference a, TypeReference b, bool? assemblyFullName = null)
    {
        var aIsNull = a == null;
        var bIsNull = b == null;

        if (aIsNull)
        {
            if (bIsNull)
                return true;
            return false;
        }
        if (bIsNull)
            return false;

        if (a == b)
            return true;

        if (a.FullName != b.FullName)
            return false;

        if (!assemblyFullName.HasValue)
            return true;

        var _a = a.Resolve().Module.Assembly;
        var _b = b.Resolve().Module.Assembly;
        if (assemblyFullName.Value)
            return _a.FullName == _b.FullName;
        return _a.Name.Name == _b.Name.Name;
    }

    public static bool IsSameAs(this TypeReference a, TypeReference b, bool? assemblyFullName = null)
    {
        if (a == null)
            throw new NullReferenceException();
        if (b == null)
            throw new ArgumentNullException();

        if (a == b)
            return true;

        if (a.FullName != b.FullName)
            return false;

        if (!assemblyFullName.HasValue)
            return true;

        var _a = a.Resolve().Module.Assembly;
        var _b = b.Resolve().Module.Assembly;
        if (assemblyFullName.Value)
            return _a.FullName == _b.FullName;
        return _a.Name.Name == _b.Name.Name;
    }

    public static bool IsAssignableFrom(this TypeReference target, TypeReference from, bool? assemblyFullName = null)
    {
        if (target.IsSameAs(from, assemblyFullName))
            return true;

        var targetDefinition = target.Resolve();
        var fromDefinition = from.Resolve();

        if (targetDefinition.IsInterface)
            return fromDefinition.Interfaces.Any(i => i.Resolve().IsSameAs(target, assemblyFullName));

        if (target.IsValueType)
            return false;

        return fromDefinition.IsSubclassOf(targetDefinition, assemblyFullName);
    }
    public static bool IsAssignableTo(this TypeReference source, TypeReference to, bool? assemblyFullName = null)
        => to.IsAssignableFrom(source, assemblyFullName);

    public static bool IsEventuallyAccessible(this TypeDefinition type)
    {
        if (type.IsPublic)
            return true;

        if (type.IsNested && type.DeclaringType.IsEventuallyAccessible())
        {
            if (type.IsNestedPublic)
                return true;
            if (type.IsNestedFamily)
                return true;
            if (type.IsNestedFamilyOrAssembly)
                return true;
        }

        return false;
    }

    public static bool IsSubclassOf(this TypeDefinition type, TypeDefinition test, bool? assemblyFullName = null)
    {
        if (type == null)
            throw new NullReferenceException();
        if (test == null)
            throw new ArgumentNullException();

        if (test.IsInterface)
            return test.IsAssignableFrom(type, assemblyFullName);

        var baseType = type.BaseType;
        if (baseType == null)
            return false;
        type = baseType.Resolve();

        if (type.IsSameAs(test, assemblyFullName))
            return true;

        return type.IsSubclassOf(test, assemblyFullName);
    }

    public static bool AreMatch(IEnumerable<TypeReference> self, IEnumerable<TypeReference> test, bool? assemblyFullName = null)
    {
        return self.SequenceEqual(test, (a, b) => AreSame(a, b, assemblyFullName));
    }
}
