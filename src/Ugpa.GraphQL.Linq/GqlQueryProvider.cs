using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Client.Abstractions;
using GraphQL.Types;
using Newtonsoft.Json.Linq;

namespace Ugpa.GraphQL.Linq
{
    internal sealed class GqlQueryProvider : IQueryProvider
    {
        private readonly IGraphQLClient client;

        public GqlQueryProvider(IGraphQLClient client, ISchema schema)
        {
            this.client = client;
            Schema = schema;
        }

        public ISchema Schema { get; }

        public IQueryable CreateQuery(Expression expression)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        public TResult Execute<TResult>(Expression expression)
        {
            var variablesResolver = new VariablesResolver();
            var query = GqlQueryBuilder.BuildQuery(expression, variablesResolver);
            var request = new GraphQLRequest
            {
                Query = query,
                Variables = variablesResolver.GetAllVariables().ToDictionary(_ => _.name, _ => _.value)
            };

            var result = Task.Run(() => client.SendQueryAsync<JToken>(request, CancellationToken.None)).Result;

            expression = RewriteExpression(expression, result.Data);
            if (expression is MethodCallExpression method)
            {
                return Expression.Lambda<Func<TResult>>(method).Compile()();
            }

            throw new NotImplementedException();
        }

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
                        if (constant.Value is GqlQueryable qq && qq.QueryName is not null)
                        {
                            var type = ((IQueryable)qq).ElementType;
                            var listType = typeof(List<>).MakeGenericType(type);
                            var obj = data[qq.QueryName].ToObject(listType);

                            var newQuery = ((Func<IEnumerable<int>, IQueryable<int>>)Queryable.AsQueryable).Method
                                .GetGenericMethodDefinition()
                                .MakeGenericMethod(type)
                                .Invoke(null, new[] { obj });

                            return Expression.Constant(newQuery);
                        }
                        else
                        {
                            return expression;
                        }
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
            }
        }
    }
}
