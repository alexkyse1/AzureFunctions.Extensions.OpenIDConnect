﻿namespace AzureFunctions.Extensions.OpenIDConnect
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.Azure.WebJobs;


    public delegate IEnumerable<Type> FunctionTypeCrawler();

    public class RouteGuardian : IRouteGuardian
    {
        private readonly Dictionary<string, AuthorizeAttribute> _routeProtection;

        public static Func<IEnumerable<Type>> AppDomainTypeCrawler = () =>
        {
            return AppDomain.CurrentDomain
                            .GetAssemblies()
                            .Where(a => !a.IsDynamic)
                            .SelectMany(x => x.GetTypes());
        };

        public RouteGuardian(FunctionTypeCrawler typeCrawler)
        {
            bool IsAzureFunction(MethodInfo methodInfo) => methodInfo.GetCustomAttributes<FunctionNameAttribute>().Any();
            bool IsHttpTrigger(MethodInfo methodInfo) => methodInfo.GetParameters().Any(paramInfo => paramInfo.GetCustomAttributes<HttpTriggerAttribute>().Any());

            var httpTriggerMethods = typeCrawler().SelectMany(type => type.GetMethods())
                .Where(methodInfo => methodInfo.IsPublic && IsAzureFunction(methodInfo) && IsHttpTrigger(methodInfo));

            var infos = httpTriggerMethods.Select(methodInfo =>
            {
                var httpTriggerAttribute = methodInfo.GetParameters()
                                                     .SelectMany(paramInfo => paramInfo.GetCustomAttributes<HttpTriggerAttribute>())
                                                     .First();

                var functionNameAttribute = methodInfo.GetCustomAttributes<FunctionNameAttribute>().First();

                var authorizeAttributeOnType = methodInfo.DeclaringType?.GetCustomAttributes<AuthorizeAttribute>().FirstOrDefault();
                var authorizeAttributeOnMethod = methodInfo.GetCustomAttributes<AuthorizeAttribute>().FirstOrDefault();
                var anonymousAttributeOnMethod = methodInfo.GetCustomAttributes<AllowAnonymousAttribute>().FirstOrDefault();
                
                return new AzureFunctionInfo
                {
                    FunctionName = functionNameAttribute.Name,
                    AuthorizeAttribute = anonymousAttributeOnMethod != null ? null : authorizeAttributeOnMethod ?? authorizeAttributeOnType,
                    Route = httpTriggerAttribute.Route
                };
            });

            _routeProtection = infos.Where(x => x.AuthorizeAttribute != null)
                                    .ToDictionary(x => x.FunctionName, x => x.AuthorizeAttribute);
        }

        public Task<bool> ShouldAuthorize(string functionName)
        {
            return Task.FromResult(_routeProtection.ContainsKey(functionName));
        }

        private class AzureFunctionInfo
        {
            public string FunctionName { get; set; }
            public AuthorizeAttribute AuthorizeAttribute { get; set; }
            public string Route { get; set; }
        }
    }
    
}