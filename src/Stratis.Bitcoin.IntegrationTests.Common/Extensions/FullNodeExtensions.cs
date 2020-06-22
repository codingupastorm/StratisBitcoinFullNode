using System;
using System.Linq;
using System.Reflection;
using NBitcoin;
using Stratis.Core;
using Stratis.Core.Base;
using Stratis.Core.Consensus;
using Stratis.Core.Interfaces;
using Stratis.Features.MemoryPool;

namespace Stratis.Bitcoin.IntegrationTests.Common
{
    public static class FullNodeExtensions
    {
        public static IConsensusManager ConsensusManager(this FullNode fullNode)
        {
            return fullNode.NodeService<IConsensusManager>() as IConsensusManager;
        }

        public static ICoinView CoinView(this FullNode fullNode)
        {
            return fullNode.NodeService<ICoinView>();
        }

        public static MempoolManager MempoolManager(this FullNode fullNode)
        {
            return fullNode.NodeService<MempoolManager>();
        }

        public static IBlockStore BlockStore(this FullNode fullNode)
        {
            return fullNode.NodeService<IBlockStore>();
        }

        public static ChainedHeader GetBlockStoreTip(this FullNode fullNode)
        {
            return fullNode.NodeService<IChainState>().BlockStoreTip;
        }

        /// <summary>
        /// Create an instance of the controller type using DI.
        /// </summary>
        /// <typeparam name="T">Class of type.</typeparam>
        /// <param name="fullNode">The fullnode instance.</param>
        /// <param name="failWithDefault">Set to true to return null instead of throwing an error.</param>
        /// <returns>A controller instance.</returns>
        public static T NodeController<T>(this IFullNode fullNode, bool failWithDefault = false)
        {
            foreach (ConstructorInfo ci in typeof(T).GetConstructors())
            {
                ParameterInfo[] paramInfo = ci.GetParameters();

                var parameters = new object[paramInfo.Length];

                for (int i = 0; i < parameters.Length; i++)
                {
                    parameters[i] = fullNode.Services.ServiceProvider.GetService(paramInfo[i].ParameterType) ?? paramInfo[i].DefaultValue;
                    if (parameters[i] == null && !paramInfo[i].HasDefaultValue)
                    {
                        if (failWithDefault)
                            return default(T);

                        throw new InvalidOperationException($"The {typeof(T).ToString()} controller constructor can't resolve {paramInfo[i].ParameterType.ToString()}");
                    }
                }

                return (T)ci.Invoke(parameters);
            }

            if (failWithDefault)
                return default(T);

            throw new InvalidOperationException($"The {typeof(T).ToString()} controller has no constructor");
        }
    }
}