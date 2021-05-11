using System;

namespace Ugpa.GraphQL.Linq.Configuration
{
    public interface IGqlTypesConfigurator
    {
        IGqlTypesConfigurator Configure<T>(Action<IGqlTypeConfigurator<T>> configurator)
            where T : class;
    }
}
