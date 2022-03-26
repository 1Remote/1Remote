using System;
using System.Collections.Generic;
using System.Linq;
using StyletIoC;

public static class IoC
{
    public static Func<Type, string, object> GetInstance = (service, key) => throw new InvalidOperationException("IoC is not initialized");

    public static IContainer Instances = null;

    public static Action<object> BuildUp = instance => throw new InvalidOperationException("IoC is not initialized");

    public static T Get<T>(string key = null)
    {
        return (T)GetInstance(typeof(T), key);
    }
}