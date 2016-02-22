using System;
using System.Linq;
using Mono.Cecil;

static class CecilEx
{
    public static bool AreSame(TypeReference a, TypeReference b)
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

        return a.Module.Assembly.FullName == b.Module.Assembly.FullName;
    }

    public static bool IsSameAs(this TypeReference a, TypeReference b)
    {
        if (a == null)
            throw new NullReferenceException();
        if (b == null)
            throw new ArgumentNullException();

        if (a == b)
            return true;

        if (a.FullName != b.FullName)
            return false;

        return a.Module.Assembly.FullName == b.Module.Assembly.FullName;
    }

    public static bool IsAssignableFrom(this TypeDefinition type, TypeDefinition test)
    {
        if (type.IsSameAs(test))
            return true;

        if (type.IsInterface)
            return test.Interfaces.Any(i => i.Resolve().IsSameAs(type));

        if (type.IsValueType)
            return false;

        return test.IsSubclassOf(type);
    }

    public static bool IsEventuallyAccessible(this TypeDefinition type)
    {
        if (type.IsPublic)
            return true;

        if (type.IsNested)
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

    public static bool IsSubclassOf(this TypeDefinition type, TypeDefinition test)
    {
        if (type == null)
            throw new NullReferenceException();
        if (test == null)
            throw new ArgumentNullException();

        if (test.IsInterface)
            return test.IsAssignableFrom(type);

        var baseType = type.BaseType;
        if (baseType == null)
            return false;
        type = baseType.Resolve();

        if (type.IsSameAs(test))
            return true;

        return type.IsSubclassOf(test);
    }
}
