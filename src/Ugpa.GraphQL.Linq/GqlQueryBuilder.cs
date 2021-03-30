using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using GraphQL.Types;

namespace Ugpa.GraphQL.Linq
{
    internal static partial class GqlQueryBuilder
    {
        private static readonly Lazy<MethodInfo> select = new Lazy<MethodInfo>(()
            => ((Func<IQueryable<int>, Expression<Func<int, int>>, IQueryable<int>>)Queryable.Select).Method.GetGenericMethodDefinition());

        private static readonly Lazy<MethodInfo> selectMany = new Lazy<MethodInfo>(()
            => ((Func<IQueryable<int>, Expression<Func<int, IEnumerable<int>>>, IQueryable<int>>)Queryable.SelectMany).Method.GetGenericMethodDefinition());

        private static readonly Lazy<MethodInfo> whereParams = new Lazy<MethodInfo>(()
            => ((Func<IQueryable<int>, int, IQueryable<int>>)QueryableExtensions.Where).Method.GetGenericMethodDefinition());


        public static string BuildQuery(Expression expression, VariablesResolver variablesResolver)
        {
            var (root, _) = GetQueryNode(expression, null, true, variablesResolver, null);
            var query = root.ToQueryString();
            return query;
        }

        private static (GqlQueryNode root, GqlQueryNode head) GetQueryNode(Expression expression, IComplexGraphType owner, bool includeScalar, VariablesResolver variablesResolver, object variablesSource)
        {
            return expression switch
            {
                MethodCallExpression methodCall => GetQueryNodeFromMethodCall(methodCall, owner, includeScalar, variablesResolver, variablesSource),
                ConstantExpression constant => GetQueryNodeFromConstant(constant, includeScalar, variablesResolver, variablesSource),
                UnaryExpression unary => GetQueryNode(unary.Operand, owner, includeScalar, variablesResolver, variablesSource),
                LambdaExpression lambda => GetQueryNode(lambda.Body, owner, includeScalar, variablesResolver, variablesSource),
                MemberExpression member => GetQueryNodeFromMember(member, owner, includeScalar),
                _ => throw new NotImplementedException()
            };
        }

        private static (GqlQueryNode root, GqlQueryNode head) GetQueryNodeFromConstant(ConstantExpression constant, bool includeScalar, VariablesResolver variablesResolver, object variablesSource)
        {
            if (constant.Value is GqlQueryable qq)
            {
                if (qq.QueryName is null)
                    throw new NotImplementedException();

                var qqq = (IQueryable)qq;
                var provider = (GqlQueryProvider)qqq.Provider;
                var gType = GetGraphType(provider.Schema, qqq.ElementType);
                var field = provider.Schema.Query.Fields.FirstOrDefault(_ => _.Name.Equals(qq.QueryName, StringComparison.OrdinalIgnoreCase));

                if (field.ResolvedType != gType &&
                    field.ResolvedType is IProvideResolvedType nn && nn.ResolvedType != gType)
                {
                    throw new InvalidOperationException();
                }

                switch (gType)
                {
                    case IComplexGraphType complexGraphType:
                        {
                            var node = GetQueryNodeForComplexType(field, includeScalar, variablesResolver, variablesSource);
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

        private static (GqlQueryNode root, GqlQueryNode head) GetQueryNodeFromMethodCall(MethodCallExpression methodCall, IComplexGraphType owner, bool includeScalar, VariablesResolver variablesResolver, object variablesSource)
        {
            if (methodCall.Method.IsGenericMethod)
            {
                if (methodCall.Method.GetGenericMethodDefinition() == select.Value ||
                    methodCall.Method.GetGenericMethodDefinition() == selectMany.Value)
                {
                    var node = GetQueryNode(methodCall.Arguments[0], owner, false, variablesResolver, variablesSource);
                    var subNode = GetQueryNode(methodCall.Arguments[1], (IComplexGraphType)node.head.GraphType, includeScalar, variablesResolver, variablesSource);
                    node.head.Children.Add(subNode.root);
                    return (node.root, subNode.head);
                }
                else if (methodCall.Method.GetGenericMethodDefinition() == whereParams.Value)
                {
                    var innerVariablesSource = ((ConstantExpression)methodCall.Arguments[1]).Value;
                    var node = GetQueryNode(methodCall.Arguments[0], owner, true, variablesResolver, innerVariablesSource);
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

            var node = GetQueryNodeForComplexType(field, includeScalar, null, null);
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

        private static GqlQueryNode GetQueryNodeForComplexType(FieldType field, bool includeScalarFields, VariablesResolver variablesResolver, object variablesSource)
        {
            var complexGraphType =
                field.ResolvedType as IComplexGraphType
                ?? (field.ResolvedType as IProvideResolvedType)?.ResolvedType as IComplexGraphType
                ?? throw new NotImplementedException();

            var node = new GqlQueryNode(field, variablesResolver, variablesSource);

            if (includeScalarFields)
            {
                var scalarFields = complexGraphType.Fields.Where(_ =>
                    _.ResolvedType is ScalarGraphType ||
                    _.ResolvedType is NonNullGraphType nn && nn.ResolvedType is ScalarGraphType);

                foreach (var childField in scalarFields)
                    node.Children.Add(new GqlQueryNode(childField, variablesResolver, variablesSource));
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
    }
}
