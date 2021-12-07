using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shawn.Utils
{
    public static class AssemblyHelper
    {
        public static List<Type> GetSubClasses(Type parentType)
        {
            var subTypeList = new List<Type>();
            var assembly = parentType.Assembly;
            var assemblyAllTypes = assembly.GetTypes();
            foreach (var itemType in assemblyAllTypes)
            {
                if (itemType.IsSubclassOf(parentType))
                    subTypeList.Add(itemType);
            }
            return subTypeList.ToList();
        }

        public static List<Type> GetAllParenTypes(Type type)
        {
            var parents = new List<Type>();
            var t = type;
            while (t.BaseType != null)
            {
                parents.Add(t.BaseType);
                t = t.BaseType;
            }
            return parents;
        }

        public static Type FindCommonBaseClass(Type t1, Type t2)
        {
            if (t1 == t2)
                return t1;
            var parents1 = GetAllParenTypes(t1);
            var parents2 = GetAllParenTypes(t2);
            var parents3 = new List<Type>(parents1);
            parents1.AddRange(parents2);
            parents2.AddRange(parents3);
            for (int i = 0; i < parents1.Count; i++)
            {
                if (parents1[i] == parents2[i])
                    return parents2[i];
            }
            return null;
        }
    }
}
