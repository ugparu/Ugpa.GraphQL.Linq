using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Client.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ugpa.GraphQL.Linq.Utils;

namespace Ugpa.GraphQL.Linq
{
    internal sealed class GqlQueryProvider : IQueryProvider
    {
        private readonly IGraphQLClient client;
        private readonly JsonSerializer serializer;
        private readonly GqlQueryBuilder queryBuilder;

        public GqlQueryProvider(IGraphQLClient client, GqlQueryBuilder queryBuilder)
            : this(client, queryBuilder, JsonSerializer.CreateDefault())
        {
        }

        public GqlQueryProvider(IGraphQLClient client, GqlQueryBuilder queryBuilder, JsonSerializer serializer)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.queryBuilder = queryBuilder ?? throw new ArgumentNullException(nameof(queryBuilder));
            this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        public IQueryable CreateQuery(Expression expression)
        {
            var elementType = expression.Type;
            return (IQueryable)((Func<Expression, IQueryable<object>>)CreateQuery<object>).Method
                .GetGenericMethodDefinition()
                .MakeGenericMethod(elementType)
                .Invoke(this, new[] { expression });
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            return new GqlQueryable<TElement>(this, expression);
        }

        public object Execute(Expression expression)
        {
            var variablesResolver = new VariablesResolver();
            var query = queryBuilder.BuildQuery(expression, variablesResolver, out var entryPoint);
            var request = new GraphQLRequest
            {
                Query = query,
                Variables = variablesResolver.GetAllVariables().ToDictionary(_ => _.name, _ => _.value)
            };

            var result = Task.Run(() => client.SendQueryAsync<JToken>(request, CancellationToken.None))
                .GetAwaiter()
                .GetResult();

            expression = RewriteExpression(expression, result.Data[entryPoint]);

            return expression switch
            {
                MethodCallExpression method => Expression.Lambda<Func<object>>(method).Compile()(),
                ConstantExpression constant => constant.Value,
                _ => throw new NotImplementedException()
            };
        }

        public TResult Execute<TResult>(Expression expression)
            => (TResult)Execute(expression);

        private Expression RewriteExpression(Expression expression, JToken data)
        {
            switch (expression)
            {
                case MethodCallExpression method:
                    {
                        return Expression.Call(
                            RewriteExpression(method.Object, data),
                            method.Method,
                            method.Arguments.Select(_ => RewriteExpression(_, data)));
                    }
                case ConstantExpression constant:
                    {
                        return constant.Value switch
                        {
                            IQueryable qq => RewriteConstantExpression(data, qq),
                            _ => expression
                        };
                    }
                case UnaryExpression unary:
                    {
                        return unary.NodeType switch
                        {
                            ExpressionType.Quote => Expression.Quote(RewriteExpression(unary.Operand, data)),
                            _ => throw new NotImplementedException()
                        };
                    }
                case LambdaExpression lambda:
                    {
                        return Expression.Lambda(RewriteExpression(lambda.Body, data), lambda.Parameters);
                    }
                case MemberExpression member:
                    {
                        return member.Member switch
                        {
                            PropertyInfo property => Expression.Property(RewriteExpression(member.Expression, data), property),
                            _ => throw new NotImplementedException()
                        };
                    }
                case ParameterExpression parameter:
                    {
                        return parameter;
                    }
                case null:
                    {
                        return null;
                    }
                default:
                    {
                        throw new NotImplementedException();
                    }
            };
        }

        private ConstantExpression RewriteConstantExpression(JToken data, IQueryable qq)
        {
            var list = Materialize(data, qq.ElementType);
            var newQuery = ((Func<IEnumerable<int>, IQueryable<int>>)Queryable.AsQueryable).Method
                .GetGenericMethodDefinition()
                .MakeGenericMethod(qq.ElementType)
                .Invoke(null, new[] { list });

            return Expression.Constant(newQuery);
        }

        private object Materialize(JToken data, Type type)
        {
            var listType = typeof(List<>).MakeGenericType(type);
            if (data is null)
            {
                return Activator.CreateInstance(listType);
            }
            else if (data is JArray jArray)
            {
                return jArray.ToObject(listType, serializer);
            }
            else if (data is JObject jObject)
            {
                var obj = jObject.ToObject(type, serializer);
                var list = Activator.CreateInstance(listType);
                ((IList)list).Add(obj);
                return list;
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }
}
