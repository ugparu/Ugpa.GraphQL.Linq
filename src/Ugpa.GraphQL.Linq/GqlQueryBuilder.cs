using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using GraphQL.Types;

namespace Ugpa.GraphQL.Linq
{
    internal static class GqlQueryBuilder
    {
        private static readonly Lazy<MethodInfo> select = new Lazy<MethodInfo>(()
            => ((Func<IQueryable<int>, Expression<Func<int, int>>, IQueryable<int>>)Queryable.Select).Method.GetGenericMethodDefinition());

        private static readonly Lazy<MethodInfo> selectMany = new Lazy<MethodInfo>(()
            => ((Func<IQueryable<int>, Expression<Func<int, IEnumerable<int>>>, IQueryable<int>>)Queryable.SelectMany).Method.GetGenericMethodDefinition());

        private static readonly Lazy<MethodInfo> whereParams = new Lazy<MethodInfo>(()
            => ((Func<IQueryable<int>, int, IQueryable<int>>)QueryableExtensions.Where).Method.GetGenericMethodDefinition());


        public static string BuildQuery(Expression expression)
        {
            var (root, _) = GetQueryNode(expression, null, true);
            var query = root.ToQueryString();
            return query;
        }

        private static (GqlQueryNode root, GqlQueryNode head) GetQueryNode(Expression expression, IComplexGraphType owner, bool includeScalar)
        {
            return expression switch
            {
                MethodCallExpression methodCall => GetQueryNodeFromMethodCall(methodCall, owner, includeScalar),
                ConstantExpression constant => GetQueryNodeFromConstant(constant, includeScalar),
                UnaryExpression unary => GetQueryNode(unary.Operand, owner, includeScalar),
                LambdaExpression lambda => GetQueryNode(lambda.Body, owner, includeScalar),
                MemberExpression member => GetQueryNodeFromMember(member, owner, includeScalar),
                _ => throw new NotImplementedException()
            };
        }

        private static (GqlQueryNode root, GqlQueryNode head) GetQueryNodeFromConstant(ConstantExpression constant, bool includeScalar)
        {
            if (constant.Value is GqlQueryable qq)
            {
                if (qq.QueryName is null)
                    throw new NotImplementedException();

                var qqq = (IQueryable)qq;
                var provider = (GqlQueryProvider)qqq.Provider;
                var gType = GetGraphType(provider.Schema, qqq.ElementType);
                var field = provider.Schema.Query.Fields.FirstOrDefault(_ => _.Name.Equals(qq.QueryName, StringComparison.OrdinalIgnoreCase));

                if (field.ResolvedType != gType ||
                    field.ResolvedType is IProvideResolvedType nn && nn.ResolvedType != gType)
                {
                    throw new InvalidOperationException();
                }

                switch (gType)
                {
                    case IComplexGraphType complexGraphType:
                        {
                            var node = GetQueryNodeForComplexType(qq.QueryName, complexGraphType, includeScalar);
                            return (node, node);
                        }
                    default:
                        {
                            throw new NotImplementedException();
                        }
                }
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private static (GqlQueryNode root, GqlQueryNode head) GetQueryNodeFromMethodCall(MethodCallExpression methodCall, IComplexGraphType owner, bool includeScalar)
        {
            if (methodCall.Method.IsGenericMethod)
            {
                if (methodCall.Method.GetGenericMethodDefinition() == select.Value ||
                    methodCall.Method.GetGenericMethodDefinition() == selectMany.Value)
                {
                    var node = GetQueryNode(methodCall.Arguments[0], owner, false);
                    var subNode = GetQueryNode(methodCall.Arguments[1], (IComplexGraphType)node.head.GraphType, includeScalar);
                    node.head.Children.Add(subNode.root);
                    return (node.root, subNode.head);
                }
                else if (methodCall.Method.GetGenericMethodDefinition() == whereParams.Value)
                {
                    var node = GetQueryNode(methodCall.Arguments[0], owner, true);
                    return node;
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private static (GqlQueryNode root, GqlQueryNode head) GetQueryNodeFromMember(MemberExpression member, IComplexGraphType owner, bool includeScalar)
        {
            GqlQueryNode root = null;
            if (member.Expression is MemberExpression nestedMember)
            {
                var nestedNode = GetQueryNodeFromMember(nestedMember, owner, false);
                root = nestedNode.root;
                owner = (IComplexGraphType)nestedNode.head.GraphType;
            }

            var field = owner.Fields.FirstOrDefault(_ => _.Name.Equals(member.Member.Name, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException();

            var complexGraphType =
                field.ResolvedType as IComplexGraphType
                ?? (field.ResolvedType as IProvideResolvedType)?.ResolvedType as IComplexGraphType
                ?? throw new NotImplementedException();

            var node = GetQueryNodeForComplexType(field.Name, complexGraphType, includeScalar);
            if (root is null)
            {
                return (node, node);
            }
            else
            {
                root.Children.Add(node);
                return (root, node);
            }
        }

        private static GqlQueryNode GetQueryNodeForComplexType(string name, IComplexGraphType complexGraphType, bool includeScalarFields)
        {
            var node = new GqlQueryNode(name, complexGraphType, null);

            if (includeScalarFields)
            {
                var scalarFields = complexGraphType.Fields.Where(_ =>
                    _.ResolvedType is ScalarGraphType ||
                    _.ResolvedType is NonNullGraphType nn && nn.ResolvedType is ScalarGraphType);

                foreach (var field in scalarFields)
                    node.Children.Add(new GqlQueryNode(field.Name, field.ResolvedType, field.Arguments));
            }

            return node;
        }

        private static IGraphType GetGraphType(ISchema schema, Type clrType)
        {
            var gType = schema.AllTypes.FirstOrDefault(_ =>
                _.GetType() is var t &&
                t.IsGenericType &&
                t.GenericTypeArguments.Length == 1 &&
                t.GenericTypeArguments[0] == clrType);

            gType ??= schema.AllTypes.FirstOrDefault(_ => _.Name == $"{clrType.Name}Type");

            return gType ?? throw new InvalidOperationException();
        }

        private class GqlQueryNode
        {
            public GqlQueryNode(string name, IGraphType graphType, IEnumerable<QueryArgument> arguments)
            {
                Name = name;
                GraphType = graphType;
                Arguments = arguments;
            }

            public string Name { get; }

            public IGraphType GraphType { get; }

            public IEnumerable<QueryArgument> Arguments { get; }

            public ICollection<GqlQueryNode> Children { get; } = new List<GqlQueryNode>();

            public string ToQueryString()
            {
                var builder = new StringBuilder();
                builder.AppendLine("query {");
                ToQueryString(builder, string.Empty);
                builder.AppendLine("}");
                return builder.ToString();
            }

            public override string ToString()
                => ToQueryString();

            private void ToQueryString(StringBuilder queryBuilder, string indent)
            {
                indent += "  ";
                queryBuilder.Append($"{indent}{Name}");
                queryBuilder.AppendLine(" {");

                foreach (var child in Children)
                {
                    if (child.Children.Any())
                        child.ToQueryString(queryBuilder, indent);
                    else
                        queryBuilder.AppendLine($"{indent}  {child.Name}");
                }

                queryBuilder.AppendLine($"{indent}}}");
            }
        }
    }
}
