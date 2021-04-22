using System;
using System.Linq;
using GraphQL.Types;
using Newtonsoft.Json.Serialization;

namespace Ugpa.GraphQL.Linq.Utils
{
    internal sealed class GraphTypeMapper
    {
        private readonly ISchema schema;
        private readonly ISerializationBinder binder;

        public GraphTypeMapper(ISchema schema, ISerializationBinder binder)
        {
            this.schema = schema ?? throw new ArgumentNullException(nameof(schema));
            this.binder = binder ?? throw new ArgumentNullException(nameof(schema));
        }

        public IGraphType GetGraphType(Type clrType)
        {
            binder.BindToName(clrType, out var assemblyName, out var typeName);

            if (typeName == null || typeName == clrType.FullName)
                typeName = clrType.Name;

            var gType = schema.AllTypes.FirstOrDefault(_ => _.Name == typeName);
            return gType ?? throw new InvalidOperationException();
        }
    }
}
