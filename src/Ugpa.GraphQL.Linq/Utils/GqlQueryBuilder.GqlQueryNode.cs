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
                Subtype
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
                // Сливаем поля комплексных типов в один узел.
                foreach (var group in Children.Where(_ => _.type == NodeType.Field).GroupBy(_ => _.Name).Where(_ => _.Count() > 1))
                {
                    var main = group.First();
                    foreach (var other in group.Skip(1))
                    {
                        if (!Equals(main.GraphType, other.GraphType))
                            throw new System.InvalidOperationException();

                        foreach (var child in other.Children)
                            main.Children.Add(child);

                        Children.Remove(other);
                    }
                }

                // Удаляем из подтипов поля базового типа.
                foreach (var child in Children.Where(_ => _.type == NodeType.Subtype))
                {
                    foreach (var field in child.Children.Join(Children, _ => _.Name, _ => _.Name, (f, c) => f).ToArray())
                        child.Children.Remove(field);
                }

                // Сливаем узлы с одинаковым подтипом.
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

                // Удаляем пустые узлы комплексного типа.
                foreach (var child in Children.ToArray())
                {
                    child.Prune();
                    if (child.GraphType is IComplexGraphType && !child.Children.Any())
                    {
                        Children.Remove(child);
                    }
                }
            }

            public string ToQueryString()
            {
                var subBuilder = new StringBuilder();
                ToQueryString(subBuilder, string.Empty);

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
                return builder.ToString();
            }

            public override string ToString()
            {
                var builder = new StringBuilder(Name);

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

            private void ToQueryString(StringBuilder queryBuilder, string indent)
            {
                indent += "  ";
                queryBuilder.Append($"{indent}{Name}");

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

                foreach (var child in Children.Where(_ => _.type == NodeType.Field))
                {
                    if (child.Children.Any())
                        child.ToQueryString(queryBuilder, indent);
                    else
                        queryBuilder.AppendLine($"{indent}  {child.Name}");
                }

                foreach (var child in Children.Where(_ => _.type == NodeType.Subtype))
                {
                    queryBuilder.Append($"{indent}  ... on {child.GraphType.Name}");
                    child.ToQueryString(queryBuilder, indent);
                }

                queryBuilder.AppendLine($"{indent}}}");
            }
        }
    }
}
