using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using GraphQL.Types;
using Ugpa.GraphQL.Linq.Properties;

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
            => ((Func<IQueryable<int>, Expression<Func<int, object>>, IQueryable<int>>)QueryableExtensions.Include).Method.GetGenericMethodDefinition());

        private static readonly Lazy<MethodInfo> includeEnum = new Lazy<MethodInfo>(()
            => ((Func<IEnumerable<int>, Func<int, object>, IEnumerable<int>>)QueryableExtensions.Include).Method.GetGenericMethodDefinition());

        private static readonly Lazy<MethodInfo> usingFragment = new Lazy<MethodInfo>(()
            => ((Func<IQueryable<int>, Expression<Func<IQueryable<int>, object>>, IQueryable<int>>)QueryableExtensions.UsingFragment).Method.GetGenericMethodDefinition());

        private readonly ISchema schema;
        private readonly IGraphTypeNameMapper graphTypeNameMapper;
        private readonly IMemberNameMapper memberNameMapper;

        public GqlQueryBuilder(ISchema schema, IGraphTypeNameMapper graphTypeNameMapper, IMemberNameMapper memberNameMapper)
        {
            this.schema = schema ?? throw new ArgumentNullException(nameof(schema));
            this.graphTypeNameMapper = graphTypeNameMapper ?? throw new ArgumentNullException(nameof(graphTypeNameMapper));
            this.memberNameMapper = memberNameMapper ?? throw new ArgumentNullException(nameof(memberNameMapper));
        }

        public string BuildQuery(Expression expression, VariablesResolver variablesResolver, out string entryPoint)
        {
            var (root, _) = GetQueryNode(expression, null, true, variablesResolver, null);
            root.Prune();
            entryPoint = root.Name;
            var query = root.ToQueryString();
            return query;
        }

        private (GqlQueryNode Root, GqlQueryNode Head) GetQueryNode(Expression expression, IComplexGraphType? owner, bool includeScalar, VariablesResolver variablesResolver, object? variablesSource)
        {
            return expression switch
            {
                MethodCallExpression methodCall => GetQueryNodeFromMethodCall(methodCall, owner, includeScalar, variablesResolver, variablesSource),
                ConstantExpression constant => GetQueryNodeFromConstant(constant, includeScalar, variablesResolver, variablesSource),
                UnaryExpression unary => unary.NodeType switch
                {
                    ExpressionType.Quote => GetQueryNode(unary.Operand, owner, includeScalar, variablesResolver, variablesSource),
                    _ => throw new NotImplementedException()
                },
                LambdaExpression lambda => GetQueryNode(lambda.Body, owner, includeScalar, variablesResolver, variablesSource),
                MemberExpression member => GetQueryNodeFromMember(member, owner!, includeScalar, variablesResolver),
                ParameterExpression parameter => GetQueryNodeFromParameter(parameter, includeScalar, variablesResolver, variablesSource),
                _ => throw new NotImplementedException()
            };
        }

        private (GqlQueryNode Root, GqlQueryNode Head) GetQueryNodeFromConstant(ConstantExpression constant, bool includeScalar, VariablesResolver variablesResolver, object? variablesSource)
        {
            if (constant.Value is IQueryable queryable)
            {
                var gType = GetGraphType(queryable.ElementType);

                switch (gType)
                {
                    case IComplexGraphType:
                        {
                            var field = GetBestFitQueryField(gType, variablesSource);
                            var node = GetQueryNodeForComplexType(field, GqlQueryNode.NodeType.Field, includeScalar, variablesResolver, variablesSource);
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

        private (GqlQueryNode Root, GqlQueryNode Head) GetQueryNodeFromParameter(ParameterExpression parameter, bool includeScalar, VariablesResolver variablesResolver, object? variablesSource)
        {
            if (!parameter.Type.IsGenericType)
                throw new NotImplementedException();

            if (parameter.Type.GenericTypeArguments.Length != 1)
                throw new NotImplementedException();

            if (parameter.Type.GetGenericTypeDefinition() != typeof(IQueryable<>))
                throw new NotImplementedException();

            var gType = GetGraphType(parameter.Type.GenericTypeArguments[0]);

            switch (gType)
            {
                case IComplexGraphType complexGraphType:
                    {
                        var node = GetQueryNodeForComplexType(
                            parameter.Name,
                            GqlQueryNode.NodeType.Fragment,
                            complexGraphType,
                            Enumerable.Empty<QueryArgument>(),
                            includeScalar,
                            variablesResolver,
                            variablesSource);

                        return (node, node);
                    }
                default:
                    {
                        throw new NotImplementedException();
                    }
            }
        }

        private (GqlQueryNode Root, GqlQueryNode Head) GetQueryNodeFromMethodCall(MethodCallExpression methodCall, IComplexGraphType? owner, bool includeScalar, VariablesResolver variablesResolver, object? variablesSource)
        {
            if (methodCall.Method.IsGenericMethod)
            {
                var methodDefinition = methodCall.Method.GetGenericMethodDefinition();

                if (methodDefinition == select.Value || methodDefinition == selectMany.Value)
                {
                    var node = GetQueryNode(methodCall.Arguments[0], owner, false, variablesResolver, variablesSource);
                    var subNode = GetQueryNode(methodCall.Arguments[1], (IComplexGraphType)node.Head.GraphType, includeScalar, variablesResolver, variablesSource);
                    node.Head.Children.Add(subNode.Root);
                    return (node.Root, subNode.Head);
                }
                else if (methodDefinition == include.Value || methodDefinition == includeEnum.Value)
                {
                    var node = GetQueryNode(methodCall.Arguments[0], owner, true, variablesResolver, variablesSource);
                    var subNode = GetQueryNode(methodCall.Arguments[1], (IComplexGraphType)node.Head.GraphType, includeScalar, variablesResolver, variablesSource);
                    node.Head.Children.Add(subNode.Root);
                    return (node.Root, node.Root);
                }
                else if (methodDefinition == whereParams.Value)
                {
                    var innerVariablesSource = ((ConstantExpression)methodCall.Arguments[1]).Value;
                    var node = GetQueryNode(methodCall.Arguments[0], owner, includeScalar, variablesResolver, innerVariablesSource);
                    return node;
                }
                else if (methodDefinition == usingFragment.Value)
                {
                    var node = GetQueryNode(methodCall.Arguments[0], null, true, variablesResolver, variablesSource);
                    var fragment = GetQueryNode(methodCall.Arguments[1], null, true, variablesResolver, variablesSource);
                    node.Root.Children.Add(fragment.Root);
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

        private (GqlQueryNode Root, GqlQueryNode Head) GetQueryNodeFromMember(MemberExpression member, IComplexGraphType owner, bool includeScalar, VariablesResolver variablesResolver)
        {
            if (owner is null)
                throw new ArgumentNullException(nameof(owner));

            GqlQueryNode GetQueryNodeFromCurrentMember(IComplexGraphType ownerType, GqlQueryNode.NodeType nodeType)
            {
                var fieldName = memberNameMapper.GetFieldName(member.Member);
                var field = ownerType.Fields.FirstOrDefault(_ => _.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException(string.Format(
                        Resources.GqlQueryBuilder_MemberIsMissingOrNotMapped,
                        member.Member.Name,
                        member.Member.DeclaringType,
                        ownerType.Name));

                return GetQueryNodeForComplexType(field, nodeType, includeScalar, variablesResolver, null);
            }

            if (member.Expression is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
            {
                if (owner is not IAbstractGraphType)
                    throw new InvalidOperationException(string.Format(Resources.GqlQueryBuilder_NotAbstractType, owner.Name));

                var subType = (IComplexGraphType)GetGraphType(unary.Type);

                if (subType is not IAbstractGraphType && !((IAbstractGraphType)owner).PossibleTypes.Contains(subType))
                    throw new InvalidOperationException(string.Format(Resources.GqlQueryBuilder_NotPosibleDerivedType, subType.Name, owner.Name));

                var node = GetQueryNodeForComplexType(string.Empty, GqlQueryNode.NodeType.Subtype, subType, Enumerable.Empty<QueryArgument>(), false, variablesResolver, null);
                var subNode = GetQueryNodeFromCurrentMember(subType, GqlQueryNode.NodeType.Field);
                node.Children.Add(subNode);
                return (node, subNode);
            }
            else if (member.Expression is MemberExpression nestedMember)
            {
                var nestedNode = GetQueryNodeFromMember(nestedMember, owner, false, variablesResolver);
                var node = GetQueryNodeFromCurrentMember((IComplexGraphType)nestedNode.Head.GraphType, GqlQueryNode.NodeType.Field);
                nestedNode.Head.Children.Add(node);
                return (nestedNode.Root, node);
            }
            else
            {
                var node = GetQueryNodeFromCurrentMember(owner, GqlQueryNode.NodeType.Field);
                return (node, node);
            }
        }

        private GqlQueryNode GetQueryNodeForComplexType(FieldType field, GqlQueryNode.NodeType nodeType, bool includeScalarFields, VariablesResolver variablesResolver, object? variablesSource)
        {
            var complexGraphType =
                field.ResolvedType as IComplexGraphType
                ?? (field.ResolvedType as IProvideResolvedType)?.ResolvedType as IComplexGraphType
                ?? throw new InvalidOperationException(string.Format(Resources.GqlQueryBuilder_FieldTypeIsNotComplexType, field.Name));

            return GetQueryNodeForComplexType(field.Name, nodeType, complexGraphType, field.Arguments, includeScalarFields, variablesResolver, variablesSource);
        }

        private GqlQueryNode GetQueryNodeForComplexType(string name, GqlQueryNode.NodeType nodeType, IComplexGraphType complexGraphType, IEnumerable<QueryArgument> arguments, bool includeScalarFields, VariablesResolver variablesResolver, object? variablesSource)
        {
            var node = new GqlQueryNode(name, nodeType, complexGraphType, arguments, variablesResolver, variablesSource);

            if (complexGraphType is IAbstractGraphType && nodeType != GqlQueryNode.NodeType.Subtype)
            {
                node.Children.Add(new GqlQueryNode(
                    "__typename",
                    GqlQueryNode.NodeType.Field,
                    new NonNullGraphType(new StringGraphType()),
                    Enumerable.Empty<QueryArgument>(),
                    variablesResolver,
                    variablesSource));
            }

            if (includeScalarFields)
            {
                var scalarFields = complexGraphType.Fields.Where(_ =>
                    _.ResolvedType is ScalarGraphType ||
                    _.ResolvedType is IProvideResolvedType r && r.ResolvedType is ScalarGraphType);

                if (complexGraphType is IImplementInterfaces implementInterfaces && nodeType == GqlQueryNode.NodeType.Subtype)
                {
                    var interfaceFields = implementInterfaces.ResolvedInterfaces
                        .SelectMany(_ => _.Fields)
                        .Join(scalarFields, _ => _.Name, _ => _.Name, (_, f) => f)
                        .ToArray();

                    scalarFields = scalarFields.Except(interfaceFields);
                }

                foreach (var childField in scalarFields)
                    node.Children.Add(GqlQueryNode.FromField(childField, variablesResolver, variablesSource));
            }

            if (complexGraphType is IAbstractGraphType abstractGraphType && nodeType != GqlQueryNode.NodeType.Subtype)
            {
                foreach (var interfaceType in abstractGraphType.PossibleTypes.SelectMany(_ => _.ResolvedInterfaces).Where(_ => _ != abstractGraphType).Distinct())
                {
                    node.Children.Add(GetQueryNodeForComplexType(
                        string.Empty,
                        GqlQueryNode.NodeType.Subtype,
                        interfaceType,
                        Enumerable.Empty<QueryArgument>(),
                        includeScalarFields,
                        variablesResolver,
                        variablesSource));
                }

                foreach (var posibleType in abstractGraphType.PossibleTypes)
                {
                    node.Children.Add(GetQueryNodeForComplexType(
                        string.Empty,
                        GqlQueryNode.NodeType.Subtype,
                        posibleType,
                        Enumerable.Empty<QueryArgument>(),
                        includeScalarFields,
                        variablesResolver,
                        variablesSource));
                }
            }

            return node;
        }

        private IGraphType GetGraphType(Type clrType)
        {
            var gTypeName = graphTypeNameMapper.GetTypeName(clrType);
            return schema.AllTypes.FirstOrDefault(_ => _.Name == gTypeName)
                ?? throw new InvalidOperationException(string.Format(Resources.GraphTypeMapper_TypeNotDefined, gTypeName));
        }

        private FieldType GetBestFitQueryField(IGraphType graphType, object? variablesSource)
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
