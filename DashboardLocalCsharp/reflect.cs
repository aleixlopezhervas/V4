using System;
using System.Reflection;
using System.Linq;

class Program
{
    static void Main()
    {
        var asm = Assembly.LoadFrom(@"Formulario\bin\Debug\csDronLink.dll");
        var dronType = asm.GetTypes().FirstOrDefault(t => t.Name == "Dron");
        if (dronType != null) {
            foreach (var m in dronType.GetMethods()) {
                var parameters = string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name));
                Console.WriteLine($"{m.ReturnType.Name} {m.Name}({parameters})");
            }
        }
    }
}