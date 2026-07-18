using System;
using System.Collections.Generic;
using GameDeveloperKit.ChannelBuild;
using NUnit.Framework;
using UnityEditor;

namespace GameDeveloperKit.Tests
{
    public sealed class ChannelBuildResponderTests
    {
        [Test]
        public void StepResult_DefensivelyCopiesCollections()
        {
            var outputs = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["artifact"] = "player.apk"
            };
            var warnings = new List<string> { "warning" };

            var result = Result("build", ChannelBuildResponderPhase.Operation, outputs: outputs, warnings: warnings);
            outputs["artifact"] = "mutated.apk";
            warnings[0] = "mutated";

            Assert.AreEqual("player.apk", result.Outputs["artifact"]);
            Assert.AreEqual("warning", result.Warnings[0]);
            Assert.Throws<NotSupportedException>(
                () => ((IDictionary<string, string>)result.Outputs)["artifact"] = "again.apk");
            Assert.Throws<NotSupportedException>(() => ((IList<string>)result.Warnings)[0] = "again");
        }

        [TestCase("apiToken")]
        [TestCase("signing-key")]
        [TestCase("private_key_path")]
        public void StepResult_RejectsSensitiveOutputKeysWithoutLeakingValues(string key)
        {
            const string sensitiveValue = "must-not-appear";
            var outputs = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [key] = sensitiveValue
            };

            var exception = Assert.Throws<ArgumentException>(
                () => Result("build", ChannelBuildResponderPhase.Operation, outputs: outputs));

            StringAssert.Contains(key, exception.Message);
            StringAssert.DoesNotContain(sensitiveValue, exception.Message);
        }

        [Test]
        public void StepResult_RejectsInvalidFailureAndCollections()
        {
            Assert.Throws<ArgumentException>(
                () => Result("build", ChannelBuildResponderPhase.Operation, false));
            Assert.Throws<ArgumentException>(
                () => Result(
                    "build",
                    ChannelBuildResponderPhase.Operation,
                    outputs: new Dictionary<string, string> { ["artifact"] = null }));
            Assert.Throws<ArgumentException>(
                () => Result(
                    "build",
                    ChannelBuildResponderPhase.Operation,
                    warnings: new[] { "bad\nwarning" }));
        }

        [Test]
        public void CreatePlan_EmptyInputReturnsEmptyImmutablePlan()
        {
            var plan = ChannelBuildResponderRunner.CreatePlan(Array.Empty<IChannelBuildResponder>());

            Assert.IsEmpty(plan);
            Assert.Throws<NotSupportedException>(
                () => ((IList<IChannelBuildResponder>)plan).Add(new Responder("late")));
        }

        [Test]
        public void CreatePlan_UsesStableReadyOrderingAndDependencyPriority()
        {
            var responders = new IChannelBuildResponder[]
            {
                new Responder("config", 1, new[] { "defines" }),
                new Responder("z", 10),
                new Responder("defines", 50),
                new Responder("a", 10)
            };

            var first = ChannelBuildResponderRunner.CreatePlan(responders);
            var second = ChannelBuildResponderRunner.CreatePlan(responders);

            CollectionAssert.AreEqual(new[] { "a", "z", "defines", "config" }, Ids(first));
            CollectionAssert.AreEqual(Ids(first), Ids(second));
        }

        [Test]
        public void CreatePlan_SnapshotsMetadataOnce()
        {
            var responder = new Responder("snapshot");

            var plan = ChannelBuildResponderRunner.CreatePlan(new[] { responder });

            Assert.AreSame(responder, plan[0]);
            Assert.AreEqual(1, responder.IdReadCount);
            Assert.AreEqual(1, responder.OrderReadCount);
            Assert.AreEqual(1, responder.DependsOnReadCount);
        }

        [Test]
        public void CreatePlan_RejectsInvalidMetadata()
        {
            Assert.Throws<ArgumentNullException>(() => ChannelBuildResponderRunner.CreatePlan(null));
            Assert.Throws<ArgumentException>(
                () => ChannelBuildResponderRunner.CreatePlan(new IChannelBuildResponder[] { null }));
            Assert.Throws<ArgumentException>(
                () => ChannelBuildResponderRunner.CreatePlan(
                    new IChannelBuildResponder[] { new Responder("same"), new Responder("same") }));
            Assert.Throws<ArgumentException>(
                () => ChannelBuildResponderRunner.CreatePlan(
                    new IChannelBuildResponder[] { new NullDependenciesResponder() }));
            Assert.Throws<ArgumentException>(
                () => ChannelBuildResponderRunner.CreatePlan(
                    new IChannelBuildResponder[] { new Responder("a", dependsOn: new[] { "missing" }) }));
            Assert.Throws<ArgumentException>(
                () => ChannelBuildResponderRunner.CreatePlan(
                    new IChannelBuildResponder[] { new Responder("a", dependsOn: new[] { "a" }) }));
        }

        [Test]
        public void CreatePlan_RejectsDependencyCycle()
        {
            var responders = new IChannelBuildResponder[]
            {
                new Responder("a", dependsOn: new[] { "b" }),
                new Responder("b", dependsOn: new[] { "a" })
            };

            Assert.Throws<GameException>(() => ChannelBuildResponderRunner.CreatePlan(responders));
        }

        [Test]
        public void Execute_SuccessRunsForwardAndRestoresInReverse()
        {
            var trace = new List<string>();
            var responders = new IChannelBuildResponder[]
            {
                new Responder("b", 20, trace: trace),
                new Responder("a", 10, trace: trace)
            };

            var execution = ChannelBuildResponderRunner.Execute(
                CreateContext(),
                responders,
                context =>
                {
                    trace.Add("operation");
                    return Result("build", ChannelBuildResponderPhase.Operation);
                });

            Assert.IsTrue(execution.Success);
            Assert.IsNull(execution.PrimaryFailure);
            CollectionAssert.AreEqual(
                new[]
                {
                    "prepare:a", "prepare:b", "apply:a", "apply:b", "operation", "restore:b", "restore:a"
                },
                trace);
            CollectionAssert.AreEqual(new[] { "a", "b" }, Ids(execution.Plan));
            Assert.AreEqual(7, execution.Results.Count);
            Assert.Throws<NotSupportedException>(
                () => ((IList<IChannelBuildResponder>)execution.Plan).Clear());
            Assert.Throws<NotSupportedException>(
                () => ((IList<ChannelBuildStepResult>)execution.Results).Clear());
        }

        [Test]
        public void Execute_PrepareFailureStopsWithoutMutationOrCleanup()
        {
            var trace = new List<string>();
            var failure = Result("b", ChannelBuildResponderPhase.Prepare, false, "not ready");
            var responders = new IChannelBuildResponder[]
            {
                new Responder("a", trace: trace),
                new Responder("b", 1, trace: trace, prepare: () => failure)
            };

            var execution = ChannelBuildResponderRunner.Execute(
                CreateContext(),
                responders,
                context => throw new InvalidOperationException("operation must not run"));

            Assert.IsFalse(execution.Success);
            Assert.AreSame(failure, execution.PrimaryFailure);
            CollectionAssert.AreEqual(new[] { "prepare:a", "prepare:b" }, trace);
        }

        [Test]
        public void Execute_PrepareExceptionStopsWithoutCleanup()
        {
            var trace = new List<string>();
            var responder = new Responder(
                "a",
                trace: trace,
                prepare: () => throw new InvalidOperationException("prepare"));

            var exception = Assert.Throws<GameException>(
                () => ChannelBuildResponderRunner.Execute(
                    CreateContext(),
                    new[] { responder },
                    context => Result("build", ChannelBuildResponderPhase.Operation)));

            Assert.IsInstanceOf<InvalidOperationException>(exception.InnerException);
            CollectionAssert.AreEqual(new[] { "prepare:a" }, trace);
        }

        [Test]
        public void Execute_ApplyFailureRestoresCurrentAndPreviouslyAppliedResponders()
        {
            var trace = new List<string>();
            var failure = Result("b", ChannelBuildResponderPhase.Apply, false, "apply failed");
            var responders = new IChannelBuildResponder[]
            {
                new Responder("a", trace: trace),
                new Responder("b", 1, trace: trace, apply: () => failure),
                new Responder("c", 2, trace: trace)
            };

            var execution = ChannelBuildResponderRunner.Execute(
                CreateContext(),
                responders,
                context => throw new InvalidOperationException("operation must not run"));

            Assert.AreSame(failure, execution.PrimaryFailure);
            CollectionAssert.AreEqual(
                new[]
                {
                    "prepare:a", "prepare:b", "prepare:c", "apply:a", "apply:b", "restore:b", "restore:a"
                },
                trace);
        }

        [Test]
        public void Execute_ApplyExceptionRestoresBeforeThrowing()
        {
            var trace = new List<string>();
            var responder = new Responder(
                "a",
                trace: trace,
                apply: () => throw new InvalidOperationException("apply"));

            var exception = Assert.Throws<GameException>(
                () => ChannelBuildResponderRunner.Execute(
                    CreateContext(),
                    new[] { responder },
                    context => Result("build", ChannelBuildResponderPhase.Operation)));

            Assert.IsInstanceOf<InvalidOperationException>(exception.InnerException);
            CollectionAssert.AreEqual(new[] { "prepare:a", "apply:a", "restore:a" }, trace);
        }

        [Test]
        public void Execute_OperationFailureRemainsPrimaryWhenRestoreAlsoFails()
        {
            var trace = new List<string>();
            var operationFailure = Result("build", ChannelBuildResponderPhase.Operation, false, "build failed");
            var restoreFailure = Result("a", ChannelBuildResponderPhase.Restore, false, "restore failed");
            var responder = new Responder("a", trace: trace, restore: () => restoreFailure);

            var execution = ChannelBuildResponderRunner.Execute(
                CreateContext(),
                new[] { responder },
                context =>
                {
                    trace.Add("operation");
                    return operationFailure;
                });

            Assert.IsFalse(execution.Success);
            Assert.AreSame(operationFailure, execution.PrimaryFailure);
            Assert.AreSame(restoreFailure, execution.Results[execution.Results.Count - 1]);
            CollectionAssert.AreEqual(
                new[] { "prepare:a", "apply:a", "operation", "restore:a" },
                trace);
        }

        [Test]
        public void Execute_RestoreFailuresContinueAndFirstBecomesPrimary()
        {
            var trace = new List<string>();
            var firstRestoreFailure = Result("b", ChannelBuildResponderPhase.Restore, false, "b restore failed");
            var responders = new IChannelBuildResponder[]
            {
                new Responder(
                    "a",
                    trace: trace,
                    restore: () => Result("a", ChannelBuildResponderPhase.Restore, false, "a restore failed")),
                new Responder("b", 1, trace: trace, restore: () => firstRestoreFailure)
            };

            var execution = ChannelBuildResponderRunner.Execute(
                CreateContext(),
                responders,
                context => Result("build", ChannelBuildResponderPhase.Operation));

            Assert.IsFalse(execution.Success);
            Assert.AreSame(firstRestoreFailure, execution.PrimaryFailure);
            CollectionAssert.AreEqual(
                new[]
                {
                    "prepare:a", "prepare:b", "apply:a", "apply:b", "restore:b", "restore:a"
                },
                trace);
        }

        [Test]
        public void Execute_OperationAndRestoreExceptionsAreAggregatedAfterAllCleanup()
        {
            var trace = new List<string>();
            var responders = new IChannelBuildResponder[]
            {
                new Responder(
                    "a",
                    trace: trace,
                    restore: () => throw new ArgumentException("restore a")),
                new Responder(
                    "b",
                    1,
                    trace: trace,
                    restore: () => throw new ApplicationException("restore b"))
            };

            var exception = Assert.Throws<GameException>(
                () => ChannelBuildResponderRunner.Execute(
                    CreateContext(),
                    responders,
                    context => throw new InvalidOperationException("operation")));

            var aggregate = exception.InnerException as AggregateException;
            Assert.IsNotNull(aggregate);
            Assert.AreEqual(3, aggregate.InnerExceptions.Count);
            Assert.IsInstanceOf<InvalidOperationException>(aggregate.InnerExceptions[0]);
            Assert.IsInstanceOf<ApplicationException>(aggregate.InnerExceptions[1]);
            Assert.IsInstanceOf<ArgumentException>(aggregate.InnerExceptions[2]);
            CollectionAssert.AreEqual(
                new[]
                {
                    "prepare:a", "prepare:b", "apply:a", "apply:b", "restore:b", "restore:a"
                },
                trace);
        }

        [Test]
        public void Execute_NullOrMismatchedApplyResultRestoresBeforeThrowing()
        {
            foreach (var result in new ChannelBuildStepResult[]
            {
                null,
                Result("other", ChannelBuildResponderPhase.Apply),
                Result("a", ChannelBuildResponderPhase.Prepare)
            })
            {
                var trace = new List<string>();
                var responder = new Responder("a", trace: trace, apply: () => result);

                Assert.Throws<GameException>(
                    () => ChannelBuildResponderRunner.Execute(
                        CreateContext(),
                        new[] { responder },
                        context => Result("build", ChannelBuildResponderPhase.Operation)));
                CollectionAssert.AreEqual(new[] { "prepare:a", "apply:a", "restore:a" }, trace);
            }
        }

        [Test]
        public void Execute_InvalidRestoreResultContinuesRemainingCleanup()
        {
            var trace = new List<string>();
            var responders = new IChannelBuildResponder[]
            {
                new Responder("a", trace: trace),
                new Responder("b", 1, trace: trace, restore: () => null)
            };

            Assert.Throws<GameException>(
                () => ChannelBuildResponderRunner.Execute(
                    CreateContext(),
                    responders,
                    context => Result("build", ChannelBuildResponderPhase.Operation)));
            CollectionAssert.AreEqual(
                new[]
                {
                    "prepare:a", "prepare:b", "apply:a", "apply:b", "restore:b", "restore:a"
                },
                trace);
        }

        [Test]
        public void Execute_InvalidOperationResultRestoresBeforeThrowing()
        {
            foreach (var result in new ChannelBuildStepResult[]
            {
                null,
                Result("build", ChannelBuildResponderPhase.Apply)
            })
            {
                var trace = new List<string>();
                var responder = new Responder("a", trace: trace);

                Assert.Throws<GameException>(
                    () => ChannelBuildResponderRunner.Execute(
                        CreateContext(),
                        new[] { responder },
                        context => result));
                CollectionAssert.AreEqual(new[] { "prepare:a", "apply:a", "restore:a" }, trace);
            }
        }

        [Test]
        public void Execute_ValidatesArguments()
        {
            var context = CreateContext();
            var responders = Array.Empty<IChannelBuildResponder>();

            Assert.Throws<ArgumentNullException>(
                () => ChannelBuildResponderRunner.Execute(null, responders, ignored => Result("build", ChannelBuildResponderPhase.Operation)));
            Assert.Throws<ArgumentNullException>(
                () => ChannelBuildResponderRunner.Execute(context, responders, null));
        }

        private static ChannelBuildContext CreateContext()
        {
            return new ChannelBuildContext(
                "dev",
                ChannelBuildEnvironment.Dev,
                BuildTarget.Android,
                "1.2.3",
                1,
                "Build/Channel");
        }

        private static ChannelBuildStepResult Result(
            string id,
            ChannelBuildResponderPhase phase,
            bool success = true,
            string message = null,
            IReadOnlyDictionary<string, string> outputs = null,
            IReadOnlyList<string> warnings = null)
        {
            return new ChannelBuildStepResult(id, phase, success, message, outputs, warnings);
        }

        private static IReadOnlyList<string> Ids(IReadOnlyList<IChannelBuildResponder> responders)
        {
            var ids = new List<string>(responders.Count);
            for (var i = 0; i < responders.Count; i++)
            {
                ids.Add(responders[i].Id);
            }
            return ids;
        }

        private sealed class Responder : IChannelBuildResponder
        {
            private readonly string m_Id;
            private readonly int m_Order;
            private readonly IReadOnlyList<string> m_DependsOn;
            private readonly List<string> m_Trace;
            private readonly Func<ChannelBuildStepResult> m_Prepare;
            private readonly Func<ChannelBuildStepResult> m_Apply;
            private readonly Func<ChannelBuildStepResult> m_Restore;

            internal Responder(
                string id,
                int order = 0,
                IReadOnlyList<string> dependsOn = default,
                List<string> trace = null,
                Func<ChannelBuildStepResult> prepare = null,
                Func<ChannelBuildStepResult> apply = null,
                Func<ChannelBuildStepResult> restore = null)
            {
                m_Id = id;
                m_Order = order;
                m_DependsOn = dependsOn == default ? Array.Empty<string>() : dependsOn;
                m_Trace = trace;
                m_Prepare = prepare;
                m_Apply = apply;
                m_Restore = restore;
            }

            public int IdReadCount { get; private set; }

            public int OrderReadCount { get; private set; }

            public int DependsOnReadCount { get; private set; }

            public string Id
            {
                get
                {
                    IdReadCount++;
                    return m_Id;
                }
            }

            public int Order
            {
                get
                {
                    OrderReadCount++;
                    return m_Order;
                }
            }

            public IReadOnlyList<string> DependsOn
            {
                get
                {
                    DependsOnReadCount++;
                    return m_DependsOn;
                }
            }

            public ChannelBuildStepResult Prepare(ChannelBuildContext context)
            {
                m_Trace?.Add($"prepare:{m_Id}");
                return m_Prepare == null
                    ? Result(m_Id, ChannelBuildResponderPhase.Prepare)
                    : m_Prepare();
            }

            public ChannelBuildStepResult Apply(ChannelBuildContext context)
            {
                m_Trace?.Add($"apply:{m_Id}");
                return m_Apply == null
                    ? Result(m_Id, ChannelBuildResponderPhase.Apply)
                    : m_Apply();
            }

            public ChannelBuildStepResult Restore(ChannelBuildContext context)
            {
                m_Trace?.Add($"restore:{m_Id}");
                return m_Restore == null
                    ? Result(m_Id, ChannelBuildResponderPhase.Restore)
                    : m_Restore();
            }
        }

        private sealed class NullDependenciesResponder : IChannelBuildResponder
        {
            public string Id => "null-dependencies";

            public int Order => 0;

            public IReadOnlyList<string> DependsOn => null;

            public ChannelBuildStepResult Prepare(ChannelBuildContext context)
            {
                throw new NotSupportedException();
            }

            public ChannelBuildStepResult Apply(ChannelBuildContext context)
            {
                throw new NotSupportedException();
            }

            public ChannelBuildStepResult Restore(ChannelBuildContext context)
            {
                throw new NotSupportedException();
            }
        }
    }
}
