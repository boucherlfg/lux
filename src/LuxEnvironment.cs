using System.Collections.Generic;

namespace Lux
{
    public class LuxEnvironment
    {
        private readonly Dictionary<string, object?> _values = new Dictionary<string, object?>();
        public LuxEnvironment? Enclosing { get; }

        public LuxEnvironment(LuxEnvironment? enclosing = null) => Enclosing = enclosing;

        public void Define(string name, object? value) => _values[name] = value;

        public IEnumerable<string> GetNames() => _values.Keys;

        public object? Get(string name, int line)
        {
            if (_values.TryGetValue(name, out object? value)) return value;
            if (Enclosing != null) return Enclosing.Get(name, line);
            throw new LuxError($"Undefined variable '{name}'", line);
        }

        public void Set(string name, object? value, int line)
        {
            if (_values.ContainsKey(name)) { _values[name] = value; return; }
            if (Enclosing != null)         { Enclosing.Set(name, value, line); return; }
            throw new LuxError($"Undefined variable '{name}'", line);
        }
    }
}
