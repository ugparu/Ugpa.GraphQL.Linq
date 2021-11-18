using GraphQL.Types;

namespace Ugpa.GraphQL.Linq
{
    /// <summary>
    /// Represents an GraphQL schema source.
    /// </summary>
    public interface ISchemaSource
    {
        /// <summary>
        /// Returns GraphQL schema.
        /// </summary>
        /// <returns>GraphQL schema.</returns>
        ISchema GetSchema();
    }
}
