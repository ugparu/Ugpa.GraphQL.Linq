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
            TypeMapper = new GraphTypeMapper(schema);
        }

        public ISchema Schema { get; }

        public GraphTypeMapper TypeMapper { get; }

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
            var query = GqlQueryBuilder.BuildQuery(expression, variablesResolver, out var entryPoint);
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
                        if (constant.Value is IQueryable qq)
                        {
                            var type = ((IQueryable)qq).ElementType;
                            var listType = typeof(List<>).MakeGenericType(type);
                            object list;
                            if (data is null)
                            {
                                list = Activator.CreateInstance(listType);
                            }
                            else if (data is JArray jArray)
                            {
                                list = jArray.ToObject(listType);
                            }
                            else if (data is JObject jObject)
                            {
                                var obj = jObject.ToObject(type);
                                list = Activator.CreateInstance(listType);
                                ((IList)list).Add(obj);
                            }
                            else
                            {
                                throw new NotImplementedException();
                            }

                            var newQuery = ((Func<IEnumerable<int>, IQueryable<int>>)Queryable.AsQueryable).Method
                                .GetGenericMethodDefinition()
                                .MakeGenericMethod(type)
                                .Invoke(null, new[] { list });

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
