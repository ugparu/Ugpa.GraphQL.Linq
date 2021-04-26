using System;
using System.Linq;
using GraphQL.Types;
using Newtonsoft.Json.Serialization;

namespace Ugpa.GraphQL.Linq.Utils
{
    internal sealed class GraphTypeMapper : IGraphTypeMapper, IGraphTypeNameMapper
    {
        private readonly ISchema schema;
        private readonly ISerializationBinder serializationBinder;

        public GraphTypeMapper(ISchema schema, ISerializationBinder serializationBinder)
        {
            this.schema = schema ?? throw new ArgumentNullException(nameof(schema));
            this.serializationBinder = serializationBinder ?? throw new ArgumentNullException(nameof(serializationBinder));
        }

        public IGraphType GetGraphType(Type objectType)
        {
            var typeName = GetTypeName(objectType);
            return schema.AllTypes.FirstOrDefault(_ => _.Name == typeName) ?? throw new InvalidOperationException();
        }

        public string GetTypeName(Type objectType)
        {
            serializationBinder.BindToName(objectType, out _, out var typeName);

            if (typeName == null || typeName == objectType.FullName)
                typeName = objectType.Name;

            return typeName;
        }
    }
}
