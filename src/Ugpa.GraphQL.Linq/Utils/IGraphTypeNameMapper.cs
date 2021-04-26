using System;

namespace Ugpa.GraphQL.Linq.Utils
{
    internal interface IGraphTypeNameMapper
    {
        string GetTypeName(Type objectType);
    }
}
