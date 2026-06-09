namespace LUAstudio.Abstractions;

public interface IPlugin
{
    string Name { get; }

    void Initialize(IGlobalRegistry registry);
}