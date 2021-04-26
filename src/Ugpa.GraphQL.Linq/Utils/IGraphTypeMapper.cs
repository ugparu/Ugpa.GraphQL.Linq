using System;
using GraphQL.Types;

namespace Ugpa.GraphQL.Linq.Utils
{
    internal interface IGraphTypeMapper
    {
        IGraphType GetGraphType(Type objectType);
    }
}
