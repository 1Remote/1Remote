using System;
using System.Linq;
using System.Reflection;

namespace Shawn.Utils
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Constructor | AttributeTargets.Parameter | AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class OtherNameAttribute : Attribute
    {
        public string Name { get; set; }
    }

    public static class OtherNameAttributeExtensions
    {
        //public string GetName<T>(PropertyInfo p)
        //{
        //    var t = typeof(T);
        //    p.GetCustomAttributes(typeof(OtherNameAttribute), false)
        //    return null;
        //}

        public static string Replace<T>(T obj, string template)
        {
            var t = typeof(T);
            var properties = t.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var p in properties)
            {
                var a = p.GetCustomAttributes(typeof(OtherNameAttribute), false).FirstOrDefault();
                if (a is OtherNameAttribute on)
                {
                    template = template.Replace($"%{on.Name}%", p.GetValue(obj)?.ToString() ?? "");
                }
            }
            return template;
        }
    }
}