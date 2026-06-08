using LUAstudio.Abstractions;
using System.Collections.Generic;

namespace LUAstudio.Core;

public class GlobalRegistry : IGlobalRegistry
{
    private readonly Dictionary<string, object> _values = new();

    public void Register<T>(string key, IGlobalValue<T> value)
    {
        _values[key] = value;
    }

    public IGlobalValue<T>? Get<T>(string key)
    {
        if (_values.TryGetValue(key, out var obj))
        {
            return obj as IGlobalValue<T>;
        }

        return null;
    }
}
