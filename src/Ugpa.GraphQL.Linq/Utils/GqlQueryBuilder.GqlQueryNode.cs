using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GraphQL.Types;

namespace Ugpa.GraphQL.Linq.Utils
{
    partial class GqlQueryBuilder
    {
        private class GqlQueryNode
        {
            private readonly NodeType type;
            private readonly IEnumerable<QueryArgument> arguments;
            private readonly VariablesResolver variablesResolver;
            private readonly object? variableValuesSource;

            public GqlQueryNode(string name, NodeType type, IGraphType graphType, IEnumerable<QueryArgument> arguments, VariablesResolver variablesResolver, object? variableValuesSource)
            {
                if (arguments.Any() && variableValuesSource is null)
                    throw new InvalidOperationException();

                this.type = type;
                this.arguments = arguments;
                this.variablesResolver = variablesResolver;
                this.variableValuesSource = variableValuesSource;

                Name = name;
                GraphType = graphType;
            }

            public enum NodeType
            {
                Field,
                Subtype,
                Fragment
            }

            public string Name { get; }

            public IGraphType GraphType { get; }

            public ICollection<GqlQueryNode> Children { get; } = new List<GqlQueryNode>();

            public static GqlQueryNode FromField(FieldType field, VariablesResolver variablesResolver, object? variableValuesSource)
            {
                var graphType = field.ResolvedType is IProvideResolvedType provideResolvedType
                    ? provideResolvedType.ResolvedType
                    : field.ResolvedType;

                return new GqlQueryNode(field.Name, NodeType.Field, graphType, field.Arguments, variablesResolver, variableValuesSource);
            }

            public void Prune()
            {
                Prune(Children.Where(_ => _.type == NodeType.Fragment).ToArray());
            }

            private void Prune(IEnumerable<GqlQueryNode> fragments)
            {
                // Merging complex type fields.
                foreach (var group in Children.Where(_ => _.type == NodeType.Field).GroupBy(_ => _.Name).Where(_ => _.Count() > 1))
                {
                    var main = group.First();
                    foreach (var other in group.Skip(1))
                    {
                        if (!Equals(main.GraphType, other.GraphType))
                            throw new InvalidOperationException();

                        foreach (var child in other.Children)
                            main.Children.Add(child);

                        Children.Remove(other);
                    }
                }

                // Removing base type fields from subtypes.
                foreach (var child in Children.Where(_ => _.type == NodeType.Subtype))
                {
                    foreach (var field in child.Children.Join(Children, _ => _.Name, _ => _.Name, (f, c) => f).ToArray())
                        child.Children.Remove(field);
                }

                // Merging same subtype nodes.
                foreach (var group in Children.Where(_ => _.type == NodeType.Subtype).GroupBy(_ => _.GraphType).Where(_ => _.Count() > 1))
                {
                    var main = group.First();
                    foreach (var other in group.Skip(1))
                    {
                        foreach (var child in other.Children)
                            main.Children.Add(child);

                        Children.Remove(other);
                    }
                }

                // Removing empty nodes.
                foreach (var child in Children.ToArray())
                {
                    child.Prune(fragments);
                    if (child.GraphType is IComplexGraphType && !child.Children.Any())
                    {
                        Children.Remove(child);
                    }
                }

                // Applying fragments.
                if (fragments.FirstOrDefault(_ => _.GraphType == GraphType) is GqlQueryNode fragment && this != fragment)
                {
                    if (!Children.Contains(fragment))
                        Children.Add(fragment);

                    foreach (var child in Children.Join(fragment.Children, _ => _.Name, _ => _.Name, (c, fc) => c).ToArray())
                        Children.Remove(child);
                }
            }

            public string ToQueryString()
            {
                var fragments = Children.Where(_ => _.type == NodeType.Fragment).ToArray();

                var subBuilder = new StringBuilder();
                ToQueryString(subBuilder, string.Empty, fragments);

                var builder = new StringBuilder();
                builder.Append("query");

                var vars = variablesResolver.GetAllVariables().ToArray();
                if (vars.Any())
                {
                    var args = vars.Select(_ => $"${_.Name}: {_.Type}").ToArray();
                    var argsString = string.Join(", ", args);
                    builder.Append($"({argsString})");
                }

                builder.AppendLine(" {");
                builder.Append(subBuilder.ToString());
                builder.AppendLine("}");

                foreach (var fragment in Children.Where(_ => _.type == NodeType.Fragment))
                    fragment.ToQueryString(builder, string.Empty, fragments);

                return builder.ToString();
            }

            public override string ToString()
            {
                var builder = new StringBuilder($"[{type}]: {Name}");

                if (arguments.Any())
                    builder.Append($"({string.Join(", ", arguments.Select(_ => _.Name))})");

                if (Children.Any())
                {
                    builder.Append(" {");
                    builder.Append(string.Join(" ", Children.Select(_ => _.Name)));
                    builder.Append(" }");
                }

                return builder.ToString();
            }

            private void ToQueryString(StringBuilder queryBuilder, string indent, IEnumerable<GqlQueryNode> fragments)
            {
                if (type == NodeType.Fragment)
                {
                    queryBuilder.Append($"{indent}fragment {Name} on {GraphType.Name}");
                }
                else
                {
                    indent += "  ";
                    queryBuilder.Append($"{indent}{Name}");
                }

                if (arguments.Any())
                {
                    queryBuilder.Append("(");

                    var args = arguments
                        .Select(argument =>
                        {
                            var varName = variablesResolver.GetArgumentVariableName(argument, variableValuesSource!);
                            return $"{argument.Name}: ${varName}";
                        })
                        .ToArray();

                    queryBuilder.Append(string.Join(", ", args));

                    queryBuilder.Append(")");
                }

                queryBuilder.AppendLine(" {");

                if (fragments.FirstOrDefault(_ => _ != this && _.GraphType == GraphType) is GqlQueryNode fragment)
                    queryBuilder.AppendLine($"{indent}  ... {fragment.Name}");

                foreach (var child in Children.Where(_ => _.type == NodeType.Field))
                {
                    if (child.Children.Any())
                        child.ToQueryString(queryBuilder, indent, fragments);
                    else
                        queryBuilder.AppendLine($"{indent}  {child.Name}");
                }

                foreach (var child in Children.Where(_ => _.type == NodeType.Subtype))
                {
                    queryBuilder.Append($"{indent}  ... on {child.GraphType.Name}");
                    child.ToQueryString(queryBuilder, indent, fragments);
                }

                queryBuilder.AppendLine($"{indent}}}");
            }
        }
    }
}
