using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GraphQL.Client.Abstractions;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using GraphQL.Types;
using Newtonsoft.Json.Linq;

namespace Ugpa.GraphQL.Linq
{
    internal sealed class IntrospectionSchemaSource : ISchemaSource
    {
        private readonly Lazy<ISchema> schema;

        public IntrospectionSchemaSource(string endPoint)
            : this(new GraphQLHttpClient(endPoint, new NewtonsoftJsonSerializer()))
        {
        }

        internal IntrospectionSchemaSource(IGraphQLClient client)
        {
            schema = new Lazy<ISchema>(() => InitSchema(client));
        }

        public ISchema GetSchema()
            => schema.Value;

        private Schema InitSchema(IGraphQLClient gqlClient)
        {
            var request = new global::GraphQL.GraphQLRequest { Query = GetIntrospectionQuery() };

            var result = Task.Run(async () => await gqlClient.SendQueryAsync<JObject>(request, CancellationToken.None)).Result.Data;

            var queryType = (JObject)result["__schema"]["queryType"];
            var types = ((JArray)result["__schema"]["types"]).Cast<JObject>().ToArray();

            var dummySchema = new Schema();
            dummySchema.Initialize();
            var cache = new Dictionary<string, IGraphType>();

            var qt = ResolveGraphType(_ => dummySchema.FindType(_) ?? (cache.ContainsKey(_) ? cache[_] : null), types, queryType, cache);

            var commonTypes = cache.Values.ToArray();

            foreach (var type in types)
                ResolveGraphType(_ => dummySchema.FindType(_) ?? (cache.ContainsKey(_) ? cache[_] : null), types, type, cache);

            var newSchema = new Schema();
            newSchema.Query = (IObjectGraphType)qt;

            foreach (var type in cache.Values.Except(commonTypes))
                newSchema.RegisterType(type);

            newSchema.Initialize();

            return newSchema;
        }

        private string GetIntrospectionQuery()
        {
            using var introspectionQueryStream = typeof(GqlContext).Assembly
                .GetManifestResourceStream(typeof(GqlContext), "Resources.IntrospectionQuery.gql")
                ?? throw new InvalidOperationException();

            using var introspectionQueryReader = new StreamReader(introspectionQueryStream);

            return introspectionQueryReader.ReadToEnd();
        }

        private IGraphType ResolveGraphType(Func<string, IGraphType> findType, JObject[] types, JObject type, Dictionary<string, IGraphType> graphTypeCache)
        {
            var typeName = (string)((JValue)type["name"]).Value;
            if (typeName is not null)
            {
                if (findType(typeName) is IGraphType graphType)
                    return graphType;

                type = types.First(_ => (string)((JValue)_["name"]).Value == typeName);
            }

            return ((JValue)type["kind"]).Value switch
            {
                "NON_NULL" => ResolveNonNullGraphType(findType, types, type, graphTypeCache),
                "OBJECT" => ResolveObjectGraphType(findType, types, type, graphTypeCache),
                "LIST" => ResolveListGraphType(findType, types, type, graphTypeCache),
                "SCALAR" => ResolveScalarGraphType(type),
                "INTERFACE" => ResolveInterfaceGraphType(findType, types, type, graphTypeCache),
                "INPUT_OBJECT" => ResolveInputObjectGraphType(findType, types, type, graphTypeCache),
                "ENUM" => ResolveEnumGraphType(findType, types, type, graphTypeCache),
                _ => throw new NotImplementedException()
            };
        }

        private ScalarGraphType ResolveScalarGraphType(JObject type)
        {
            return ((JValue)type["name"]).Value switch
            {
                "Int" => new IntGraphType { Name = "Int" },
                "Float" => new FloatGraphType { Name = "Float" },
                "ID" => new IdGraphType { Name = "ID" },
                _ => throw new NotImplementedException()
            };
        }

        private NonNullGraphType ResolveNonNullGraphType(Func<string, IGraphType> findType, JObject[] types, JObject type, Dictionary<string, IGraphType> graphTypeCache)
            => ResolveProvideResolvedType(findType, types, type, graphTypeCache, _ => new NonNullGraphType(_));

        private ListGraphType ResolveListGraphType(Func<string, IGraphType> findType, JObject[] types, JObject type, Dictionary<string, IGraphType> graphTypeCache)
            => ResolveProvideResolvedType(findType, types, type, graphTypeCache, _ => new ListGraphType(_));

        private TGraphType ResolveProvideResolvedType<TGraphType>(Func<string, IGraphType> findType, JObject[] types, JObject type, Dictionary<string, IGraphType> graphTypeCache, Func<IGraphType, TGraphType> create)
            where TGraphType : IProvideResolvedType
        {
            var ofType = (JObject)type["ofType"] ?? throw new InvalidOperationException();
            var gType = create(ResolveGraphType(findType, types, ofType, graphTypeCache));
            return gType;
        }

        private ObjectGraphType ResolveObjectGraphType(Func<string, IGraphType> findType, JObject[] types, JObject type, Dictionary<string, IGraphType> graphTypeCache)
        {
            var gType = ResolveComplexGraphType<ObjectGraphType>(findType, types, type, graphTypeCache);

            foreach (var @interface in ((JArray)type["interfaces"]).Cast<JObject>())
                gType.AddResolvedInterface((IInterfaceGraphType)ResolveGraphType(findType, types, @interface, graphTypeCache));

            return gType;
        }

        private InterfaceGraphType ResolveInterfaceGraphType(Func<string, IGraphType> findType, JObject[] types, JObject type, Dictionary<string, IGraphType> graphTypeCache)
        {
            var gType = ResolveComplexGraphType<InterfaceGraphType>(findType, types, type, graphTypeCache);
            gType.ResolveType = _ => throw new NotSupportedException();
            return gType;
        }

        private TGraphType ResolveComplexGraphType<TGraphType>(Func<string, IGraphType> findType, JObject[] types, JObject type, Dictionary<string, IGraphType> graphTypeCache)
            where TGraphType : IComplexGraphType, new()
        {
            var typeName = (string)((JValue)type["name"]).Value;
            var gType = new TGraphType { Name = typeName };
            graphTypeCache[typeName] = gType;

            foreach (var field in ((JArray)type["fields"]).Cast<JObject>())
            {
                var fieldType = (JObject)field["type"] ?? throw new InvalidOperationException();
                var fieldArgs = (JArray)field["args"] ?? throw new NotImplementedException();
                var queryArgs = fieldArgs
                    .Select(_ => new QueryArgument(ResolveGraphType(findType, types, (JObject)_["type"], graphTypeCache))
                    {
                        Name = (string)((JValue)_["name"]).Value
                    })
                    .ToArray();

                gType.AddField(new FieldType
                {
                    Name = (string)((JValue)field["name"]).Value,
                    ResolvedType = ResolveGraphType(findType, types, fieldType, graphTypeCache),
                    Arguments = new QueryArguments(queryArgs)
                });
            }

            return gType;
        }

        private InputObjectGraphType ResolveInputObjectGraphType(Func<string, IGraphType> findType, JObject[] types, JObject type, Dictionary<string, IGraphType> graphTypeCache)
        {
            var typeName = (string)((JValue)type["name"]).Value;
            var gType = new InputObjectGraphType { Name = typeName };
            graphTypeCache[typeName] = gType;

            foreach (var field in ((JArray)type["inputFields"]).Cast<JObject>())
            {
                var fieldType = (JObject)field["type"] ?? throw new InvalidOperationException();
                gType.AddField(new FieldType
                {
                    Name = (string)((JValue)field["name"]).Value,
                    ResolvedType = ResolveGraphType(findType, types, fieldType, graphTypeCache)
                });
            }

            return gType;
        }

        private EnumerationGraphType ResolveEnumGraphType(Func<string, IGraphType> findType, JObject[] types, JObject type, Dictionary<string, IGraphType> graphTypeCache)
        {
            var typeName = (string)((JValue)type["name"]).Value;
            var gType = new EnumerationGraphType { Name = typeName };

            foreach (var value in type["enumValues"])
                gType.AddValue(new EnumValueDefinition { Name = (string)((JValue)value["name"]).Value });

            return gType;
        }
    }
}
