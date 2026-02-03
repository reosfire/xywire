using System.Reflection;
using System.Runtime.Loader;
using XywireHost.Core.core;

namespace XywireHost.Core.effects;

public class DynamicallyLoadedEffect : AbstractEffect
{
    private readonly LoadedEffectBase _currentEffect;
    private readonly Color[][] _colorsBuffer;
    
    public DynamicallyLoadedEffect(LedLine attachedLedLine) : base(attachedLedLine)
    {
        _colorsBuffer = Array2D.CreateJagged<Color>(attachedLedLine.Height, attachedLedLine.Width);
        _currentEffect = LoadFromFolder("plugins")[0];
    }

    protected override void MoveNext()
    {
        _currentEffect.FillFrame(_colorsBuffer);
        LedLine.SetColors(_colorsBuffer);
    }
    
    private static IReadOnlyList<LoadedEffectBase> LoadFromFolder(string folderPath)
    {
        List<LoadedEffectBase> result = [];
        
        folderPath = Path.GetFullPath(folderPath);
        if (!Directory.Exists(folderPath)) return result;

        foreach (string dllPath in Directory.EnumerateFiles(folderPath, "*.dll"))
        {
            AssemblyLoadContext alc = new(Path.GetFileNameWithoutExtension(dllPath), true);
            Assembly assembly = alc.LoadFromAssemblyPath(dllPath);
            foreach (Type type in assembly.GetTypes())
            {
                if (type.IsAbstract || !typeof(LoadedEffectBase).IsAssignableFrom(type)) continue;
                if (Activator.CreateInstance(type) is LoadedEffectBase plugin)
                    result.Add(plugin);
            }
        }

        return result;
    }
    
    protected override int StabilizeFps() => 60;
}
