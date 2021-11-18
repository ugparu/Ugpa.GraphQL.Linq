using System;

namespace Ugpa.GraphQL.Linq.Utils
{
    /// <summary>
    /// Represents clr type to GraphQL type name mapper.
    /// </summary>
    internal interface IGraphTypeNameMapper
    {
        /// <summary>
        /// Returns GrqphQL type name for given clr type.
        /// </summary>
        /// <param name="objectType">Source clr type.</param>
        /// <returns>Mapped GraphQL type name.</returns>
        string GetTypeName(Type objectType);
    }
}
