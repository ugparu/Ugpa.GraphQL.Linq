using System;
using System.Linq;
using GraphQL.Client.Abstractions;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using GraphQL.Types;
using Newtonsoft.Json;
using Ugpa.GraphQL.Linq.Configuration;
using Ugpa.GraphQL.Linq.Utils;
using Ugpa.Json.Serialization;

namespace Ugpa.GraphQL.Linq
{
    /// <summary>
    /// Provides functionality to fetching data from GraphQL endpoint.
    /// </summary>
    public sealed class GqlContext
    {
        private readonly Lazy<GqlQueryProvider> queryProvider;

        private readonly FluentContext fluentContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="GqlContext"/> class with introspection schema source.
        /// </summary>
        /// <param name="endPoint">GraphQL endpoint url.</param>
        public GqlContext(string endPoint)
            : this(endPoint, new IntrospectionSchemaSource(endPoint))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GqlContext"/> class with intospection schema source and custom scalar type resolver.
        /// </summary>
        /// <param name="endPoint">GraphQL endpoint url.</param>
        /// <param name="scalarTypeResolver">Custom scalar type resolver.</param>
        public GqlContext(string endPoint, Func<string, ScalarGraphType?> scalarTypeResolver)
            : this(endPoint, new IntrospectionSchemaSource(endPoint, scalarTypeResolver ?? throw new ArgumentNullException(nameof(scalarTypeResolver))))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GqlContext"/> class with custom schema source.
        /// </summary>
        /// <param name="endPoint">GraphQL endpoint url.</param>
        /// <param name="schemaSource">Custom schema source.</param>
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

                var mapper = new GraphTypeMapper(schema, fluentContext!);
                var serializer = GetSerializer(new EntityCache(mapper));
                var typeNameMapper = new JsonContactMemberNameMapper(serializer.ContractResolver);
                var queryBuilder = new GqlQueryBuilder(schema, mapper, typeNameMapper);

                return new GqlQueryProvider(gqlClient, queryBuilder, serializer);
            });

            fluentContext = new FluentContext();
        }

        /// <summary>
        /// Configures types mapping.
        /// </summary>
        /// <param name="configure">Types mapping delegate.</param>
        public void ConfigureTypes(Action<IGqlTypesConfigurator> configure)
        {
            configure(new TypesConfigurator(fluentContext));
        }

        /// <summary>
        /// Creates and returns <see cref="IQueryable{T}"/> to fetch data from endpoint.
        /// </summary>
        /// <typeparam name="T">Data type.</typeparam>
        /// <returns>An instance of <see cref="IQueryable{T}"/> to fetch data.</returns>
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

            return JsonSerializer.Create(settings);
        }
    }
}
