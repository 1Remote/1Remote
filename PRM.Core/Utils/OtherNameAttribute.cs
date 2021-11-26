using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

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
            var t = obj.GetType();
            var properties = t.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var p in properties)
            {
                var a = p.GetCustomAttributes(typeof(OtherNameAttribute), false).FirstOrDefault();
                if (a is OtherNameAttribute on)
                {
                    Debug.Assert(on.Name.StartsWith("%") == false && on.Name.EndsWith("%") == false);
                    template = template.Replace($"%{on.Name}%", p.GetValue(obj)?.ToString() ?? "");
                }
            }
            return template;
        }

        /// <summary>
        /// return one type's all other names by dict(property name -> other name)
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static Dictionary<string, string> GetOtherNames(Type t)
        {
            var ret = new Dictionary<string, string>();
            var properties = t.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var p in properties)
            {
                var a = p.GetCustomAttributes(typeof(OtherNameAttribute), false).FirstOrDefault();
                if (a is OtherNameAttribute on)
                {
                    ret.Add(p.Name, $"%{on.Name}%");
                }
            }
            return ret;
        }
    }
}