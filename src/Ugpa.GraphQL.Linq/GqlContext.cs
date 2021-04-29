using System;
using System.Linq;
using GraphQL.Client.Abstractions;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using Newtonsoft.Json;
using Ugpa.GraphQL.Linq.Configuration;
using Ugpa.GraphQL.Linq.Utils;
using Ugpa.Json.Serialization;

namespace Ugpa.GraphQL.Linq
{
    public sealed class GqlContext
    {
        private readonly Lazy<GqlQueryProvider> queryProvider;

        private readonly FluentContext fluentContext;

        public GqlContext(string endPoint)
            : this(endPoint, new IntrospectionSchemaSource(endPoint))
        {
        }

        public GqlContext(string endPoint, ISchemaSource schemaSource)
            : this(() => new GraphQLHttpClient(endPoint, new NewtonsoftJsonSerializer()), schemaSource)
        {
        }

        internal GqlContext(Func<IGraphQLClient> clientFactory, ISchemaSource schemaSource)
        {
            queryProvider = new Lazy<GqlQueryProvider>(() =>
            {
                var gqlClient = clientFactory();
                var schema = schemaSource.GetSchema();

                var mapper = new GraphTypeMapper(schema, fluentContext);
                var serializer = GetSerializer(new EntityCache(mapper));
                var queryBuilder = new GqlQueryBuilder(schema, mapper);

                return new GqlQueryProvider(gqlClient, queryBuilder, serializer);
            });

            fluentContext = new FluentContext();
        }

        public void ConfigureTypes(Action<IGqlTypesConfigurator> configure)
        {
            configure(new TypesConfigurator(fluentContext));
        }

        public IQueryable<T> Get<T>()
            => queryProvider.Value.CreateQuery<T>(Enumerable.Empty<T>().AsQueryable().Expression);

        private JsonSerializer GetSerializer(EntityCache cache)
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = fluentContext,
                SerializationBinder = fluentContext,
                TypeNameHandling = TypeNameHandling.All,
                Converters =
                {
                    new GqlMaterializer(cache)
                }
            };

            FluentContextExtensions.Apply(fluentContext, settings);

            return JsonSerializer.Create(settings);
        }
    }
}
