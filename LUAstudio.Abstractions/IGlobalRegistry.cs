namespace LUAstudio.Abstractions;

public interface IGlobalRegistry
{
    void Register<T>(string key, IGlobalValue<T> value);

    IGlobalValue<T>? Get<T>(string key);
}