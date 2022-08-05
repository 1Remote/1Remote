using System;
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

    public static T Get<T>(string? key = null) where T : class
    {
        var obj = Instances?.Get(typeof(T), key);
#if !DEBUG
        if (obj == null)
            throw new Exception("Ioc can not get an item.");
#endif
        return (T)obj!;
    }
}