using System;
using System.Collections.Generic;
using System.Reflection;
using DataGraph.Runtime;

namespace DataGraph.Editor.Reflection
{
    /// <summary>
    /// Per-parse cache for Type/Field/Method lookups. The parse pipeline
    /// resolves the same generated entry type and the same field names
    /// for every row of a sheet — without caching, each lookup rescans
    /// the type's metadata via reflection. One instance is created at
    /// the top of a parse run and threaded through helpers so members
    /// resolve once per (Type, name, flags) tuple.
    ///
    /// FindType delegates to <see cref="TypeFinder"/> rather than keeping
    /// a parallel type cache; that keeps a single source of truth for
    /// type lookups across the editor.
    /// </summary>
    internal sealed class ReflectionCache
    {
        private const BindingFlags DefaultFieldFlags =
            BindingFlags.Public | BindingFlags.Instance;

        private const BindingFlags DefaultMethodFlags =
            BindingFlags.Public | BindingFlags.Instance;

        private readonly Dictionary<FieldKey, FieldInfo> _fields = new();
        private readonly Dictionary<MethodKey, MethodInfo> _methods = new();

        /// <summary>
        /// Resolves a Type by full name. Delegates to TypeFinder so the
        /// global success cache is shared across all parse runs.
        /// </summary>
        public Type FindType(string fullName) => TypeFinder.Find(fullName);

        /// <summary>
        /// Resolves a Type by user-defined short name, trying the
        /// DataGraph-generated namespace first. Delegates to TypeFinder.
        /// </summary>
        public Type FindGenerated(string typeName) => TypeFinder.FindGenerated(typeName);

        /// <summary>
        /// Returns a public instance field by name, or the result for the
        /// given binding flags. Caches successful lookups for the lifetime
        /// of this instance.
        /// </summary>
        public FieldInfo GetField(Type type, string name) =>
            GetField(type, name, DefaultFieldFlags);

        /// <summary>
        /// Returns a field by name with explicit binding flags.
        /// </summary>
        public FieldInfo GetField(Type type, string name, BindingFlags flags)
        {
            if (type == null || string.IsNullOrEmpty(name)) return null;
            var key = new FieldKey(type, name, flags);
            if (_fields.TryGetValue(key, out var cached)) return cached;

            var field = type.GetField(name, flags);
            if (field != null) _fields[key] = field;
            return field;
        }

        /// <summary>
        /// Returns a public instance method by name. Caches successful
        /// lookups. For overloaded methods, prefer the BindingFlags
        /// overload or use reflection directly.
        /// </summary>
        public MethodInfo GetMethod(Type type, string name) =>
            GetMethod(type, name, DefaultMethodFlags);

        /// <summary>
        /// Returns a method by name with explicit binding flags.
        /// </summary>
        public MethodInfo GetMethod(Type type, string name, BindingFlags flags)
        {
            if (type == null || string.IsNullOrEmpty(name)) return null;
            var key = new MethodKey(type, name, flags, null);
            if (_methods.TryGetValue(key, out var cached)) return cached;

            var method = type.GetMethod(name, flags);
            if (method != null) _methods[key] = method;
            return method;
        }

        /// <summary>
        /// Returns a method by name and explicit parameter signature.
        /// Used for disambiguating overloads (e.g. FromFloat_UNSAFE(float)
        /// vs FromFloat_UNSAFE(double)).
        /// </summary>
        public MethodInfo GetMethod(Type type, string name, BindingFlags flags, Type[] parameterTypes)
        {
            if (type == null || string.IsNullOrEmpty(name)) return null;
            var key = new MethodKey(type, name, flags, parameterTypes);
            if (_methods.TryGetValue(key, out var cached)) return cached;

            var method = type.GetMethod(name, flags, null, parameterTypes ?? Type.EmptyTypes, null);
            if (method != null) _methods[key] = method;
            return method;
        }

        private readonly struct FieldKey : IEquatable<FieldKey>
        {
            private readonly Type _type;
            private readonly string _name;
            private readonly BindingFlags _flags;

            public FieldKey(Type type, string name, BindingFlags flags)
            {
                _type = type; _name = name; _flags = flags;
            }

            public bool Equals(FieldKey other) =>
                _type == other._type && _name == other._name && _flags == other._flags;

            public override bool Equals(object obj) => obj is FieldKey k && Equals(k);

            public override int GetHashCode() =>
                HashCode.Combine(_type, _name, (int)_flags);
        }

        private readonly struct MethodKey : IEquatable<MethodKey>
        {
            private readonly Type _type;
            private readonly string _name;
            private readonly BindingFlags _flags;
            private readonly Type[] _parameterTypes;

            public MethodKey(Type type, string name, BindingFlags flags, Type[] parameterTypes)
            {
                _type = type; _name = name; _flags = flags; _parameterTypes = parameterTypes;
            }

            public bool Equals(MethodKey other)
            {
                if (_type != other._type || _name != other._name || _flags != other._flags)
                    return false;
                if (ReferenceEquals(_parameterTypes, other._parameterTypes)) return true;
                if (_parameterTypes == null || other._parameterTypes == null) return false;
                if (_parameterTypes.Length != other._parameterTypes.Length) return false;
                for (int i = 0; i < _parameterTypes.Length; i++)
                    if (_parameterTypes[i] != other._parameterTypes[i]) return false;
                return true;
            }

            public override bool Equals(object obj) => obj is MethodKey k && Equals(k);

            public override int GetHashCode()
            {
                int hash = HashCode.Combine(_type, _name, (int)_flags);
                if (_parameterTypes != null)
                    foreach (var p in _parameterTypes)
                        hash = HashCode.Combine(hash, p);
                return hash;
            }
        }
    }
}
