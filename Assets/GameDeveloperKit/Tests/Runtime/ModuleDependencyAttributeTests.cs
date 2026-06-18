using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.Config;
using GameDeveloperKit.Download;
using GameDeveloperKit.Event;
using GameDeveloperKit.File;
using GameDeveloperKit.Operation;
using GameDeveloperKit.Resource;
using GameDeveloperKit.Sound;
using GameDeveloperKit.Timer;
using GameDeveloperKit.UI;
using NUnit.Framework;

namespace GameDeveloperKit.Tests
{
    public sealed class ModuleDependencyAttributeTests
    {
        [Test]
        public void DependencyAttribute_WhenDependencyTypeIsProvided_StoresDependencyType()
        {
            var attribute = new TestDependencyAttribute(typeof(TestModule));

            Assert.AreEqual(typeof(TestModule), attribute.DependencyType);
        }

        [Test]
        public void DependencyAttribute_WhenDependencyTypeIsNull_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new TestDependencyAttribute(null));
        }

        [Test]
        public void ModuleDependencyAttribute_WhenModuleTypeIsProvided_StoresDependencyType()
        {
            var attribute = new ModuleDependencyAttribute(typeof(TestModule));

            Assert.IsInstanceOf<DependencyAttribute>(attribute);
            Assert.AreEqual(typeof(TestModule), attribute.DependencyType);
        }

        [Test]
        public void ModuleDependencyAttribute_WhenModuleTypeIsNull_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ModuleDependencyAttribute(null));
        }

        [Test]
        public void ModuleDependencyAttribute_WhenModuleTypeDoesNotImplementIGameModule_Throws()
        {
            Assert.Throws<GameException>(() => new ModuleDependencyAttribute(typeof(string)));
        }

        [Test]
        public void DependencyAttributes_WhenReadingAttributeUsage_MatchDependencyContract()
        {
            AssertAttributeUsage<DependencyAttribute>();
            AssertAttributeUsage<ModuleDependencyAttribute>();
        }

        [Test]
        public void RuntimeModules_WhenReadingModuleDependencyAttributes_MatchStartupDependencies()
        {
            AssertModuleDependencies<EventModule>(typeof(TimerModule));
            AssertModuleDependencies<DownloadModule>(typeof(OperationModule));
            AssertModuleDependencies<ResourceModule>(typeof(OperationModule), typeof(DownloadModule), typeof(FileModule));
            AssertModuleDependencies<ConfigModule>(typeof(ResourceModule), typeof(DownloadModule));
            AssertModuleDependencies<SoundModule>(typeof(ResourceModule));
            AssertModuleDependencies<UIModule>(typeof(ResourceModule));
        }

        private static void AssertAttributeUsage<TAttribute>() where TAttribute : Attribute
        {
            var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(
                typeof(TAttribute),
                typeof(AttributeUsageAttribute));

            Assert.IsNotNull(usage);
            Assert.AreEqual(AttributeTargets.Class, usage.ValidOn);
            Assert.IsTrue(usage.AllowMultiple);
            Assert.IsFalse(usage.Inherited);
        }

        private static void AssertModuleDependencies<TModule>(params Type[] expectedDependencies)
            where TModule : IGameModule
        {
            var attributes = (ModuleDependencyAttribute[])Attribute.GetCustomAttributes(
                typeof(TModule),
                typeof(ModuleDependencyAttribute),
                false);
            var actualDependencies = new HashSet<Type>(attributes.Select(attribute => attribute.DependencyType));

            CollectionAssert.AreEquivalent(expectedDependencies, actualDependencies);
            Assert.AreEqual(expectedDependencies.Length, attributes.Length);
        }

        private sealed class TestDependencyAttribute : DependencyAttribute
        {
            public TestDependencyAttribute(Type dependencyType) : base(dependencyType)
            {
            }
        }

        private sealed class TestModule : GameModuleBase
        {
            public override void Startup()
            {
            }

            public override void Shutdown()
            {
            }
        }
    }
}
