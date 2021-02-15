﻿using System;
using Microsoft.Extensions.DependencyInjection;

namespace AzureFunctions.Extensions.OpenIDConnect.Configuration
{
    public static class ServicesConfigurationExtensions
    {
        public static void AddOpenIDConnect(this IServiceCollection services, string issuer, string audience)
        {
            Action<ConfigurationBuilder> configurator = builder =>
            {
                builder.SetTokenValidation(TokenValidationParametersHelpers.Default(audience, issuer));

                builder.SetIssuerBaseUrlConfiguration(issuer);
            };

            AddOpenIDConnect(services, configurator);
        }

        public static void AddOpenIDConnect(this IServiceCollection services, Action<ConfigurationBuilder> configurator)
        {
            if (configurator == null)
            {
                throw new ArgumentNullException(nameof(configurator));
            }

            var builder = new ConfigurationBuilder(services);
            configurator(builder);

            if (!builder.IsValid)
            {
                throw new ArgumentException("Be sure to configure Token Validation and Configuration Manager");
            }


            // These are created as a singletons, so that only one instance of each
            // is created for the lifetime of the hosting Azure Function App.
            // That helps reduce the number of calls to the authorization service
            // for the signing keys and other stuff that can be used across multiple
            // calls to the HTTP triggered Azure Functions.

            services.AddSingleton<IAuthorizationHeaderBearerTokenExtractor, AuthorizationHeaderBearerTokenExtractor>();
            services.AddSingleton<IJwtSecurityTokenHandlerWrapper, JwtSecurityTokenHandlerWrapper>();
            services.AddSingleton<IOidcConfigurationManager, OidcConfigurationManager>();
            services.AddSingleton<IApiAuthentication, ApiAuthenticationService>();
        }
    }
}