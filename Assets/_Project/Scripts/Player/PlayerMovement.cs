using System;
using ShadowBlade.Combat;
using UnityEngine;

namespace ShadowBlade.Player
{
    /// <summary>
    /// Déplacement du héros (relatif à la caméra) et esquive en roulade.
    /// Pendant la roulade, le héros est invincible (IsInvulnerable) et ne peut pas changer de direction.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(PlayerInputReader))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Déplacement")]
        [SerializeField] float moveSpeed = 6f;
        [Tooltip("Vitesse de rotation en degrés par seconde.")]
        [SerializeField] float turnSpeed = 900f;
        [SerializeField] float gravity = -25f;

        [Header("Esquive (roulade)")]
        [SerializeField] float dodgeDistance = 5f;
        [SerializeField] float dodgeDuration = 0.35f;
        [SerializeField] float dodgeCooldown = 0.6f;
        [Tooltip("Modèle visuel qui tourne sur lui-même pendant la roulade (optionnel).")]
        [SerializeField] Transform visual;
        [Tooltip("Fait tourner le visuel pendant la roulade. À désactiver si le modèle a sa propre animation de roulade.")]
        [SerializeField] bool proceduralRoll = true;

        public bool IsDodging { get; private set; }
        public bool IsInvulnerable => IsDodging || SkillInvulnerable;
        /// <summary>Invincibilité accordée par une compétence (ex : pendant la Charge).</summary>
        public bool SkillInvulnerable { get; set; }
        /// <summary>0 = esquive disponible, 1 = vient d'être utilisée. Pratique pour l'UI.</summary>
        public float DodgeCooldown01 => dodgeCooldown > 0f ? Mathf.Clamp01(cooldownTimer / dodgeCooldown) : 0f;
        /// <summary>Vitesse horizontale normalisée (0 à 1), pour piloter un Animator plus tard.</summary>
        public float Speed01 { get; private set; }
        /// <summary>Direction voulue par le joueur (joystick), relative à la caméra.</summary>
        public Vector3 MoveDirection => GetCameraRelativeDirection(input.Move);

        /// <summary>Bloque la marche (ex : pendant une attaque). La roulade reste possible.</summary>
        public bool MovementLocked { get; set; }
        /// <summary>Vitesse imposée pendant que la marche est bloquée (ex : avancée d'un coup d'épée).</summary>
        public Vector3 ExternalVelocity { get; set; }

        public event Action DodgeStarted;
        public event Action DodgeEnded;

        CharacterController controller;
        PlayerInputReader input;
        Transform cameraTransform;

        float verticalVelocity;
        float dodgeTimer;
        float cooldownTimer;
        Vector3 dodgeDirection;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            input = GetComponent<PlayerInputReader>();
            if (Camera.main != null) cameraTransform = Camera.main.transform;
            if (TryGetComponent(out Health health)) health.InvulnerabilityCheck = () => IsInvulnerable;
        }

        void OnEnable() => input.DodgePressed += TryDodge;
        void OnDisable() => input.DodgePressed -= TryDodge;

        void Update()
        {
            float dt = Time.deltaTime;
            cooldownTimer = Mathf.Max(0f, cooldownTimer - dt);

            Vector3 horizontalVelocity;
            if (IsDodging)
            {
                horizontalVelocity = UpdateDodge(dt);
            }
            else if (MovementLocked)
            {
                Speed01 = 0f;
                horizontalVelocity = ExternalVelocity;
            }
            else
            {
                horizontalVelocity = UpdateWalk(dt);
            }

            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f;
            verticalVelocity += gravity * dt;

            controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * dt);
        }

        Vector3 UpdateWalk(float dt)
        {
            Vector3 direction = GetCameraRelativeDirection(input.Move);
            Speed01 = direction.magnitude;

            if (Speed01 > 0.01f)
            {
                Quaternion target = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * dt);
            }

            return direction * moveSpeed;
        }

        Vector3 UpdateDodge(float dt)
        {
            dodgeTimer += dt;
            float t = Mathf.Clamp01(dodgeTimer / dodgeDuration);

            // Vitesse décroissante (2 * (1 - t)) : rapide au départ, puis ralentit.
            // La moyenne vaut 1, donc la distance parcourue est exactement dodgeDistance.
            float speed = dodgeDistance / dodgeDuration * 2f * (1f - t);

            if (visual != null && proceduralRoll)
                visual.localRotation = Quaternion.Euler(360f * Mathf.SmoothStep(0f, 1f, t), 0f, 0f);

            if (t >= 1f) EndDodge();

            return dodgeDirection * speed;
        }

        void TryDodge()
        {
            if (IsDodging || cooldownTimer > 0f) return;

            Vector3 direction = GetCameraRelativeDirection(input.Move);
            dodgeDirection = direction.sqrMagnitude > 0.01f ? direction.normalized : transform.forward;
            transform.rotation = Quaternion.LookRotation(dodgeDirection);

            IsDodging = true;
            dodgeTimer = 0f;
            Speed01 = 1f;
            DodgeStarted?.Invoke();
        }

        void EndDodge()
        {
            IsDodging = false;
            cooldownTimer = dodgeCooldown;
            if (visual != null) visual.localRotation = Quaternion.identity;
            DodgeEnded?.Invoke();
        }

        /// <summary>Tourne immédiatement le héros vers une direction (à plat).</summary>
        public void FaceDirection(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(direction.normalized);
        }

        Vector3 GetCameraRelativeDirection(Vector2 stick)
        {
            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;

            if (cameraTransform != null)
            {
                forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
                right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
            }

            return Vector3.ClampMagnitude(forward * stick.y + right * stick.x, 1f);
        }
    }
}
