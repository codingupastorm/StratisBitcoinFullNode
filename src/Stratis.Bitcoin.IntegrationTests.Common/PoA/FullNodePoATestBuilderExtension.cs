﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Stratis.Core.Builder;
using Stratis.Core.Builder.Feature;
using Stratis.Features.PoA;

namespace Stratis.Bitcoin.IntegrationTests.Common.PoA
{
    public static class FullNodePoATestBuilderExtension
    {
        public static IFullNodeBuilder AddFastMiningCapability(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                foreach (IFeatureRegistration feature in features.FeatureRegistrations)
                {
                    feature.FeatureServices(services =>
                    {
                        services.Replace(new ServiceDescriptor(typeof(IPoAMiner), typeof(TestPoAMiner), ServiceLifetime.Singleton));
                    });
                }
            });

            return fullNodeBuilder;
        }

        public static IFullNodeBuilder AddTokenlessFastMiningCapability(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                foreach (IFeatureRegistration feature in features.FeatureRegistrations)
                {
                    feature.FeatureServices(services =>
                    {
                        services.Replace(new ServiceDescriptor(typeof(IPoAMiner), typeof(TokenlessTestPoAMiner), ServiceLifetime.Singleton));
                    });
                }
            });

            return fullNodeBuilder;
        }
    }
}