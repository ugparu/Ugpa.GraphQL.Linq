using System;
using GraphQL.Types;

namespace Ugpa.GraphQL.Linq.Utils
{
    /// <summary>
    /// Represents a clr type to GraphQL type mapper.
    /// </summary>
    internal interface IGraphTypeMapper
    {
        /// <summary>
        /// Returns GraphQL type for given clr type.
        /// </summary>
        /// <param name="objectType">Source clr type.</param>
        /// <returns>Mapped GraphQL type.</returns>
        IGraphType GetGraphType(Type objectType);
    }
}
