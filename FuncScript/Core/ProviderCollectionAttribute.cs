using System;
using System.Collections.Generic;

namespace FuncScript.Core
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class ProviderCollectionAttribute : Attribute
    {
        public ProviderCollectionAttribute(params string[] collectionNames)
        {
            CollectionNames = collectionNames ?? Array.Empty<string>();
        }

        public IReadOnlyList<string> CollectionNames { get; }

        public string[] MemberNames { get; set; } = Array.Empty<string>();
    }
}
