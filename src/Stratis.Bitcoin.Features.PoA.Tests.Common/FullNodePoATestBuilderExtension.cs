﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;

namespace Stratis.Features.PoA.Tests.Common
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