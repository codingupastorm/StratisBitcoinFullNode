using System;
using System.Collections.Generic;
using Stratis.SmartContracts.Core.Test;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class MappingTest
    {
        [Fact]
        public void Test()
        {
            Dictionary<string, object> underlyingKvStore = new Dictionary<string, object>();
            IMapping<IMapping<string>> mapping = new Mapping<IMapping<string>>(underlyingKvStore, "MyMapping");

            // Can retrieve nested mappings and set objects in them
            IMapping<string> mapping2 = mapping["Key1"];
            mapping2["Key2"] = "Value1";
            Assert.Equal("Value1", underlyingKvStore["MyMapping[Key1][Key2]"]);

            mapping["Key1"]["Key3"] = "Value2";
            Assert.Equal("Value2", mapping2["Key3"]);

            // Can't set mapping
            Assert.Throws<NotImplementedException>(() => mapping["test"] = new Mapping<string>(underlyingKvStore, "MyMapping2"));
        }
    }
}
