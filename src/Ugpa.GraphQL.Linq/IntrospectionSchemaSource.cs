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
        {
            schema = new Lazy<ISchema>(() => InitSchema(new GraphQLHttpClient(endPoint, new NewtonsoftJsonSerializer())));
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
            var cache = new Dictionary<string, IGraphType>();

            var qt = ResolveGraphType(_ => dummySchema.FindType(_) ?? (cache.ContainsKey(_) ? cache[_] : null), types, queryType, cache);

            var commonTypes = cache.Values.ToArray();

            foreach (var type in types)
                ResolveGraphType(_ => dummySchema.FindType(_) ?? (cache.ContainsKey(_) ? cache[_] : null), types, type, cache);

            var newSchema = new Schema();
            newSchema.Query = (IObjectGraphType)qt;

            foreach (var type in cache.Values.Except(commonTypes))
                newSchema.RegisterType(type);

            _ = newSchema.AllTypes;

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
                "SCALAR" => ResolveScalarGraphType(findType, types, type, graphTypeCache),
                "INTERFACE" => ResolveInterfaceGraphType(findType, types, type, graphTypeCache),
                "INPUT_OBJECT" => ResolveInputObjectGraphType(findType, types, type, graphTypeCache),
                _ => throw new NotImplementedException()
            };
        }

        private IGraphType ResolveScalarGraphType(Func<string, IGraphType> findType, JObject[] types, JObject type, Dictionary<string, IGraphType> graphTypeCache)
        {
            return ((JValue)type["name"]).Value switch
            {
                "Int" => new IntGraphType { Name = "Int" },
                "Float" => new FloatGraphType { Name = "Float" },
                "ID" => new IdGraphType { Name = "ID" },
                _ => throw new NotImplementedException()
            };
        }

        private IGraphType ResolveNonNullGraphType(Func<string, IGraphType> findType, JObject[] types, JObject type, Dictionary<string, IGraphType> graphTypeCache)
        {
            var ofType = (JObject)type["ofType"] ?? throw new InvalidOperationException();
            var gType = new NonNullGraphType(ResolveGraphType(findType, types, ofType, graphTypeCache));
            return gType;
        }

        private IGraphType ResolveListGraphType(Func<string, IGraphType> findType, JObject[] types, JObject type, Dictionary<string, IGraphType> graphTypeCache)
        {
            var ofType = (JObject)type["ofType"] ?? throw new InvalidOperationException();
            var gType = new ListGraphType(ResolveGraphType(findType, types, ofType, graphTypeCache));
            return gType;
        }

        private IObjectGraphType ResolveObjectGraphType(Func<string, IGraphType> findType, JObject[] types, JObject type, Dictionary<string, IGraphType> graphTypeCache)
        {
            var typeName = (string)((JValue)type["name"]).Value;
            var gType = new ObjectGraphType { Name = typeName };
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

            foreach (var @interface in ((JArray)type["interfaces"]).Cast<JObject>())
            {
                gType.AddResolvedInterface((IInterfaceGraphType)ResolveGraphType(findType, types, @interface, graphTypeCache));
            }

            return gType;
        }

        private IInterfaceGraphType ResolveInterfaceGraphType(Func<string, IGraphType> findType, JObject[] types, JObject type, Dictionary<string, IGraphType> graphTypeCache)
        {
            var typeName = (string)((JValue)type["name"]).Value;
            var gType = new InterfaceGraphType { Name = typeName };
            graphTypeCache[typeName] = gType;

            foreach (var field in ((JArray)type["fields"]).Cast<JObject>())
            {
                var fieldType = (JObject)field["type"] ?? throw new InvalidOperationException();
                gType.AddField(new FieldType
                {
                    Name = (string)((JValue)field["name"]).Value,
                    ResolvedType = ResolveGraphType(findType, types, fieldType, graphTypeCache)
                });
            }

            gType.ResolveType = _ => throw new NotSupportedException();

            return gType;
        }

        private IInputObjectGraphType ResolveInputObjectGraphType(Func<string, IGraphType> findType, JObject[] types, JObject type, Dictionary<string, IGraphType> graphTypeCache)
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
    }
}
