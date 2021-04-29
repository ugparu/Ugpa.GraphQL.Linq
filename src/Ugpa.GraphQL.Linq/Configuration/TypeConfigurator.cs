using System;
using Ugpa.Json.Serialization;

namespace Ugpa.GraphQL.Linq.Configuration
{
    internal sealed class TypeConfigurator<T> : IGqlTypeConfigurator<T>
    {
        private readonly FluentContractBuilder<T> contractBuilder;

        public TypeConfigurator(FluentContractBuilder<T> contractBuilder)
        {
            this.contractBuilder = contractBuilder;
        }

        public IGqlTypeConfigurator<T> HasField<TProperty>(System.Linq.Expressions.Expression<Func<T, TProperty>> field, string name)
        {
            contractBuilder.HasProperty(field, name, false);
            return this;
        }

        public IGqlTypeConfigurator<T> HasTypeName(string typeName)
        {
            contractBuilder.HasContractName(typeName);
            return this;
        }
    }
}
