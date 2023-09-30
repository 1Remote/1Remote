using System;
using _1RM.Service;
using Shawn.Utils.Interface;
using StyletIoC;

namespace _1RM;

public static class IoC
{
    public static void Init(IContainer iContainer)
    {
        Instances = iContainer;
        BuildUp = iContainer.BuildUp;
    }

    public static IContainer? Instances { get; private set; } = null;

    public static Action<object> BuildUp { get; private set; } = instance => throw new InvalidOperationException("IoC is not initialized");

    public delegate object? GetByTypeDelegate(Type type, string? key = null);

    public static GetByTypeDelegate GetByType = (type, key) => Instances?.Get(type, key);

    public static T Get<T>(string? key = null) where T : class
    {
        var obj = TryGet<T>(key);
        // if T is ILanguageService
        if (obj == null && typeof(T).IsAssignableFrom(typeof(ILanguageService)))
        {
            return (new MockLanguageService() as T)!;
        }
#if !DEBUG
        if (obj == null)
            throw new Exception("Ioc can not get an item.");
#endif
        return (T)obj!;
    }


    public static T? TryGet<T>(string? key = null) where T : class
    {
        var obj = GetByType(typeof(T), key);
        return obj as T;
    }
}