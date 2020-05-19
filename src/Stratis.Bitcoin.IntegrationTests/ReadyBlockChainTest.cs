using System.IO;
using System.IO.Compression;
using NBitcoin;
using Stratis.Bitcoin.IntegrationTests.Common.ReadyData;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Core.Configuration;
using Stratis.Core.NodeStorage;
using Stratis.Core.NodeStorage.KeyValueStoreDBreeze;
using Stratis.Core.NodeStorage.KeyValueStoreLevelDB;
using Stratis.Core.Utilities;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    internal static class NormalizeDirectorySeparatorExt
    {
        /// <summary>
        /// Fixes incorrect directory separator characters in path (if any).
        /// </summary>
        public static string NormalizeDirectorySeparator(this string path)
        {
            // Replace incorrect with correct
            return path.Replace((Path.DirectorySeparatorChar == '/') ? '\\' : '/', Path.DirectorySeparatorChar);
        }
    }

    public class ReadyBlockChainTest
    {
        private void MigrateStores(Network network, string readyDataName)
        {
            string temp = Path.GetTempPath();
            string dir = Path.Combine(temp, readyDataName.Substring(0, readyDataName.LastIndexOf('.')));
            string workDir = dir.Replace("ReadyData", "ReadyDataLevelDB");
            string zipSource = Path.GetFullPath(readyDataName);
            string zipTarget = Path.Combine(temp, readyDataName.Replace("ReadyData", "ReadyDataLevelDB"));
            string typeName = network.IsBitcoin() ? "bitcoin" : "stratis";

            Directory.CreateDirectory("ReadyDataLevelDB");

            ZipFile.ExtractToDirectory(zipSource, dir, true);

            var dataFolderDBZ = new DataFolder(Path.Combine(dir, typeName, network.Name));
            var dataFolderLDB = new DataFolder(Path.Combine(workDir, typeName, network.Name));

            (new Migrate()).MigrateKeyValueStore<KeyValueStoreDBreeze, KeyValueStoreLevelDB>(network, dataFolderDBZ, dataFolderLDB);

            if (File.Exists(zipTarget))
                File.Delete(zipTarget);

            ZipFile.CreateFromDirectory(workDir, zipTarget);
        }

        [Fact(Skip = "Run this manually when needed")]
        public void MigrateFromDBreezeToLevelDb()
        {
            foreach (string readyDataName in ReadyBlockchain.StratisRegTestAll)
                this.MigrateStores(KnownNetworks.StratisRegTest, readyDataName.NormalizeDirectorySeparator());

            foreach (string readyDataName in ReadyBlockchain.BitcoinRegTestAll)
                this.MigrateStores(KnownNetworks.RegTest, readyDataName.NormalizeDirectorySeparator());

            this.MigrateStores(KnownNetworks.StratisMain, ReadyBlockchain.StratisMainnet9500.NormalizeDirectorySeparator());
        }
    }
}
