using System.Reflection;

var ls = typeof(Luau.LuauState);
foreach (var m in ls.GetMethods(BindingFlags.Public|BindingFlags.Instance).Where(x => x.Name.Contains("CFunction") || x.Name.Contains("CClosure")))
    Console.WriteLine($"{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})");

foreach (var name in Enum.GetNames(typeof(Luau.LuauType)))
    Console.WriteLine($"LuauType.{name}");
