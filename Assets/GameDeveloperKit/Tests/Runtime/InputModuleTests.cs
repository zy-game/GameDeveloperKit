using System;
using System.Collections.Generic;
using GameDeveloperKit.Input;
using GameDeveloperKit.Timer;
using NUnit.Framework;
using UnityEngine;

namespace GameDeveloperKit.Tests
{
    public sealed class InputModuleTests : RuntimeTestBase
    {
        [TearDown]
        public void TearDown()
        {
            TryUnregister<InputModule>();
            TryUnregister<TimerModule>();
        }

        [Test]
        public void Startup_WhenStarted_HasEnabledEmptySnapshotAndUpdateHandle()
        {
            var module = CreateStartedModule(out var timer, out _);

            var snapshot = module.Snapshot();

            Assert.IsTrue(snapshot.Enabled);
            Assert.AreEqual(0, snapshot.Maps.Count);
            Assert.AreEqual(1, timer.Snapshot().Updates.Count);
        }

        [Test]
        public void RegisterMap_WhenGameplayMapAdded_AppearsInSnapshot()
        {
            var module = CreateStartedModule(out _, out _);
            module.RegisterMap(CreateGameplayMap());

            var snapshot = module.Snapshot();

            Assert.AreEqual(1, snapshot.Maps.Count);
            Assert.AreEqual("Gameplay", snapshot.Maps[0].Name);
            Assert.AreEqual(1, snapshot.Maps[0].Actions.Count);
            Assert.AreEqual("Jump", snapshot.Maps[0].Actions[0].Name);
        }

        [Test]
        public void KeyBinding_WhenPressedHeldAndReleased_UpdatesEdges()
        {
            var module = CreateStartedModule(out var timer, out var source);
            module.RegisterMap(CreateGameplayMap());

            Tick(timer);
            Assert.IsFalse(module.IsPressed("Gameplay", "Jump"));
            Assert.IsFalse(module.WasPressedThisFrame("Gameplay", "Jump"));

            source.SetKey(KeyCode.Space, true);
            Tick(timer);
            Assert.IsTrue(module.IsPressed("Gameplay", "Jump"));
            Assert.IsTrue(module.WasPressedThisFrame("Gameplay", "Jump"));
            Assert.IsFalse(module.WasReleasedThisFrame("Gameplay", "Jump"));

            Tick(timer);
            Assert.IsTrue(module.IsPressed("Gameplay", "Jump"));
            Assert.IsFalse(module.WasPressedThisFrame("Gameplay", "Jump"));

            source.SetKey(KeyCode.Space, false);
            Tick(timer);
            Assert.IsFalse(module.IsPressed("Gameplay", "Jump"));
            Assert.IsTrue(module.WasReleasedThisFrame("Gameplay", "Jump"));
        }

        [Test]
        public void MultipleBindings_WhenMousePressed_ActionIsPressed()
        {
            var module = CreateStartedModule(out var timer, out var source);
            module.RegisterMap(CreateGameplayMap());

            source.SetMouseButton(0, true);
            Tick(timer);

            Assert.IsTrue(module.IsPressed("Gameplay", "Jump"));
            Assert.AreEqual(1f, module.GetValue("Gameplay", "Jump"));
        }

        [Test]
        public void AxisBinding_WhenSourceReturnsValue_UsesScaledAxisValue()
        {
            var module = CreateStartedModule(out var timer, out var source);
            var gameplay = new InputActionMap("Gameplay");
            var move = new InputAction("MoveX");
            move.AddBinding(InputBinding.Axis("Horizontal", 0.5f));
            gameplay.AddAction(move);
            module.RegisterMap(gameplay);

            source.SetAxis("Horizontal", -1f);
            Tick(timer);

            Assert.IsTrue(module.IsPressed("Gameplay", "MoveX"));
            Assert.AreEqual(-0.5f, module.GetValue("Gameplay", "MoveX"), 0.0001f);
        }

        [Test]
        public void DisabledMap_WhenInputPressed_DoesNotTriggerAndReleasesExistingPress()
        {
            var module = CreateStartedModule(out var timer, out var source);
            module.RegisterMap(CreateGameplayMap());
            source.SetKey(KeyCode.Space, true);
            Tick(timer);
            Assert.IsTrue(module.IsPressed("Gameplay", "Jump"));

            module.SetMapEnabled("Gameplay", false);
            Tick(timer);

            Assert.IsFalse(module.IsPressed("Gameplay", "Jump"));
            Assert.IsTrue(module.WasReleasedThisFrame("Gameplay", "Jump"));

            Tick(timer);
            Assert.IsFalse(module.WasReleasedThisFrame("Gameplay", "Jump"));
        }

        [Test]
        public void DisabledModule_WhenInputPressed_DoesNotTriggerAndReleasesExistingPress()
        {
            var module = CreateStartedModule(out var timer, out var source);
            module.RegisterMap(CreateGameplayMap());
            source.SetKey(KeyCode.Space, true);
            Tick(timer);
            Assert.IsTrue(module.IsPressed("Gameplay", "Jump"));

            module.Enabled = false;
            Tick(timer);

            Assert.IsFalse(module.IsPressed("Gameplay", "Jump"));
            Assert.IsTrue(module.WasReleasedThisFrame("Gameplay", "Jump"));
        }

        [Test]
        public void Query_WhenMapOrActionMissing_ThrowsGameException()
        {
            var module = CreateStartedModule(out _, out _);
            module.RegisterMap(CreateGameplayMap());

            Assert.Throws<GameException>(() => module.IsPressed("Missing", "Jump"));
            Assert.Throws<GameException>(() => module.IsPressed("Gameplay", "Missing"));
        }

        [Test]
        public void Register_WhenArgumentsInvalid_ThrowsExpectedExceptions()
        {
            var module = CreateStartedModule(out _, out _);
            var map = CreateGameplayMap();
            module.RegisterMap(map);

            Assert.Throws<ArgumentNullException>(() => module.RegisterMap(null));
            Assert.Throws<GameException>(() => module.RegisterMap(CreateGameplayMap()));
            Assert.Throws<ArgumentNullException>(() => new InputActionMap(null));
            Assert.Throws<ArgumentException>(() => new InputActionMap(" "));
            Assert.Throws<ArgumentNullException>(() => new InputAction(null));
            Assert.Throws<ArgumentException>(() => new InputAction(" "));
            Assert.Throws<ArgumentException>(() => InputBinding.MouseButton(-1));
            Assert.Throws<ArgumentNullException>(() => InputBinding.Axis(null));
            Assert.Throws<ArgumentException>(() => InputBinding.Axis(" "));
            Assert.Throws<ArgumentNullException>(() => map.AddAction(null));
            Assert.Throws<GameException>(() => map.AddAction(new InputAction("Jump")));
            Assert.Throws<ArgumentNullException>(() => module.IsPressed(null, "Jump"));
            Assert.Throws<ArgumentException>(() => module.IsPressed(" ", "Jump"));
            Assert.Throws<ArgumentNullException>(() => module.IsPressed("Gameplay", null));
            Assert.Throws<ArgumentException>(() => module.IsPressed("Gameplay", " "));
        }

        [Test]
        public void Shutdown_WhenCalled_CancelsUpdateHandleAndClearsMaps()
        {
            var module = CreateStartedModule(out var timer, out _);
            module.RegisterMap(CreateGameplayMap());

            module.Shutdown();
            Tick(timer);
            var snapshot = module.Snapshot();

            Assert.IsFalse(snapshot.Enabled);
            Assert.AreEqual(0, snapshot.Maps.Count);
            Assert.AreEqual(0, timer.Snapshot().Updates.Count);
        }

        [Test]
        public void AppInput_WhenAccessed_StartsTimerDependency()
        {
            var module = App.Input;

            Assert.IsNotNull(module);
            Assert.IsTrue(App.TryGetRegistered<TimerModule>(out var timer));
            Assert.IsNotNull(timer);
            Assert.AreEqual(1, timer.Snapshot().Updates.Count);
        }

        private static InputModule CreateStartedModule(out TimerModule timer, out FakeInputSource source)
        {
            timer = App.Timer;
            source = new FakeInputSource();
            var module = new InputModule(source);
            module.Startup();
            return module;
        }

        private static InputActionMap CreateGameplayMap()
        {
            var gameplay = new InputActionMap("Gameplay");
            var jump = new InputAction("Jump");
            jump.AddBinding(InputBinding.Key(KeyCode.Space));
            jump.AddBinding(InputBinding.MouseButton(0));
            gameplay.AddAction(jump);
            return gameplay;
        }

        private static void Tick(TimerModule timer)
        {
            timer.Update(TimerTickKind.Update, 0.016f, 0.016f);
        }

        private static void TryUnregister<T>() where T : IGameModule
        {
            try
            {
                App.Unregister<T>().GetAwaiter().GetResult();
            }
            catch (GameException)
            {
            }
        }

        private sealed class FakeInputSource : IInputSource
        {
            private readonly HashSet<KeyCode> m_Keys = new HashSet<KeyCode>();
            private readonly HashSet<int> m_MouseButtons = new HashSet<int>();
            private readonly Dictionary<string, float> m_Axes = new Dictionary<string, float>(StringComparer.Ordinal);

            public bool GetKey(KeyCode key)
            {
                return m_Keys.Contains(key);
            }

            public bool GetMouseButton(int button)
            {
                return m_MouseButtons.Contains(button);
            }

            public float GetAxisRaw(string axisName)
            {
                return m_Axes.TryGetValue(axisName, out var value) ? value : 0f;
            }

            public void SetKey(KeyCode key, bool pressed)
            {
                if (pressed)
                {
                    m_Keys.Add(key);
                    return;
                }

                m_Keys.Remove(key);
            }

            public void SetMouseButton(int button, bool pressed)
            {
                if (pressed)
                {
                    m_MouseButtons.Add(button);
                    return;
                }

                m_MouseButtons.Remove(button);
            }

            public void SetAxis(string axisName, float value)
            {
                m_Axes[axisName] = value;
            }
        }
    }
}
