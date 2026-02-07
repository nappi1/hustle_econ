using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Core;

namespace Tests.Core
{
    public class InputManagerTests
    {
        private GameObject inputGameObject;
        private InputManager inputManager;

        [SetUp]
        public void SetUp()
        {
            ResetSingleton(typeof(InputManager));
            inputGameObject = new GameObject("InputManager");
            inputManager = inputGameObject.AddComponent<InputManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (inputGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(inputGameObject);
            }
        }

        [Test]
        public void Initialize_DefaultContextGameplay()
        {
            Assert.AreEqual(InputManager.InputContext.Gameplay, inputManager.GetContext(), "Default context should be Gameplay");
        }

        [Test]
        public void SetContext_ChangesAndFiresEvent()
        {
            bool fired = false;
            inputManager.OnContextChanged += _ => fired = true;
            inputManager.SetContext(InputManager.InputContext.UI);
            Assert.IsTrue(fired, "Context change event should fire");
            Assert.AreEqual(InputManager.InputContext.UI, inputManager.GetContext(), "Context should change");
        }

        [Test]
        public void PushContext_SavesAndChanges()
        {
            inputManager.SetContext(InputManager.InputContext.Gameplay);
            inputManager.PushContext(InputManager.InputContext.UI);
            Assert.AreEqual(InputManager.InputContext.UI, inputManager.GetContext(), "Context should be pushed to UI");
        }

        [Test]
        public void PopContext_RestoresPrevious()
        {
            inputManager.SetContext(InputManager.InputContext.Gameplay);
            inputManager.PushContext(InputManager.InputContext.UI);
            inputManager.PopContext();
            Assert.AreEqual(InputManager.InputContext.Gameplay, inputManager.GetContext(), "Context should pop back to Gameplay");
        }

        [Test]
        public void PopContext_Empty_DoesNotChange()
        {
            inputManager.SetContext(InputManager.InputContext.Phone);
            inputManager.PopContext();
            Assert.AreEqual(InputManager.InputContext.Phone, inputManager.GetContext(), "Context should remain when stack empty");
        }

        [Test]
        public void SimulateMovement_SetsMovementInput()
        {
            inputManager.SimulateMovement(new Vector2(1f, 0f));
            InvokeProcessMovementInput(inputManager);
            Vector2 raw = inputManager.GetMovementRaw();
            Assert.AreEqual(1f, raw.x, 0.001f, "Movement X should be 1");
            Assert.AreEqual(0f, raw.y, 0.001f, "Movement Y should be 0");
        }

        [Test]
        public void Movement_NormalizesDiagonal()
        {
            inputManager.SimulateMovement(new Vector2(1f, 1f));
            InvokeProcessMovementInput(inputManager);
            Vector2 raw = inputManager.GetMovementRaw();
            Assert.AreEqual(1f, raw.magnitude, 0.001f, "Diagonal movement should normalize to magnitude 1");
        }

        [Test]
        public void Movement_BlockedInUIContext()
        {
            inputManager.SetContext(InputManager.InputContext.UI);
            inputManager.SimulateMovement(new Vector2(1f, 0f));
            InvokeProcessMovementInput(inputManager);
            Vector2 raw = inputManager.GetMovementRaw();
            Assert.AreEqual(Vector2.zero, raw, "Movement should be blocked in UI context");
        }

        [Test]
        public void IsRunning_DetectedWhenSimulatedRun()
        {
            inputManager.SimulateInput(InputManager.InputAction.Run, true);
            inputManager.SimulateMovement(new Vector2(0f, 1f));
            InvokeProcessMovementInput(inputManager);
            Assert.IsTrue(inputManager.IsRunning(), "Running should be true when Run input is simulated");
        }

        [Test]
        public void IsRunning_FalseWhenNotRunning()
        {
            inputManager.SimulateMovement(new Vector2(0f, 1f));
            InvokeProcessMovementInput(inputManager);
            Assert.IsFalse(inputManager.IsRunning(), "Running should be false by default");
        }

        [Test]
        public void SimulateInput_SetsActionHeld()
        {
            inputManager.SimulateInput(InputManager.InputAction.Interact, true);
            Assert.IsTrue(inputManager.GetAction(InputManager.InputAction.Interact), "Action should be held");
        }

        [Test]
        public void SimulateInput_ActionDownAndUp()
        {
            inputManager.SimulateInput(InputManager.InputAction.Interact, true);
            Assert.IsTrue(inputManager.GetActionDown(InputManager.InputAction.Interact), "ActionDown should be true on press");

            inputManager.ClearFrameForTesting();
            Assert.IsFalse(inputManager.GetActionDown(InputManager.InputAction.Interact), "ActionDown should clear after update");

            inputManager.SimulateInput(InputManager.InputAction.Interact, false);
            Assert.IsTrue(inputManager.GetActionUp(InputManager.InputAction.Interact), "ActionUp should be true on release");
        }

        [Test]
        public void OnActionPressed_EventFires()
        {
            bool fired = false;
            inputManager.OnActionPressed += action =>
            {
                if (action == InputManager.InputAction.Interact)
                {
                    fired = true;
                }
            };

            inputManager.SimulateInput(InputManager.InputAction.Interact, true);
            Assert.IsTrue(fired, "OnActionPressed should fire");
        }

        [Test]
        public void OnActionReleased_EventFires()
        {
            bool fired = false;
            inputManager.OnActionReleased += action =>
            {
                if (action == InputManager.InputAction.Interact)
                {
                    fired = true;
                }
            };

            inputManager.SimulateInput(InputManager.InputAction.Interact, true);
            inputManager.SimulateInput(InputManager.InputAction.Interact, false);
            Assert.IsTrue(fired, "OnActionReleased should fire");
        }

        [Test]
        public void GetMovementInput_ConvertsVector2ToVector3()
        {
            inputManager.SimulateMovement(new Vector2(0.5f, 0.25f));
            InvokeProcessMovementInput(inputManager);
            Vector3 movement = inputManager.GetMovementInput();
            Assert.AreEqual(0.5f, movement.x, 0.001f, "X should map from input X");
            Assert.AreEqual(0f, movement.y, 0.001f, "Y should be zero");
            Assert.AreEqual(0.25f, movement.z, 0.001f, "Z should map from input Y");
        }

        [Test]
        public void GetMovementRaw_ReturnsVector2()
        {
            inputManager.SimulateMovement(new Vector2(-1f, 0.3f));
            InvokeProcessMovementInput(inputManager);
            Assert.AreEqual(new Vector2(-1f, 0.3f).normalized, inputManager.GetMovementRaw(), "Raw movement should match normalized input");
        }

        [Test]
        public void RebindKey_UpdatesPrimary()
        {
            inputManager.RebindKey(InputManager.InputAction.Interact, KeyCode.Z, true);
            InputManager.KeyBinding binding = inputManager.GetBinding(InputManager.InputAction.Interact);
            Assert.AreEqual(KeyCode.Z, binding.primaryKey, "Primary key should update");
        }

        [Test]
        public void RebindKey_UpdatesAlternate()
        {
            inputManager.RebindKey(InputManager.InputAction.Interact, KeyCode.X, false);
            InputManager.KeyBinding binding = inputManager.GetBinding(InputManager.InputAction.Interact);
            Assert.AreEqual(KeyCode.X, binding.alternateKey, "Alternate key should update");
        }

        [Test]
        public void RebindKey_FiresEvent()
        {
            bool fired = false;
            inputManager.OnKeyRebound += (action, key) =>
            {
                if (action == InputManager.InputAction.Interact && key == KeyCode.Z)
                {
                    fired = true;
                }
            };

            inputManager.RebindKey(InputManager.InputAction.Interact, KeyCode.Z, true);
            Assert.IsTrue(fired, "Rebind event should fire");
        }

        [Test]
        public void GetBinding_ReturnsBinding()
        {
            InputManager.KeyBinding binding = inputManager.GetBinding(InputManager.InputAction.Interact);
            Assert.IsNotNull(binding, "Binding should exist");
        }

        [Test]
        public void ResetToDefaults_RestoresBindings()
        {
            inputManager.RebindKey(InputManager.InputAction.Interact, KeyCode.Z, true);
            inputManager.ResetToDefaults();
            InputManager.KeyBinding binding = inputManager.GetBinding(InputManager.InputAction.Interact);
            Assert.AreEqual(KeyCode.E, binding.primaryKey, "Reset should restore default key");
        }

        [Test]
        public void LoadKeyBindings_OverridesBindings()
        {
            var settings = new InputManager.InputSettings
            {
                keyBindings = new List<InputManager.KeyBinding>
                {
                    new InputManager.KeyBinding
                    {
                        action = InputManager.InputAction.Interact,
                        primaryKey = KeyCode.Z,
                        alternateKey = KeyCode.None
                    }
                }
            };

            inputManager.LoadKeyBindings(settings);
            InputManager.KeyBinding binding = inputManager.GetBinding(InputManager.InputAction.Interact);
            Assert.AreEqual(KeyCode.Z, binding.primaryKey, "Loaded binding should override default");
        }

        [Test]
        public void LoadKeyBindings_Null_DoesNotThrow()
        {
            inputManager.LoadKeyBindings(null);
            InputManager.KeyBinding binding = inputManager.GetBinding(InputManager.InputAction.Interact);
            Assert.AreEqual(KeyCode.E, binding.primaryKey, "Binding should remain default on null load");
        }

        [Test]
        public void SetContextForTesting_ChangesContext()
        {
            inputManager.SetContextForTesting(InputManager.InputContext.UI);
            Assert.AreEqual(InputManager.InputContext.UI, inputManager.GetContext(), "SetContextForTesting should change context");
        }

        [Test]
        public void GetStateForTesting_ReturnsState()
        {
            InputManager.InputState state = inputManager.GetStateForTesting();
            Assert.IsNotNull(state, "State should be returned");
        }

        [Test]
        public void SimulateInput_DoesNotAffectOtherActions()
        {
            inputManager.SimulateInput(InputManager.InputAction.Interact, true);
            Assert.IsFalse(inputManager.GetAction(InputManager.InputAction.OpenPhone), "Other actions should remain false");
        }

        [Test]
        public void CursorVisibility_ChangesWithContext()
        {
            inputManager.SetContext(InputManager.InputContext.UI);
            Assert.IsTrue(Cursor.visible, "Cursor should be visible in UI");

            inputManager.SetContext(InputManager.InputContext.Gameplay);
            Assert.IsFalse(Cursor.visible, "Cursor should be hidden in Gameplay");
        }

        private void InvokeProcessMovementInput(InputManager target)
        {
            MethodInfo method = typeof(InputManager).GetMethod("ProcessMovementInput", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Invoke(target, null);
        }

        private static void ResetSingleton(Type type)
        {
            FieldInfo field = type.GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(null, null);
            }
        }
    }
}
