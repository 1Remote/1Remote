using System;

namespace PRM.Core.I
{
    public interface ILanguageService
    {
        void AddXamlLanguageResources(string code, string fullName);
        string Translate(Enum e);
        string Translate(string key);
        string Translate(string key, params object[] parameters);
    }
}