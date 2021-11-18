using System.Reflection;

namespace Ugpa.GraphQL.Linq.Utils
{
    /// <summary>
    /// Represents member name mapper for query builder.
    /// </summary>
    internal interface IMemberNameMapper
    {
        /// <summary>
        /// Returns field name for a given member.
        /// </summary>
        /// <param name="member">Source member.</param>
        /// <returns>Mapped field name.</returns>
        string GetFieldName(MemberInfo member);
    }
}
