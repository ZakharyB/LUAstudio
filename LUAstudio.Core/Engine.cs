using LUAstudio.Abstractions;

namespace LUAstudio.Core;

public static class Engine
{
    public static IGlobalRegistry Globals { get; private set; } = null!;

    public static void Initialize()
    {
        Globals = new GlobalRegistry();
    }
}
