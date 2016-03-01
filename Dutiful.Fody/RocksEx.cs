using System;
using System.Linq;

namespace Mono.Cecil.Rocks
{
    static partial class RocksEx
    {
        public static bool AreSame(TypeReference a, TypeReference b, bool? useAssemblyFullName = null)
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

            if (!useAssemblyFullName.HasValue)
                return true;

            var _a = a.Resolve().Module.Assembly;
            var _b = b.Resolve().Module.Assembly;
            if (useAssemblyFullName.Value)
                return _a.FullName == _b.FullName;
            return _a.Name.Name == _b.Name.Name;
        }

        public static bool IsSameAs(this TypeReference a, TypeReference b, bool? useAssemblyFullName = null)
        {
            if (a == null)
                throw new NullReferenceException();
            if (b == null)
                throw new ArgumentNullException();

            if (a == b)
                return true;

            if (a.FullName != b.FullName)
                return false;

            if (!useAssemblyFullName.HasValue)
                return true;

            var _a = a.Resolve().Module.Assembly;
            var _b = b.Resolve().Module.Assembly;
            if (useAssemblyFullName.Value)
                return _a.FullName == _b.FullName;
            return _a.Name.Name == _b.Name.Name;
        }

        public static bool IsAssignableFrom(this TypeReference target, TypeReference from, bool? useAssemblyFullName = null)
        {
            if (target.IsSameAs(from, useAssemblyFullName))
                return true;

            var targetDefinition = target.Resolve();
            var fromDefinition = from.Resolve();

            if (targetDefinition.IsInterface)
                return fromDefinition.Interfaces.Any(i => i.Resolve().IsSameAs(target, useAssemblyFullName));

            if (target.IsValueType)
                return false;

            return fromDefinition.IsSubclassOf(targetDefinition, useAssemblyFullName);
        }
        public static bool IsAssignableTo(this TypeReference source, TypeReference to, bool? useAssemblyFullName = null)
            => to.IsAssignableFrom(source, useAssemblyFullName);

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

        public static bool IsSubclassOf(this TypeDefinition type, TypeDefinition test, bool? useAssemblyFullName = null)
        {
            if (type == null)
                throw new NullReferenceException();
            if (test == null)
                throw new ArgumentNullException();

            if (test.IsInterface)
                return test.IsAssignableFrom(type, useAssemblyFullName);

            var baseType = type.BaseType;
            if (baseType == null)
                return false;
            type = baseType.Resolve();

            if (type.IsSameAs(test, useAssemblyFullName))
                return true;

            return type.IsSubclassOf(test, useAssemblyFullName);
        }
    }
}
