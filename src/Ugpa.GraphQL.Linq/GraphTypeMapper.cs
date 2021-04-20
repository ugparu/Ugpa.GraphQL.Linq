using System;
using System.Linq;
using GraphQL.Types;

namespace Ugpa.GraphQL.Linq
{
    internal sealed class GraphTypeMapper
    {
        private readonly ISchema schema;

        public GraphTypeMapper(ISchema schema)
        {
            this.schema = schema;
        }

        public IGraphType GetGraphType(Type clrType)
        {
            var gType = schema.AllTypes.FirstOrDefault(_ =>
                _.GetType() is var t &&
                t.IsGenericType &&
                t.GenericTypeArguments.Length == 1 &&
                t.GenericTypeArguments[0] == clrType)

                ?? schema.AllTypes.FirstOrDefault(_ => _.Name == clrType.Name)
                ?? schema.AllTypes.FirstOrDefault(_ => _.Name == $"{clrType.Name}Type")
                ?? schema.AllTypes.FirstOrDefault(_ => _.Name == $"{clrType.Name}Interface");

            return gType ?? throw new InvalidOperationException();
        }
    }
}
