using LUAstudio.Abstractions;

namespace LUAstudio.Core;

public class GlobalValue<T> : IGlobalValue<T>
{
    public string Key { get; }

    private T _value;

    public event Action<T>? Changed;

    public GlobalValue(string key, T initial)
    {
        Key = key;
        _value = initial;
    }

    public T Value
    {
        get => _value;
        set
        {
            if (Equals(_value, value))
            {
                return;
            }

            _value = value;
            Changed?.Invoke(_value);
        }
    }
}
