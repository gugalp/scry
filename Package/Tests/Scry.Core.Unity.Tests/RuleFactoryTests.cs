using NUnit.Framework;
using Scry.Core;
using Scry.Core.Rules;
using Scry.Core.Unity.Config;

namespace Scry.Core.Unity.Tests
{
    public class RuleFactoryTests
    {
        [Test]
        public void Build_SumEqualsRuleConfig_ProducesWorkingRule()
        {
            var rule = RuleFactory.Build(new SumEqualsRuleConfig { Field = "weight", Target = 100, GroupByField = "table" });

            Assert.IsInstanceOf<SumEqualsRule>(rule);
        }

        [Test]
        public void Build_NoDuplicateRuleConfig_ProducesWorkingRule()
        {
            var rule = RuleFactory.Build(new NoDuplicateRuleConfig { Field = "itemId" });

            Assert.IsInstanceOf<NoDuplicateRule>(rule);
        }

        [Test]
        public void Build_RequiredAtLeastOnceRuleConfig_ProducesWorkingRule()
        {
            var rule = RuleFactory.Build(new RequiredAtLeastOnceRuleConfig { Field = "rarity", RequiredValue = "Starter" });

            Assert.IsInstanceOf<RequiredAtLeastOnceRule>(rule);
        }

        [Test]
        public void Build_UnknownRuleConfig_Throws()
        {
            Assert.Throws<System.NotSupportedException>(() => RuleFactory.Build(new UnknownRuleConfig()));
        }

        private sealed class UnknownRuleConfig : RuleConfig
        {
        }
    }
}
