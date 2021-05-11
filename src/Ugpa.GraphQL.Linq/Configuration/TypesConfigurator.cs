using System;
using Ugpa.Json.Serialization;

namespace Ugpa.GraphQL.Linq.Configuration
{
    internal sealed class TypesConfigurator : IGqlTypesConfigurator
    {
        private readonly FluentContext fluentContext;

        public TypesConfigurator(FluentContext fluentContext)
        {
            this.fluentContext = fluentContext;
        }

        public IGqlTypesConfigurator Configure<T>(Action<IGqlTypeConfigurator<T>> configurator)
            where T : class
        {
            fluentContext.Configure<T>(_ => configurator(new TypeConfigurator<T>(_)));
            return this;
        }
    }
}
