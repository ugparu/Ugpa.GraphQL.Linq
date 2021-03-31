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
    public sealed class GqlContext
    {
        private readonly Lazy<IGraphQLClient> gqlClient;

        private readonly Lazy<GqlQueryProvider> queryProvider;

        private readonly Lazy<ISchema> schema;

        public GqlContext(string endPoint)
            : this(endPoint, new IntrospectionSchemaSource(endPoint))
        {
        }

        public GqlContext(string endPoint, ISchemaSource schemaSource)
        {
            gqlClient = new Lazy<IGraphQLClient>(() => new GraphQLHttpClient(endPoint, new NewtonsoftJsonSerializer()));
            schema = new Lazy<ISchema>(() => schemaSource.GetSchema());
            queryProvider = new Lazy<GqlQueryProvider>(() => new GqlQueryProvider(gqlClient.Value, schema.Value));
        }

        public IQueryable<T> Get<T>(string queryName)
            => new GqlQueryable<T>(queryProvider.Value, queryName);
    }
}
