using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using GraphQL.Types;

namespace Ugpa.GraphQL.Linq.Utils
{
    internal partial class GqlQueryBuilder
    {
        private static readonly Lazy<MethodInfo> select = new Lazy<MethodInfo>(()
            => ((Func<IQueryable<int>, Expression<Func<int, int>>, IQueryable<int>>)Queryable.Select).Method.GetGenericMethodDefinition());

        private static readonly Lazy<MethodInfo> selectMany = new Lazy<MethodInfo>(()
            => ((Func<IQueryable<int>, Expression<Func<int, IEnumerable<int>>>, IQueryable<int>>)Queryable.SelectMany).Method.GetGenericMethodDefinition());

        private static readonly Lazy<MethodInfo> whereParams = new Lazy<MethodInfo>(()
            => ((Func<IQueryable<int>, int, IQueryable<int>>)QueryableExtensions.Where).Method.GetGenericMethodDefinition());

        private static readonly Lazy<MethodInfo> include = new Lazy<MethodInfo>(()
            => ((Func<IQueryable<int>, Expression<Func<int, int>>, IQueryable<int>>)QueryableExtensions.Include).Method.GetGenericMethodDefinition());

        private readonly ISchema schema;
        private readonly IGraphTypeNameMapper graphTypeNameMapper;

        public GqlQueryBuilder(ISchema schema, IGraphTypeNameMapper graphTypeNameMapper)
        {
            this.schema = schema ?? throw new ArgumentNullException(nameof(schema));
            this.graphTypeNameMapper = graphTypeNameMapper ?? throw new ArgumentNullException(nameof(graphTypeNameMapper));
        }

        public string BuildQuery(Expression expression, VariablesResolver variablesResolver, out string entryPoint)
        {
            var (root, _) = GetQueryNode(expression, null, true, variablesResolver, null);
            root.Prune();
            entryPoint = root.Name;
            var query = root.ToQueryString();
            return query;
        }

        private (GqlQueryNode root, GqlQueryNode head) GetQueryNode(Expression expression, IComplexGraphType owner, bool includeScalar, VariablesResolver variablesResolver, object variablesSource)
        {
            return expression switch
            {
                MethodCallExpression methodCall => GetQueryNodeFromMethodCall(methodCall, owner, includeScalar, variablesResolver, variablesSource),
                ConstantExpression constant => GetQueryNodeFromConstant(constant, includeScalar, variablesResolver, variablesSource),
                UnaryExpression unary => GetQueryNode(unary.Operand, owner, includeScalar, variablesResolver, variablesSource),
                LambdaExpression lambda => GetQueryNode(lambda.Body, owner, includeScalar, variablesResolver, variablesSource),
                MemberExpression member => GetQueryNodeFromMember(member, owner, includeScalar, variablesResolver),
                _ => throw new NotImplementedException()
            };
        }

        private (GqlQueryNode root, GqlQueryNode head) GetQueryNodeFromConstant(ConstantExpression constant, bool includeScalar, VariablesResolver variablesResolver, object variablesSource)
        {
            if (constant.Value.GetType() is var qType && qType.IsGenericType && qType.GetGenericTypeDefinition() == typeof(GqlQueryable<>))
            {
                var queryable = (IQueryable)constant.Value;
                var provider = (GqlQueryProvider)queryable.Provider;
                var gTypeName = graphTypeNameMapper.GetTypeName(queryable.ElementType);
                var gType = schema.AllTypes.FirstOrDefault(_ => _.Name == gTypeName) ?? throw new InvalidOperationException();

                switch (gType)
                {
                    case IComplexGraphType complexGraphType:
                        {
                            var field = GetBestFitQueryField(gType, variablesSource);
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

        private (GqlQueryNode root, GqlQueryNode head) GetQueryNodeFromMethodCall(MethodCallExpression methodCall, IComplexGraphType owner, bool includeScalar, VariablesResolver variablesResolver, object variablesSource)
        {
            if (methodCall.Method.IsGenericMethod)
            {
                var methodDefinition = methodCall.Method.GetGenericMethodDefinition();

                if (methodDefinition == select.Value || methodDefinition == selectMany.Value)
                {
                    var node = GetQueryNode(methodCall.Arguments[0], owner, false, variablesResolver, variablesSource);
                    var subNode = GetQueryNode(methodCall.Arguments[1], (IComplexGraphType)node.head.GraphType, includeScalar, variablesResolver, variablesSource);
                    node.head.Children.Add(subNode.root);
                    return (node.root, subNode.head);
                }
                else if (methodDefinition == include.Value)
                {
                    var node = GetQueryNode(methodCall.Arguments[0], owner, true, variablesResolver, variablesSource);
                    var subNode = GetQueryNode(methodCall.Arguments[1], (IComplexGraphType)node.root.GraphType, includeScalar, variablesResolver, variablesSource);
                    node.root.Children.Add(subNode.root);
                    return (node.root, subNode.head);
                }
                else if (methodDefinition == whereParams.Value)
                {
                    var innerVariablesSource = ((ConstantExpression)methodCall.Arguments[1]).Value;
                    var node = GetQueryNode(methodCall.Arguments[0], owner, includeScalar, variablesResolver, innerVariablesSource);
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

        private (GqlQueryNode root, GqlQueryNode head) GetQueryNodeFromMember(MemberExpression member, IComplexGraphType owner, bool includeScalar, VariablesResolver variablesResolver)
        {
            GqlQueryNode root = null;
            if (member.Expression is MemberExpression nestedMember)
            {
                var nestedNode = GetQueryNodeFromMember(nestedMember, owner, false, variablesResolver);
                root = nestedNode.root;
                owner = (IComplexGraphType)nestedNode.head.GraphType;
            }

            var field = owner.Fields.FirstOrDefault(_ => _.Name.Equals(member.Member.Name, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException();

            var node = GetQueryNodeForComplexType(field, includeScalar, variablesResolver, null);
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

        private GqlQueryNode GetQueryNodeForComplexType(FieldType field, bool includeScalarFields, VariablesResolver variablesResolver, object variablesSource)
        {
            var complexGraphType =
                field.ResolvedType as IComplexGraphType
                ?? (field.ResolvedType as IProvideResolvedType)?.ResolvedType as IComplexGraphType
                ?? throw new NotImplementedException();

            return GetQueryNodeForComplexType(field.Name, complexGraphType, field.Arguments, includeScalarFields, variablesResolver, variablesSource);
        }

        private GqlQueryNode GetQueryNodeForComplexType(string name, IComplexGraphType complexGraphType, IEnumerable<QueryArgument> arguments, bool includeScalarFields, VariablesResolver variablesResolver, object variablesSource)
        {
            var node = new GqlQueryNode(name, complexGraphType, arguments, variablesResolver, variablesSource);

            if (includeScalarFields)
            {
                var scalarFields = complexGraphType.Fields.Where(_ =>
                    _.ResolvedType is ScalarGraphType ||
                    _.ResolvedType is NonNullGraphType nn && nn.ResolvedType is ScalarGraphType);

                foreach (var childField in scalarFields)
                    node.Children.Add(GqlQueryNode.FromField(childField, variablesResolver, variablesSource));
            }

            if (complexGraphType is IAbstractGraphType abstractGraphType)
            {
                foreach (var posibleType in abstractGraphType.PossibleTypes)
                {
                    node.PosibleTypes.Add(GetQueryNodeForComplexType(
                        string.Empty,
                        posibleType,
                        Enumerable.Empty<QueryArgument>(),
                        includeScalarFields,
                        variablesResolver,
                        variablesSource));
                }
            }

            return node;
        }

        private FieldType GetBestFitQueryField(IGraphType graphType, object variablesSource)
        {
            var fields = schema.Query.Fields
                .Where(_ =>
                    _.ResolvedType == graphType ||
                    _.ResolvedType is IProvideResolvedType prt && prt.ResolvedType == graphType)
                .ToArray();

            if (!fields.Any())
                throw new InvalidOperationException();

            if (variablesSource is null)
            {
                return fields.Single(_ => !_.Arguments.Any());
            }
            else
            {
                if (variablesSource is IDictionary<string, object>)
                {
                    throw new NotImplementedException();
                }
                else
                {
                    var props = variablesSource.GetType().GetProperties();
                    return fields
                        .Single(_ =>
                            _.Arguments.Count == props.Length &&
                            _.Arguments.All(arg => props.Any(p => p.Name == arg.Name)));
                }
            }
        }
    }
}

