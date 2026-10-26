using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NineToFight.Player
{
    /// <summary>Keyboard + gamepad bindings for the brawler, defined in code so the scene needs no asset wiring.</summary>
    public class PlayerInputReader : MonoBehaviour
    {
        const float NavigateThreshold = 0.5f;

        InputAction move, jump, lightAttack, heavyAttack, special, super, ability, dodge, submit, startButton, restart, pause, mute, cancel;
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
        public bool SuperPressed => !HotbarChord && super.WasPressedThisFrame();
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
        // While LT is held the gamepad's face buttons and right shoulder pick hotbar slots instead (see HotbarPressed).
        public bool JumpPressed => !HotbarChord && jump.WasPressedThisFrame();
        public bool LightPressed => !HotbarChord && lightAttack.WasPressedThisFrame();
        public bool HeavyPressed => !HotbarChord && heavyAttack.WasPressedThisFrame();
        public bool SpecialPressed => !HotbarChord && special.WasPressedThisFrame();
        public bool DodgePressed => !HotbarChord && dodge.WasPressedThisFrame();

        /// <summary>LT held: the gamepad's hotbar layer (X Y B A RB RT = slots 1-6).</summary>
        public bool HotbarChord => hotbarChord != null && hotbarChord.IsPressed();
        /// <summary>Confirm in the pause and options menus: Enter or the gamepad's A (Start is Pause there).</summary>
        public bool SubmitPressed => submit.WasPressedThisFrame();
        /// <summary>Confirm on screens with nothing to pause (title, department select, endings): also Start.</summary>
        public bool ConfirmPressed => submit.WasPressedThisFrame() || startButton.WasPressedThisFrame();
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
            // Start is Pause; A confirms. Start also confirms where there's nothing to pause (ConfirmPressed).
            submit = Button("Submit", "<Keyboard>/enter", "<Keyboard>/numpadEnter", "<Gamepad>/buttonSouth");
            startButton = Button("Start", "<Gamepad>/start");
            hotbarChord = Button("HotbarChord", "<Gamepad>/leftTrigger");
            padHotbar = new[]
            {
                Button("PadSlot1", "<Gamepad>/buttonWest"), Button("PadSlot2", "<Gamepad>/buttonNorth"),
                Button("PadSlot3", "<Gamepad>/buttonEast"), Button("PadSlot4", "<Gamepad>/buttonSouth"),
                Button("PadSlot5", "<Gamepad>/rightShoulder"), Button("PadSlot6", "<Gamepad>/rightTrigger"),
            };
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

        InputAction[] hotbar, padHotbar;
        InputAction hotbarChord;

        /// <summary>Index 0..9 of the hotbar key (1..9, 0) pressed this frame, or -1. On a gamepad: LT + X Y B A RB RT for slots 1-6.</summary>
        public int HotbarPressed
        {
            get
            {
                for (int i = 0; i < hotbar.Length; i++)
                    if (hotbar[i].WasPressedThisFrame()) return i;
                if (HotbarChord)
                    for (int i = 0; i < padHotbar.Length; i++)
                        if (padHotbar[i].WasPressedThisFrame()) return i;
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
                foreach (var a in new[] { move, jump, lightAttack, heavyAttack, special, super, ability, dodge, submit, startButton, restart, pause, mute, cancel })
                    yield return a;
                foreach (var a in hotbar) yield return a;
                foreach (var a in padHotbar) yield return a;
                yield return hotbarChord;
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
