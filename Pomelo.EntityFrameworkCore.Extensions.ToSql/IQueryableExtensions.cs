using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Remotion.Linq;

// ReSharper disable once CheckNamespace
namespace Microsoft.EntityFrameworkCore
{
    public static class IQueryableExtensions
    {
        public static string ToSql<TEntity>(this IQueryable<TEntity> self)
        {
            var visitor = self.CompileQuery();
            return string.Join("", visitor.Queries.Select(x => x.ToString().TrimEnd().TrimEnd(';') + ";" + Environment.NewLine));
        }

        public static IEnumerable<string> ToUnevaluated<TEntity>(this IQueryable<TEntity> self)
        {
            var visitor = self.CompileQuery();
            return VisitExpression(visitor.Expression, null);
        }

        internal static IEnumerable<string> VisitExpression(Expression expression, dynamic caller)
        {
            var ret = new List<string>();
            dynamic exp = expression;

            if (expression.NodeType == ExpressionType.Lambda)
            {
                var resultBuilder = new StringBuilder();
                if (caller != null)
                {
                    resultBuilder.Append(caller.Method.Name.Replace("_", "."));
                    resultBuilder.Append("(");
                }
                resultBuilder.Append(exp.ToString());

                if (caller != null)
                {
                    resultBuilder.Append(")");
                }
                ret.Add(resultBuilder.ToString());
            }

            try
            {
                if (exp.Arguments.Count > 0)
                {
                    foreach (var x in exp.Arguments)
                    {
                        ret.AddRange(VisitExpression(x, exp));
                    }
                }
            }
            catch
            { }

            return ret;
        }

        public static class ReflectionCommon
        {
            public static readonly FieldInfo QueryCompilerOfEntityQueryProvider = typeof(EntityQueryProvider).GetTypeInfo().DeclaredFields.First(x => x.Name == "_queryCompiler");
            public static readonly PropertyInfo DatabaseOfQueryCompiler = typeof(QueryCompiler).GetTypeInfo().DeclaredProperties.First(x => x.Name == "Database");
            public static readonly PropertyInfo DependenciesOfDatabase = typeof(Database).GetTypeInfo().DeclaredProperties.First(x => x.Name == "Dependencies");
            public static readonly PropertyInfo DependenciesOfQueryCompilerContextFactory = typeof(QueryCompilationContextFactory).GetTypeInfo().DeclaredProperties.Single(x => x.Name == "Dependencies");
        }

        public static RelationalQueryModelVisitor CompileQuery<TEntity>(this IQueryable<TEntity> self)
        {
            var q = self as EntityQueryable<TEntity>;
            if (q == null)
            {
                return null;
            }

            var queryCompiler = (QueryCompiler)ReflectionCommon.QueryCompilerOfEntityQueryProvider.GetValue(self.Provider);
            var database = (Database)ReflectionCommon.DatabaseOfQueryCompiler.GetValue(queryCompiler);
            var dependencies = (DatabaseDependencies)ReflectionCommon.DependenciesOfDatabase.GetValue(database);
            var factory = dependencies.QueryCompilationContextFactory;
            var queryModel = self.GetService<IQueryModelGenerator>().ParseQuery(self.Expression);
            var modelVisitor = (RelationalQueryModelVisitor)database.CreateVisitor(factory, queryModel);
            modelVisitor.CreateQueryExecutor<TEntity>(queryModel);
            return modelVisitor;
        }


        public static EntityQueryModelVisitor CreateVisitor(this Database self, IQueryCompilationContextFactory factory, QueryModel qm)
        {
            return factory.Create(async: false).CreateQueryModelVisitor();
        }
    }
}
