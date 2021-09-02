using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using GraphQL.Types;
using GraphQL.Utilities;
using Moq;
using Ugpa.GraphQL.Linq.Utils;
using Xunit;

namespace Ugpa.GraphQL.Linq.Tests
{
    public class GqlQueryBuilderTest
    {
        private readonly IGraphTypeNameMapper mapper;

        public GqlQueryBuilderTest()
        {
            var mapperMock = new Mock<IGraphTypeNameMapper>();
            mapperMock.Setup(_ => _.GetTypeName(It.IsAny<Type>())).Returns((Type t) => t.Name);
            mapper = mapperMock.Object;
        }

        [Fact]
        public void DeepNestingFieldTypeQueryTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                type Product {
                    name: String!
                }
                type Query {
                   products: [[[[Product!]!]!]!]!
                }");

            var query = new Product[0].AsQueryable();

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out _);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("query { products { name } }", queryText);
        }

        [Fact]
        public void SimpleQueryTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                type ProductInfo {
                    version: String!
                }
                type Product {
                    id: ID
                    name: String!
                    comment: String!
                    productInfo: ProductInfo!
                }
                type Query {
                    products: [Product]
                }");

            var query = new Product[0].AsQueryable();

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out var entryPoint);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("products", entryPoint);
            Assert.Equal("query { products { id name comment } }", queryText);
        }

        [Fact]
        public void SingleSelectQueryTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                type About {
                    developer: String
                }
                type ProductInfo {
                    title: String!
                    version: String!
                    about: About!
                }
                type Product {
                    id: Int!
                    productInfo: ProductInfo!
                }
                type Query {
                    products: [Product]
                }");

            var query = new Product[0]
                .AsQueryable()
                .Select(_ => _.ProductInfo);

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out var entryPoint);

            queryText = PostProcessQuery(queryText);

            Assert.Equal("products", entryPoint);
            Assert.Equal("query { products { productInfo { title version } } }", queryText);
        }

        [Fact]
        public void NestedPropertySelectQueryTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                type About {
                    stamp: String!
                }
                type ProductInfo {
                    title: String!
                    about: About!
                }
                type Product {
                    id: Int!
                    productInfo: ProductInfo!
                }
                type Query {
                    products: [Product]
                }");

            var query = new Product[0]
                .AsQueryable()
                .Select(_ => _.ProductInfo.About);

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out var entryPoint);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("products", entryPoint);
            Assert.Equal("query { products { productInfo { about { stamp } } } }", queryText);
        }

        [Fact]
        public void ChainedSelectQueryTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                type About {
                    stamp: String!
                }
                type ProductInfo {
                    title: String!
                    about: About!
                }
                type Product {
                    id: Int!
                    productInfo: ProductInfo!
                }
                type Query {
                    products: [Product]
                }");

            var query = new Product[0]
                .AsQueryable()
                .Select(_ => _.ProductInfo)
                .Select(_ => _.About);

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out var entryPoint);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("products", entryPoint);
            Assert.Equal("query { products { productInfo { about { stamp } } } }", queryText);
        }

        [Fact]
        public void SelectManyQueryTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                type Schema {
                    id: ID
                    name: String!
                }
                type Product {
                    id: Int!
                    schemas: [Schema]
                }
                type Query {
                    products: [Product]
                }");

            var query = new Product[0]
                .AsQueryable()
                .SelectMany(_ => _.Schemas);

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out var entryPoint);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("products", entryPoint);
            Assert.Equal("query { products { schemas { id name } } }", queryText);
        }

        [Fact]
        public void SingleIncludeQueryTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                type ProductInfo {
                    title: String!
                    version: String!
                }
                type Product {
                    id: ID
                    name: String!
                    comment: String
                    productInfo: ProductInfo!
                }
                type Query {
                    products: [Product]
                }");

            var query = new Product[0]
                .AsQueryable()
                .Include(_ => _.ProductInfo);

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out var entryPoint);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("products", entryPoint);
            Assert.Equal("query { products { id name comment productInfo { title version } } }", queryText);
        }

        [Fact]
        public void NestedIncludeQueryTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                type About {
                    stamp: String!
                }
                type ProductInfo {
                    comment: String!
                    about: About!
                }
                type Product {
                    id: ID
                    name: String!
                    comment: String
                    productInfo: ProductInfo!
                }
                type Query {
                    products: [Product]
                }");

            var query = new Product[0]
                .AsQueryable()
                .Include(_ => _.ProductInfo.About);

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out var entryPoint);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("products", entryPoint);
            Assert.Equal("query { products { id name comment productInfo { about { stamp } } } }", queryText);
        }

        [Fact]
        public void MultipleIncludesQueryTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                type About {
                    stamp: String!
                }
                type ProductInfo {
                    title: String!
                    version: String!
                    about: About!
                }
                type Product {
                    id: ID
                    name: String!
                    comment: String
                    productInfo: ProductInfo!
                }
                type Query {
                    products: [Product]
                }");

            var query = new Product[0]
                .AsQueryable()
                .Include(_ => _.ProductInfo)
                .Include(_ => _.ProductInfo.About);

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out var entryPoint);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("products", entryPoint);
            Assert.Equal("query { products { id name comment productInfo { title version about { stamp } } } }", queryText);
        }

        [Fact]
        public void IncludeAfterSelectTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                type Product {
                    id: Int!
                    productInfo: ProductInfo
                }
                type ProductInfo {
                    description: String!
                    about: ProductAbout
                }
                type ProductAbout {
                    code: String!
                }
                type Query {
                    products: [Product]
                }");

            var query = new Product[0]
                .AsQueryable()
                .Select(_ => _.ProductInfo)
                .Include(_ => _.About)
                .Include(_ => _.About);

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out _);
            queryText = PostProcessQuery(queryText);

            Assert.Equal(
                "query { products { productInfo { description about { code } } } }",
                queryText);
        }

        [Fact]
        public void InheritanceViolationForNonAbstractBaseClassIncludeTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                interface Module {
                    id: ID
                }
                type Module2 implements Module {
                    id: ID
                }
                type Module3 implements Module {
                    id: ID
                }
                type Query {
                    modules: [Module2]
                }",
                cfg => cfg.Types.For("Module").ResolveType = _ => throw new NotSupportedException());

            var query = new Module2[0]
                .AsQueryable()
                .Include(_ => ((Module3)_).Channels);

            Assert.Throws<InvalidOperationException>(() => queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out _));
        }

        [Fact]
        public void InheritanceViolationForAbstractBaseClassIncludeTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                interface Module {
                    id: ID
                }
                type Module2 {
                    id: ID
                }
                type Query {
                    modules: [Module]
                }",
                cfg => cfg.Types.For("Module").ResolveType = _ => throw new NotSupportedException());

            var query = new Module[0]
                .AsQueryable()
                .Include(_ => ((Module2)_).Channels);

            Assert.Throws<InvalidOperationException>(() => queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out _));
        }

        [Fact]
        public void SimpleParametrizedQueryTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                type DrawSchema {
                    id: ID
                    name: String!
                }
                type Query {
                    schemas(productId: Int!): [DrawSchema]
                }");

            var query = new DrawSchema[0]
                .AsQueryable()
                .Where(new { productId = 111 });

            var variablesResolver = new VariablesResolver();
            var queryText = queryBuilder.BuildQuery(query.Expression, variablesResolver, out var entryPoint);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("schemas", entryPoint);
            Assert.Equal("query($linq_param_0: Int!) { schemas(productId: $linq_param_0) { id name } }", queryText);

            var variable = Assert.Single(variablesResolver.GetAllVariables());
            Assert.Equal("linq_param_0", variable.Name);
            Assert.Equal(111, variable.Value);
        }

        [Fact]
        public void ParametrizedSelectQueryTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                type ProductInfo {
                    title: String!
                    version: String!
                }
                type Product {
                    id: ID
                    productInfo: ProductInfo!
                }
                type Query {
                    products: [Product]
                    product(productId: Int!): Product
                }");

            var query = new Product[0]
                .AsQueryable()
                .Where(new { productId = 111 })
                .Select(_ => _.ProductInfo);

            var variablesResolver = new VariablesResolver();
            var queryText = queryBuilder.BuildQuery(query.Expression, variablesResolver, out var entryPoint);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("product", entryPoint);
            Assert.Equal("query($linq_param_0: Int!) { product(productId: $linq_param_0) { productInfo { title version } } }", queryText);

            var variable = Assert.Single(variablesResolver.GetAllVariables());
            Assert.Equal("linq_param_0", variable.Name);
            Assert.Equal(111, variable.Value);
        }

        [Fact]
        public void InterfaceImplementationQueryTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                interface TypeTemplate {
                    id: ID
                    name: String!
                }
                type TextTemplate implements TypeTemplate {
                    id: ID
                    name: String!
                    fontFamily: String!
                    fontSize: Int!            
                }
                type RailchainTemplate implements TypeTemplate {
                    id: ID
                    name: String!
                    mainLineWidth: Int!
                    sideLineWidth: Int!
                }
                type TextboxFrame {
                    borderWidth: Int!
                }
                type TextboxTemplate implements TypeTemplate {
                    id: ID
                    name: String!
                    frame: TextboxFrame!
                }
                type Query {
                    templates: [TypeTemplate]
                }",
                cfg =>
                {
                    cfg.Types.For("TypeTemplate").ResolveType = _ => throw new NotImplementedException();
                });

            var query = new TypeTemplate[0].AsQueryable();

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out var entryPoint);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("templates", entryPoint);
            Assert.Equal(
                "query { templates { __typename id name " +
                "... on TextTemplate { fontFamily fontSize } " +
                "... on RailchainTemplate { mainLineWidth sideLineWidth } " +
                "} }",
                queryText);
        }

        [Fact]
        public void IncludeSubtypeFieldQueryWithConvertTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                interface TypeTemplate {
                    id: ID
                    name: String!
                }
                type TextTemplate implements TypeTemplate {
                    id: ID
                    name: String!
                    fontFamily: String!
                    fontSize: Int!
                }
                type RailchainTemplate implements TypeTemplate {
                    id: ID
                    name: String!
                    mainLineWidth: Int!
                    sideLineWidth: Int!
                }
                type TextboxFrame {
                    borderWidth: Int!
                }
                type TextboxTemplate implements TypeTemplate {
                    id: ID
                    name: String!
                    frame: TextboxFrame!
                }
                type Query {
                    templates: [TypeTemplate]
                }",
                cfg =>
                {
                    cfg.Types.For("TypeTemplate").ResolveType = _ => throw new NotImplementedException();
                });

            var query = new TypeTemplate[0]
                .AsQueryable()
                .Include(_ => ((TextboxTemplate)_).Frame);

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out var entryPoint);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("templates", entryPoint);

            Assert.Equal(
                "query { templates { __typename id name " +
                "... on TextTemplate { fontFamily fontSize } " +
                "... on RailchainTemplate { mainLineWidth sideLineWidth } " +
                "... on TextboxTemplate { frame { borderWidth } } " +
                "} }",
                queryText);
        }

        [Fact]
        public void IncludeSubtypeFieldQueryWithTypeAsTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                interface TypeTemplate {
                    id: ID
                    name: String!
                }
                type TextTemplate implements TypeTemplate {
                    id: ID
                    name: String!
                    fontFamily: String!
                    fontSize: Int!
                }
                type RailchainTemplate implements TypeTemplate {
                    id: ID
                    name: String!
                    mainLineWidth: Int!
                    sideLineWidth: Int!
                }
                type TextboxFrame {
                    borderWidth: Int!
                }
                type TextboxTemplate implements TypeTemplate {
                    id: ID
                    name: String!
                    frame: TextboxFrame!
                }
                type Query {
                    templates: [TypeTemplate]
                }",
                cfg =>
                {
                    cfg.Types.For("TypeTemplate").ResolveType = _ => throw new NotImplementedException();
                });

            var query = new TypeTemplate[0]
                .AsQueryable()
                .Include(_ => (_ as TextboxTemplate).Frame);

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out var entryPoint);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("templates", entryPoint);

            Assert.Equal(
                "query { templates { __typename id name " +
                "... on TextTemplate { fontFamily fontSize } " +
                "... on RailchainTemplate { mainLineWidth sideLineWidth } " +
                "... on TextboxTemplate { frame { borderWidth } } " +
                "} }",
                queryText);
        }

        [Fact]
        public void NestedCollectionIncludeTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                type DrawSchemaInfo {
                    author: String!
                }
                type DrawSchema {
                    id: Int!
                    info: DrawSchemaInfo!
                }
                type Product {
                    id: Int!
                    schemas: [DrawSchema]
                }
                type Query {
                    products: [Product]
                }");

            var query = new Product[0]
                .AsQueryable()
                .Include(p => p.Schemas.Include(s => s.Info));

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out _);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("query { products { id schemas { id info { author } } } }", queryText);
        }

        [Fact]
        public void NestedCollectionCastIncludeTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                type DrawSchemaInfo {
                    author: String!
                }
                interface DrawSchema {
                    id: Int!
                    info: DrawSchemaInfo!
                }
                type SimpleDrawSchema implements DrawSchema {
                    id: Int!
                    info: DrawSchemaInfo!
                }
                type ExtendedDrawSchema implements DrawSchema {
                    id: Int!
                    info: DrawSchemaInfo!
                    extendedInfo: DrawSchemaInfo!
                }
                type Product {
                    id: Int!
                    schemas: [DrawSchema]
                }
                type Query {
                    products: [Product]
                }",
                cfg =>
                {
                    cfg.Types.For("DrawSchema").ResolveType = _ => throw new NotImplementedException();
                });

            var query = new Product[0]
                .AsQueryable()
                .Include(p => p.Schemas.Include(s => s.Info))
                .Include(p => p.Schemas.Include(s => ((ExtendedDrawSchema)s).ExtendedInfo));

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out _);
            queryText = PostProcessQuery(queryText);

            Assert.Equal(
                @"query { products { id schemas { __typename id info { author } ... on ExtendedDrawSchema { extendedInfo { author } } } } }",
                queryText);
        }

        [Fact]
        public void NestedReferenceCollectionIncludeTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                type Module {
                    name: String!
                    child: Module
                    submodules: [Module]
                }
                type Query {
                    modules: [Module]
                }");

            var query = new Module[0]
                .AsQueryable()
                .Include(m0 => m0.Child.Submodules.Include(m1 => m1.Child));

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out _);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("query { modules { name child { submodules { name child { name } } } } }", queryText);
        }

        [Fact]
        public void ScalarCollectionFetchTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                type Product {
                    remarks: [String!]!
                }
                type Query {
                    products: [Product]
                }");

            var query = new Product[0].AsQueryable();
            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out _);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("query { products { remarks } }", queryText);
        }

        [Fact]
        public void MultipleInterfacesImplementationTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                interface DataFlowSystemItem {
                    id: ID
                }
                interface Module {
                    name: String!
                }
                type ModuleA implements DataFlowSystemItem & Module {
                    id: ID
                    name: String!
                    boolValue: Boolean!
                }
                type ModuleB implements DataFlowSystemItem & Module {
                    id: ID
                    name: String!
                    intValue: Int!
                }
                type Query {
                    items: [DataFlowSystemItem]
                    modules: [Module]
                }",
                cfg =>
                {
                    cfg.Types.For("DataFlowSystemItem").ResolveType = _ => throw new NotSupportedException();
                    cfg.Types.For("Module").ResolveType = _ => throw new NotSupportedException();
                });

            var query1 = new DataFlowSystemItem[0].AsQueryable();
            var queryText1 = queryBuilder.BuildQuery(query1.Expression, new VariablesResolver(), out _);
            queryText1 = PostProcessQuery(queryText1);

            Assert.Equal("query { items { __typename id ... on Module { name } ... on ModuleA { boolValue } ... on ModuleB { intValue } } }", queryText1);

            var query2 = new Module[0].AsQueryable();
            var queryText2 = queryBuilder.BuildQuery(query2.Expression, new VariablesResolver(), out _);
            queryText2 = PostProcessQuery(queryText2);

            Assert.Equal("query { modules { __typename name ... on DataFlowSystemItem { id } ... on ModuleA { boolValue } ... on ModuleB { intValue } } }", queryText2);
        }

        [Fact]
        public void UnionTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                type Module2 {
                    name: String!
                }
                type Module3 {
                    name: String!
                }
                union Module = Module2 | Module3
                type Query {
                    modules: [Module]
                }",
                cfg => cfg.Types.For("Module").ResolveType = _ => throw new NotImplementedException());

            var query = new Module[0].AsQueryable();
            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out _);
            queryText = PostProcessQuery(queryText);

            Assert.Equal(
                "query { modules { __typename ... on Module2 { name } ... on Module3 { name } } }",
                queryText);
        }

        [Fact]
        public void IncludeFieldOfUnionTypeTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                type Module2 {
                    name: String!
                }
                type Module3 {
                    name: String!
                }
                union DataFlowSystemItem = Module2 | Module3
                type DataFlowSystem {
                    items: [DataFlowSystemItem]
                }
                type Query {
                    systems: [DataFlowSystem]
                }",
                cfg => cfg.Types.For("DataFlowSystemItem").ResolveType = _ => throw new NotImplementedException());

            var query = new DataFlowSystem[0]
                .AsQueryable()
                .Include(_ => _.Items);

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out _);
            queryText = PostProcessQuery(queryText);

            Assert.Equal(
                "query { systems { items { __typename ... on Module2 { name } ... on Module3 { name } } } }",
                queryText);
        }

        [Fact]
        public void IncludeUnionFieldTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                type Ref {
                    data: String!
                }
                type Module2 {
                    id: Int!
                    ref: Ref
                }
                type Module3 {
                    name: String!
                    ref: Ref
                }
                union Module = Module2 | Module3
                type Query {
                    modules: [Module]
                }",
                cfg => cfg.Types.For("Module").ResolveType = _ => throw new NotImplementedException());

            var query = new Module[0]
                .AsQueryable()
                .Include(_ => ((Module2)_).Ref)
                .Include(_ => ((Module3)_).Ref);

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out _);
            queryText = PostProcessQuery(queryText);

            Assert.Equal(
                "query { modules { __typename ... on Module2 { id ref { data } } ... on Module3 { name ref { data } } } }",
                queryText);
        }

        [Fact]
        public void IncludeUnionSubtypeFieldTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                type Module2 {
                    name: String!
                }
                type Module3 {
                    name: String!
                    channels: ChannelGroup
                }
                type ChannelGroup {
                    id: ID
                }
                union Module = Module2 | Module3
                type Query {
                    modules: [Module]
                }",
                cfg => cfg.Types.For("Module").ResolveType = _ => throw new NotImplementedException());

            var query = new Module[0]
                .AsQueryable()
                .Include(_ => ((Module3)_).Channels);

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out _);
            queryText = PostProcessQuery(queryText);

            Assert.Equal(
                "query { modules { __typename ... on Module2 { name } ... on Module3 { name channels { id } } } }",
                queryText);
        }

        [Fact]
        public void RecursiveQueryTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                type Module {
                    name: String!
                    submodules: [Module]
                }
                type Query {
                    modules: [Module]
                }");

            var query = new Module[0]
                .AsQueryable()
                .Include(m => m.Submodules.Include(mm => mm.Submodules.Include(mmm => mmm.Submodules)));

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out _);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("query { modules { name submodules { name submodules { name submodules { name } } } } }", queryText);
        }

        [Fact]
        public void MultipleRecursiveQueryTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                type Module {
                    name: String!
                    submodules: [Module]
                }
                type Query {
                    modules: [Module]
                }");

            var query = new Module[0]
                .AsQueryable()
                .Include(m => m.Submodules)
                .Include(m => m.Submodules.Include(mm => mm.Submodules))
                .Include(m => m.Submodules.Include(mm => mm.Submodules.Include(mmm => mmm.Submodules)));

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out _);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("query { modules { name submodules { name submodules { name submodules { name } } } } }", queryText);
        }

        [Fact]
        public void RecursiveQueryWithCastTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                interface DataFlowSystemItem {
                    name: String!
                }
                type Module implements DataFlowSystemItem {
                    name: String!
                    submodules: [Module]
                }
                type Query {
                    items: [DataFlowSystemItem]
                }",
                cfg => cfg.Types.For("DataFlowSystemItem").ResolveType = _ => throw new NotSupportedException());

            var query = new DataFlowSystemItem[0]
                .AsQueryable()
                .Include(i => ((Module)i).Submodules.Include(m => m.Submodules));

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out _);
            queryText = PostProcessQuery(queryText);

            Assert.Equal(
                "query { items { __typename name ... on Module { submodules { name submodules { name } } } } }",
                queryText);
        }

        [Fact]
        public void SimpleFragmentUsageTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                type Module {
                    name: String!
                }
                type Query {
                    modules: [Module]
                }");

            var query = new Module[0]
                .AsQueryable()
                .UsingFragment((IQueryable<Module> fullModule) => fullModule);

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out _);
            queryText = PostProcessQuery(queryText);

            Assert.Equal(
                "query { modules { ... fullModule } } fragment fullModule on Module { name }",
                queryText);
        }

        [Fact]
        public void FragmentWithSubtypesUsageTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                interface Module {
                    name: String!
                }
                type ModuleA implements Module {
                    name: String!
                    a: Int!
                }
                type ModuleB implements Module {
                    name: String!
                    b: Int!
                }
                type Query {
                    modules: [Module]
                }",
                cfg => cfg.Types.For("Module").ResolveType = _ => throw new NotSupportedException());

            var query = new Module[0].AsQueryable()
                .UsingFragment((IQueryable<Module> fullModule) => fullModule);

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out _);
            queryText = PostProcessQuery(queryText);

            Assert.Equal(
                "query { modules { ... fullModule } } fragment fullModule on Module { __typename name ... on ModuleA { a } ... on ModuleB { b } }",
                queryText);
        }

        [Fact]
        public void FragmentRecursiveUsageTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                type Module {
                    name: String!
                    submodules: [Module]
                }
                type Query {
                    modules: [Module]
                }");

            var query = new Module[0].AsQueryable()
                .Include(_ => _.Submodules)
                .UsingFragment((IQueryable<Module> fullModule) => fullModule);

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out _);
            queryText = PostProcessQuery(queryText);

            Assert.Equal(
                "query { modules { ... fullModule submodules { ... fullModule } } } fragment fullModule on Module { name }",
                queryText);
        }

        [Fact]
        public void RecursiveFragmentTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                type Module {
                    name: String!
                    child: Module
                }
                type Query {
                    modules: [Module]
                }");

            var query = new Module[0]
                .AsQueryable()
                .UsingFragment((IQueryable<Module> fullModule) => fullModule.Include(_ => _.Child));

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out _);
            queryText = PostProcessQuery(queryText);

            Assert.Equal(
                "query { modules { ... fullModule } } fragment fullModule on Module { name child { name } }",
                queryText);
        }

        [Fact]
        public void FragmentUsageOnNestedFieldTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                type Module {
                    name: String!
                    child: Module
                }
                type ModuleCollection {
                    modules: [Module]
                }
                type Query {
                    modules: ModuleCollection!
                }");

            var query = new ModuleCollection[0]
                .AsQueryable()
                .Include(_ => _.Modules)
                .UsingFragment((IQueryable<Module> fullModule) => fullModule);

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out _);
            queryText = PostProcessQuery(queryText);

            Assert.Equal(
                "query { modules { modules { ... fullModule } } } fragment fullModule on Module { name }",
                queryText);
        }

        [Fact]
        public void ExternalFragmentBuildingTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                type Module {
                    name: String!
                    child: Module
                }
                type ModuleCollection {
                    modules: [Module]
                }
                type Query {
                    modules: ModuleCollection!
                }");

            IQueryable<Module> CreateFragment(IQueryable<Module> modules)
            {
                return modules.Include(_ => _.Child);
            }

            var query = new ModuleCollection[0]
                .AsQueryable()
                .Include(_ => _.Modules)
                .UsingFragment<ModuleCollection, Module>("fullModule", CreateFragment);

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out _);
            queryText = PostProcessQuery(queryText);

            Assert.Equal(
                "query { modules { modules { ... fullModule } } } fragment fullModule on Module { name child { name } }",
                queryText);
        }

        [Fact]
        public void FragmentUsageWithMultipleInterfacesImplementationTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                interface DataFlowSystemItem {
                    id: ID
                }
                interface Module {
                    name: String!
                }
                type ModuleA implements DataFlowSystemItem & Module {
                    id: ID
                    name: String!
                    a: Int!
                }
                type ModuleB implements DataFlowSystemItem & Module {
                    id: ID
                    name: String!
                    b: Int!
                }
                type Query {
                    items: [DataFlowSystemItem]
                }",
                cfg =>
                {
                    cfg.Types.For("DataFlowSystemItem").ResolveType = _ => throw new NotSupportedException();
                    cfg.Types.For("Module").ResolveType = _ => throw new NotSupportedException();
                });

            var query = new DataFlowSystemItem[0]
                .AsQueryable()
                .UsingFragment((IQueryable<Module> fullModule) => fullModule);

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out _);
            queryText = PostProcessQuery(queryText);

            Assert.Equal(
                "query { items { __typename id ... on Module { ... fullModule } ... on ModuleA { a } ... on ModuleB { b } } } " +
                "fragment fullModule on Module { __typename name ... on DataFlowSystemItem { id } ... on ModuleA { a } ... on ModuleB { b } }",
                queryText);
        }


        [Fact]
        public void FragmentUsageWithMultipleInterfacesImplementationAndRecursionTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                interface DataFlowSystemItem {
                    id: ID
                }
                interface Module {
                    name: String!
                    submodules: [Module]
                }
                type ModuleA implements DataFlowSystemItem & Module {
                    id: ID
                    name: String!
                    a: Int!
                    submodules: [Module]
                }
                type ModuleB implements DataFlowSystemItem & Module {
                    id: ID
                    name: String!
                    b: Int!
                    submodules: [Module]
                }
                type Query {
                    items: [DataFlowSystemItem]
                }",
                cfg =>
                {
                    cfg.Types.For("DataFlowSystemItem").ResolveType = _ => throw new NotSupportedException();
                    cfg.Types.For("Module").ResolveType = _ => throw new NotSupportedException();
                });

            var query = new DataFlowSystemItem[0]
                .AsQueryable()
                .Include(i => ((Module)i).Submodules.Include(m0 => m0.Submodules))
                .UsingFragment((IQueryable<Module> fullModule) => fullModule);

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out _);
            queryText = PostProcessQuery(queryText);

            Assert.Equal(
                "query { items { __typename id ... on Module { ... fullModule submodules { ... fullModule submodules { ... fullModule } } } ... on ModuleA { a } ... on ModuleB { b } } } " +
                "fragment fullModule on Module { __typename name ... on DataFlowSystemItem { id } ... on ModuleA { a } ... on ModuleB { b } }",
                queryText);
        }

        [Fact]
        public void MultipleInterfaceImplementationWithIncludeTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                interface DataFlowSystemItem {
                    id: ID
                }
                interface Module {
                    name: String!
                }
                type Module2 implements DataFlowSystemItem & Module {
                    id: ID
                    name: String!
                    channels: ChannelGroup
                }
                type ChannelGroup {
                    id: ID
                    groups: [ChannelGroup]
                }
                type Query {
                    items: [DataFlowSystemItem]
                }",
                cfg =>
                {
                    cfg.Types.For("DataFlowSystemItem").ResolveType = _ => throw new NotSupportedException();
                    cfg.Types.For("Module").ResolveType = _ => throw new NotSupportedException();
                });

            var query = new DataFlowSystemItem[0]
                .AsQueryable()
                .Include(i => ((Module2)i).Channels)
                .Include(i => ((Module2)i).Channels.Groups.Include(c => c.Groups));

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out _);
            queryText = PostProcessQuery(queryText);

            Assert.Equal(
                "query { items { __typename id ... on Module { name } ... on Module2 { channels { id groups { id groups { id } } } } } }",
                queryText);
        }

        private GqlQueryBuilder GetQueryBuilder(string typeDefinitions, Action<SchemaBuilder> configure = null)
        {
            var mapperMock = new Mock<IMemberNameMapper>();
            mapperMock.Setup(_ => _.GetFieldName(It.IsAny<MemberInfo>())).Returns<MemberInfo>(m => m.Name);
            return new GqlQueryBuilder(Schema.For(typeDefinitions, configure), mapper, mapperMock.Object);
        }

        private string PostProcessQuery(string query)
        {
            query = Regex.Replace(query, @"{", " { ");
            query = Regex.Replace(query, @"}", " } ");
            query = Regex.Replace(query, @"{{", "{ {");
            query = Regex.Replace(query, @"}}", "} }");
            query = Regex.Replace(query, @"\r", " ");
            query = Regex.Replace(query, @"\n", " ");
            query = Regex.Replace(query, @"\s\s+", " ");
            return query.Trim();
        }

        private class Product
        {
            public int Id { get; }

            public ProductInfo ProductInfo { get; }

            public IEnumerable<DrawSchema> Schemas { get; }

            public IEnumerable<string> Developers { get; }
        }

        private class ProductInfo
        {
            public ProductAbout About { get; }
        }

        private class ProductAbout
        {
        }

        private class DrawSchema
        {
            public DrawSchemaInfo Info { get; }
        }

        private class DrawSchemaInfo
        {
        }

        private class ExtendedDrawSchema : DrawSchema
        {
            public DrawSchemaInfo ExtendedInfo { get; }
        }

        private class TypeTemplate
        {
        }

        private class TextboxTemplate : TypeTemplate
        {
            public TextboxFrame Frame { get; }
        }

        private class TextboxFrame
        {
        }

        private abstract class DataFlowSystem
        {
            public DataFlowSystemItem[] Items { get; }
        }

        private abstract class DataFlowSystemItem
        {
        }

        private abstract class ModuleCollection
        {
            public Module[] Modules { get; }
        }

        private abstract class Module : DataFlowSystemItem
        {
            public Module[] Submodules { get; }

            public Module Child { get; }

            public object Ref { get; }
        }

        private abstract class Module2 : Module
        {
            public ChannelGroup Channels { get; }
        }

        private abstract class Module3 : Module2
        {
        }

        private abstract class ChannelGroup
        {
            public ChannelGroup[] Groups { get; }
        }
    }
}
