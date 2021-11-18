using System;

namespace Ugpa.GraphQL.Linq.Configuration
{
    /// <summary>
    /// Represents an configurator for GraphQL serialization process.
    /// </summary>
    public interface IGqlTypesConfigurator
    {
        /// <summary>
        /// Configures mapping for type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Configured type.</typeparam>
        /// <param name="configurator">Type configurator delegate.</param>
        /// <returns>This instance of configurator.</returns>
        IGqlTypesConfigurator Configure<T>(Action<IGqlTypeConfigurator<T>> configurator)
            where T : class;
    }
}
