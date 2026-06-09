namespace LUAstudio.Abstractions;

public interface IGlobalValue<T>
{
    string Key { get; }
    T Value { get; set; }

    event Action<T>? Changed;
}