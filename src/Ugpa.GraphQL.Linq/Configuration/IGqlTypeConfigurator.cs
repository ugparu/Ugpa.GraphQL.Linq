using System;
using System.Linq.Expressions;

namespace Ugpa.GraphQL.Linq.Configuration
{
    /// <summary>
    /// Represents a mapping configurator for type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Configured type.</typeparam>
    public interface IGqlTypeConfigurator<T>
    {
        /// <summary>
        /// Map property to GraphQL field.
        /// </summary>
        /// <typeparam name="TProperty">Property type.</typeparam>
        /// <param name="field">Type property representing GraphQL field.</param>
        /// <param name="name">GraphQL field name.</param>
        /// <returns>This instance of configurator.</returns>
        IGqlTypeConfigurator<T> HasField<TProperty>(Expression<Func<T, TProperty>> field, string name);

        /// <summary>
        /// Map type to GraphQL type name.
        /// </summary>
        /// <param name="typeName">GraphQL type name.</param>
        /// <returns>This instance of configurator.</returns>
        IGqlTypeConfigurator<T> HasTypeName(string typeName);

        /// <summary>
        /// Defines default cretor for type.
        /// </summary>
        /// <param name="factory">Factory delegate.</param>
        /// <returns>This instance of configurator.</returns>
        IGqlTypeConfigurator<T> ConstructWith(Func<T> factory);
    }
}
