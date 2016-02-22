using System.Linq;
using Mono.Cecil;

static class CecilEx
{
    public static bool IsAssignableFrom(this TypeDefinition type, TypeDefinition derived)
    {
        if (type == derived) return true;

        if (type.IsInterface)
            return derived.Interfaces.Any(i => type.IsAssignableFrom(i.Resolve()));

        if (type.IsValueType) return false;

        return derived.IsSubclassOf(type);
    }

    public static bool IsSubclassOf(this TypeDefinition type, TypeDefinition @base)
    {
        type = type.BaseType?.Resolve();

        if (type == @base) return true;
        if (type == null) return false;

        return type.IsSubclassOf(@base);
    }
}
