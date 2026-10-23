using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShadowBlade.Player
{
    /// <summary>
    /// Lit les entrées du joueur : clavier, manette et contrôles tactiles.
    /// Les contrôles tactiles (OnScreenStick / OnScreenButton) simulent une manette virtuelle,
    /// donc les mêmes actions fonctionnent sur PC comme sur téléphone.
    /// Les touches clavier sont liées à la position physique : WASD = ZQSD sur un clavier AZERTY.
    /// </summary>
    public class PlayerInputReader : MonoBehaviour
    {
        /// <summary>Touches des compétences 1, 2, 3 : clavier puis manette.</summary>
        static readonly string[][] SkillBindings =
        {
            new[] { "<Keyboard>/1", "<Gamepad>/buttonNorth" },
            new[] { "<Keyboard>/2", "<Gamepad>/leftShoulder" },
            new[] { "<Keyboard>/3", "<Gamepad>/rightShoulder" },
        };

        public Vector2 Move { get; private set; }

        public event Action DodgePressed;
        public event Action AttackPressed;
        /// <summary>Indice de la compétence (0, 1 ou 2).</summary>
        public event Action<int> SkillPressed;

        InputAction moveAction;
        InputAction dodgeAction;
        InputAction attackAction;
        InputAction[] skillActions;

        void Awake()
        {
            moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            moveAction.AddBinding("<Gamepad>/leftStick");

            dodgeAction = new InputAction("Dodge", InputActionType.Button);
            dodgeAction.AddBinding("<Keyboard>/space");
            dodgeAction.AddBinding("<Gamepad>/buttonEast");

            attackAction = new InputAction("Attack", InputActionType.Button);
            attackAction.AddBinding("<Keyboard>/j");
            attackAction.AddBinding("<Gamepad>/buttonWest");

            dodgeAction.performed += _ => DodgePressed?.Invoke();
            attackAction.performed += _ => AttackPressed?.Invoke();

            skillActions = new InputAction[SkillBindings.Length];
            for (int i = 0; i < SkillBindings.Length; i++)
            {
                int index = i;
                var action = new InputAction("Skill" + (i + 1), InputActionType.Button);
                foreach (string binding in SkillBindings[i]) action.AddBinding(binding);
                action.performed += _ => SkillPressed?.Invoke(index);
                skillActions[i] = action;
            }
        }

        void OnEnable()
        {
            moveAction.Enable();
            dodgeAction.Enable();
            attackAction.Enable();
            foreach (InputAction action in skillActions) action.Enable();
        }

        void OnDisable()
        {
            moveAction.Disable();
            dodgeAction.Disable();
            attackAction.Disable();
            foreach (InputAction action in skillActions) action.Disable();
            Move = Vector2.zero;
        }

        void OnDestroy()
        {
            moveAction.Dispose();
            dodgeAction.Dispose();
            attackAction.Dispose();
            foreach (InputAction action in skillActions) action.Dispose();
        }

        void Update()
        {
            Move = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
        }
    }
}
