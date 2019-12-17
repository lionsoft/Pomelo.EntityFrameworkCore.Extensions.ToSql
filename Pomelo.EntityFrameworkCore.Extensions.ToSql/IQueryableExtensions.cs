using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

// ReSharper disable once CheckNamespace
namespace Microsoft.EntityFrameworkCore
{
    public static class IQueryableExtensions
    {
        public static class ReflectionCommon
        {
            public static readonly FieldInfo QueryCompilerOfEntityQueryProvider = typeof(EntityQueryProvider).GetTypeInfo().DeclaredFields.First(x => x.Name == "_queryCompiler");
            public static readonly FieldInfo DatabaseOfQueryCompiler = typeof(QueryCompiler).GetTypeInfo().DeclaredFields.First(x => x.Name == "_database");
            public static readonly PropertyInfo DependenciesOfDatabase = typeof(Database).GetTypeInfo().DeclaredProperties.First(x => x.Name == "Dependencies");
            public static readonly FieldInfo DependenciesOfQueryCompilerContextFactory = typeof(QueryCompilationContextFactory).GetTypeInfo().DeclaredFields.Single(x => x.Name == "_dependencies");
        }

        private static object Private(this object obj, string privateField) => obj?.GetType().GetField(privateField, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(obj);
        private static T Private<T>(this object obj, string privateField) => (T)obj?.GetType().GetField(privateField, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(obj);

        public static TService GetService<TService>(this IQueryable self)
        {
            TService res;
            if (self.GetType().GetTypeInfo().GetGenericTypeDefinition() == typeof(EntityQueryable<>))
            {
                var queryCompiler = (QueryCompiler)ReflectionCommon.QueryCompilerOfEntityQueryProvider.GetValue(self.Provider);  // (self.Provider as EntityQueryProvider)._queryCompiler: QueryCompiler
                var database = (RelationalDatabase)ReflectionCommon.DatabaseOfQueryCompiler.GetValue(queryCompiler);             // QueryCompiler._database: Database
                var dbDependencies = (DatabaseDependencies)ReflectionCommon.DependenciesOfDatabase.GetValue(database);           // Database.Dependencies: DatabaseDependencies
                var qccf = dbDependencies.QueryCompilationContextFactory;                                                        // Database.Dependencies.QueryCompilationContextFactory
                var qccfDependencies = (QueryCompilationContextDependencies)ReflectionCommon.DependenciesOfQueryCompilerContextFactory.GetValue(qccf); // QueryCompilationContextFactory._dependencies: QueryCompilationContextDependencies
                var context = qccfDependencies.CurrentContext.Context; // DbContext
                res = context.GetService<TService>();
            }
            else if (self.GetType().GetTypeInfo().GetGenericTypeDefinition() == typeof(InternalDbSet<>))
            {
                var context = Private<DbContext>(self, "_context");
                res = context.GetService<TService>();
            }
            else
            {
                throw new NotSupportedException(self.GetType().Name);
            }
            return res;
        }

        public static string ToSql<TEntity>(this IQueryable<TEntity> query) where TEntity : class
        {
            using (var enumerator = query.Provider.Execute<IEnumerable<TEntity>>(query.Expression).GetEnumerator())
            {
                var relationalCommandCache = enumerator.Private("_relationalCommandCache");
                var selectExpression = relationalCommandCache.Private<SelectExpression>("_selectExpression");
                var factory = relationalCommandCache.Private<IQuerySqlGeneratorFactory>("_querySqlGeneratorFactory");
                
                var sqlGenerator = factory.Create();
                var command = sqlGenerator.GetCommand(selectExpression);

                return command.CommandText;
            }
        }
    }
}
