using System;
using System.Linq;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Reflection;

// ReSharper disable once CheckNamespace
namespace Microsoft.EntityFrameworkCore
{
    internal static class GetServiceExtensions
    {
        public static TService GetService<TService>(this IQueryable self)
        {
            if (self.GetType().GetTypeInfo().GetGenericTypeDefinition() == typeof(EntityQueryable<>))
            {
                var queryCompiler = (QueryCompiler)IQueryableExtensions.ReflectionCommon.QueryCompilerOfEntityQueryProvider.GetValue(self.Provider);
                var database = (RelationalDatabase)IQueryableExtensions.ReflectionCommon.DatabaseOfQueryCompiler.GetValue(queryCompiler);
                var dbDependencies = (DatabaseDependencies)IQueryableExtensions.ReflectionCommon.DependenciesOfDatabase.GetValue(database);
                var qccf = dbDependencies.QueryCompilationContextFactory;
                var qccfDependencies = (QueryCompilationContextDependencies)IQueryableExtensions.ReflectionCommon.DependenciesOfQueryCompilerContextFactory.GetValue(qccf);
                var context = qccfDependencies.CurrentContext.Context;
                return context.GetService<TService>();
            }
            else if (self.GetType().GetTypeInfo().GetGenericTypeDefinition() == typeof(InternalDbSet<>))
            {
                var context = (DbContext)self.GetType().GetTypeInfo().DeclaredFields.Single(x => x.Name == "_context").GetValue(self);
                return context.GetService<TService>();
            }
            else
            {
                throw new NotSupportedException(self.GetType().Name);
            }
        }
    }
}
