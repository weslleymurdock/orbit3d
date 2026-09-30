using System;
using System.Reflection;
using System.Linq;

class Program
{
    static void Main()
    {
        var asm = Assembly.LoadFrom(@"C:\Users\wesll\.nuget\packages\assimp.maui\6.0.5-rc4\lib\net10.0-windows10.0.19041\Assimp.Maui.dll");
        foreach (var type in asm.GetTypes().Where(t => t.IsPublic))
        {
            Console.WriteLine($"{type.Namespace}.{type.Name}");
        }
    }
}
