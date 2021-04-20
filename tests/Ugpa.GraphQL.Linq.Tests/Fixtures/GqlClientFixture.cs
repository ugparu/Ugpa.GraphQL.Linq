using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Client.Abstractions;
using GraphQL.Client.Serializer.Newtonsoft;
using GraphQL.NewtonsoftJson;
using GraphQL.Types;
using GraphQL.Utilities;

namespace Ugpa.GraphQL.Linq.Tests.Fixtures
{
    public sealed class GqlClientFixture
    {
        internal IGraphQLClient CreateClientFor(string typeDefinitions, object root = null, Action<SchemaBuilder> configure = null)
        {
            var schema = Schema.For(typeDefinitions, configure);
            schema.Initialize();
            return CreateClientFor(schema, root);
        }

        internal IGraphQLClient CreateClientFor(ISchema schema, object root = null)
            => new AutoClient(schema, root);

        private sealed class AutoClient : IGraphQLClient
        {
            private readonly IDocumentExecuter executer = new DocumentExecuter();
            private readonly IDocumentWriter writer = new DocumentWriter();

            private readonly ISchema schema;
            private readonly object root;

            public AutoClient(ISchema schema, object root)
            {
                this.schema = schema;
                this.root = root;
            }

            public IObservable<GraphQLResponse<TResponse>> CreateSubscriptionStream<TResponse>(GraphQLRequest request)
            {
                throw new NotImplementedException();
            }

            public IObservable<GraphQLResponse<TResponse>> CreateSubscriptionStream<TResponse>(GraphQLRequest request, Action<Exception> exceptionHandler)
            {
                throw new NotImplementedException();
            }

            public void Dispose()
            {
                throw new NotImplementedException();
            }

            public Task<GraphQLResponse<TResponse>> SendMutationAsync<TResponse>(GraphQLRequest request, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public async Task<GraphQLResponse<TResponse>> SendQueryAsync<TResponse>(GraphQLRequest request, CancellationToken cancellationToken = default)
            {
                var res = await executer
                    .ExecuteAsync(_ =>
                    {
                        _.Schema = schema;
                        _.Query = request.Query;
                        _.Root = root;
                    })
                    .ConfigureAwait(false);

                if (res.Errors is ExecutionErrors err && err.Any())
                    throw new Exception();

                var text = await writer
                    .WriteToStringAsync(res)
                    .ConfigureAwait(false);

                return await new NewtonsoftJsonSerializer()
                    .DeserializeFromUtf8StreamAsync<TResponse>(new MemoryStream(Encoding.UTF8.GetBytes(text)), cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }
}
