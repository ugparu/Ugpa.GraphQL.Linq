using System;
using System.Collections.Generic;
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
    public sealed class GqlContext
    {
        private readonly Lazy<IGraphQLClient> gqlClient;

        private readonly Lazy<GqlQueryProvider> queryProvider;

        private readonly Lazy<Schema> schema;

        public GqlContext(string endPoint)
        {
            gqlClient = new Lazy<IGraphQLClient>(() => new GraphQLHttpClient(endPoint, new NewtonsoftJsonSerializer()));
            schema = new Lazy<Schema>(() => InitSchema());
            queryProvider = new Lazy<GqlQueryProvider>(() => new GqlQueryProvider(gqlClient.Value, schema.Value));
        }

        public IQueryable<T> Get<T>(string queryName)
            => new GqlQueryable<T>(queryProvider.Value, queryName);

        private Schema InitSchema()
        {
            var q = @"{
  __schema {
    queryType {
      name
      kind
    }
    types {
      name
      kind
      fields {
        name
        type {
          name
          kind
          ofType {
            name
            kind
          }
        }
      }
      enumValues {
        name
      }
      interfaces {
        kind
        name
      }
    }
  }
}";


            var request = new global::GraphQL.GraphQLRequest { Query = q };

            var result = Task.Run(async () => await gqlClient.Value.SendQueryAsync<JObject>(request, CancellationToken.None)).Result.Data;

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
                gType.AddField(new FieldType
                {
                    Name = (string)((JValue)field["name"]).Value,
                    ResolvedType = ResolveGraphType(findType, types, fieldType, graphTypeCache)
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
    }
}
