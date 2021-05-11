using System;
using System.Linq.Expressions;

namespace Ugpa.GraphQL.Linq.Configuration
{
    public interface IGqlTypeConfigurator<T>
    {
        IGqlTypeConfigurator<T> HasField<TProperty>(Expression<Func<T, TProperty>> field, string name);

        IGqlTypeConfigurator<T> HasTypeName(string typeName);

        IGqlTypeConfigurator<T> ConstructWith(Func<T> factory);
    }
}
