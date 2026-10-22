using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NineToFight.Player
{
    /// <summary>Keyboard + gamepad bindings for the brawler, defined in code so the scene needs no asset wiring.</summary>
    public class PlayerInputReader : MonoBehaviour
    {
        const float NavigateThreshold = 0.5f;

        InputAction move, jump, lightAttack, heavyAttack, special, super, ability, dodge, submit, restart, pause, mute, cancel;
        int lastNavigateFrame = -1, lastNavigateYFrame = -1;
        int navigateThisFrame, navigateYThisFrame;
        float previousNavigateX, previousNavigateY;

        /// <summary>-1 (down) / +1 (up) on the frame up/down is first pushed (menus), otherwise 0.</summary>
        public int NavigateY
        {
            get
            {
                if (lastNavigateYFrame != Time.frameCount)
                {
                    lastNavigateYFrame = Time.frameCount;
                    float y = Move.y;
                    navigateYThisFrame = Mathf.Abs(y) >= NavigateThreshold && Mathf.Abs(previousNavigateY) < NavigateThreshold
                        ? (y > 0f ? 1 : -1)
                        : 0;
                    previousNavigateY = y;
                }
                return navigateYThisFrame;
            }
        }

        /// <summary>Back out of a menu: Esc, Backspace or the gamepad's east button.</summary>
        public bool CancelPressed => cancel.WasPressedThisFrame();

        public Vector2 Move => move.ReadValue<Vector2>();
        public bool SuperPressed => super.WasPressedThisFrame();
        public bool AbilityPressed => ability.WasPressedThisFrame();

        /// <summary>-1/+1 on the frame left/right is first pushed (menus), otherwise 0.</summary>
        public int NavigateX
        {
            get
            {
                if (lastNavigateFrame != Time.frameCount)
                {
                    lastNavigateFrame = Time.frameCount;
                    float x = Move.x;
                    navigateThisFrame = Mathf.Abs(x) >= NavigateThreshold && Mathf.Abs(previousNavigateX) < NavigateThreshold
                        ? (x > 0f ? 1 : -1)
                        : 0;
                    previousNavigateX = x;
                }
                return navigateThisFrame;
            }
        }
        public bool JumpPressed => jump.WasPressedThisFrame();
        public bool LightPressed => lightAttack.WasPressedThisFrame();
        public bool HeavyPressed => heavyAttack.WasPressedThisFrame();
        public bool SpecialPressed => special.WasPressedThisFrame();
        public bool DodgePressed => dodge.WasPressedThisFrame();
        public bool SubmitPressed => submit.WasPressedThisFrame();
        public bool RestartPressed => restart.WasPressedThisFrame();
        public bool PausePressed => pause.WasPressedThisFrame();
        public bool MutePressed => mute.WasPressedThisFrame();

        void Awake()
        {
            move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick");
            move.AddBinding("<Gamepad>/dpad");

            jump = Button("Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            lightAttack = Button("Light", "<Keyboard>/j", "<Mouse>/leftButton", "<Gamepad>/buttonWest");
            heavyAttack = Button("Heavy", "<Keyboard>/k", "<Mouse>/rightButton", "<Gamepad>/buttonNorth");
            special = Button("Special", "<Keyboard>/i", "<Gamepad>/rightShoulder");
            super = Button("Super", "<Keyboard>/u", "<Gamepad>/rightTrigger");
            ability = Button("Ability", "<Keyboard>/q", "<Gamepad>/leftShoulder");
            dodge = Button("Dodge", "<Keyboard>/l", "<Keyboard>/leftShift", "<Gamepad>/buttonEast");
            submit = Button("Submit", "<Keyboard>/enter", "<Keyboard>/numpadEnter", "<Gamepad>/start");
            restart = Button("Restart", "<Keyboard>/r", "<Gamepad>/select");
            pause = Button("Pause", "<Keyboard>/escape", "<Keyboard>/p", "<Gamepad>/start");
            mute = Button("Mute", "<Keyboard>/m");
            cancel = Button("Cancel", "<Keyboard>/escape", "<Keyboard>/backspace", "<Gamepad>/buttonEast");
            CreateHotbar();
        }

        static InputAction Button(string name, params string[] bindings)
        {
            var action = new InputAction(name, InputActionType.Button);
            foreach (var b in bindings) action.AddBinding(b);
            return action;
        }

        InputAction[] hotbar;

        /// <summary>Index 0..9 of the hotbar key (1..9, 0) pressed this frame, or -1.</summary>
        public int HotbarPressed
        {
            get
            {
                for (int i = 0; i < hotbar.Length; i++)
                    if (hotbar[i].WasPressedThisFrame()) return i;
                return -1;
            }
        }

        void CreateHotbar()
        {
            hotbar = new InputAction[10];
            for (int i = 0; i < hotbar.Length; i++)
            {
                string digit = i == 9 ? "0" : (i + 1).ToString();
                hotbar[i] = Button("Hotbar" + digit, $"<Keyboard>/{digit}", $"<Keyboard>/numpad{digit}");
            }
        }

        IEnumerable<InputAction> All
        {
            get
            {
                foreach (var a in new[] { move, jump, lightAttack, heavyAttack, special, super, ability, dodge, submit, restart, pause, mute, cancel })
                    yield return a;
                foreach (var a in hotbar) yield return a;
            }
        }

        void OnEnable()
        {
            foreach (var a in All) a.Enable();
        }

        void OnDisable()
        {
            foreach (var a in All) a.Disable();
        }

        void OnDestroy()
        {
            foreach (var a in All) a.Dispose();
        }
    }
}
