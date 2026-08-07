using System;
using System.IO;
using System.Reflection;

class Program
{
    static void Main()
    {
        var dir = @"C:\YukkuriMovieMaker4-20231229T073048Z-001\YukkuriMovieMaker4";
        foreach (var file in Directory.GetFiles(dir, "YukkuriMovieMaker*.dll"))
        {
            try
            {
                var asm = Assembly.LoadFrom(file);
                foreach (var type in asm.GetTypes())
                {
                    if (type.Name.Contains("Attribute", StringComparison.OrdinalIgnoreCase) || type.Name.Contains("Selector", StringComparison.OrdinalIgnoreCase) || type.Name.Contains("Button", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"{Path.GetFileName(file)} : {type.FullName}");
                    }
                }
            }
            catch { }
        }
    }
}
