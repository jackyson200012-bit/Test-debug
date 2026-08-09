using System.Collections.Generic;
using UnityEngine;

namespace JayFos.Runtime
{
    [DisallowMultipleComponent]
    public sealed class AnimatorDriver : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private Transform body;

        [Header("Tuning")]
        [SerializeField, Min(0f)]
        private float speedDamping = 5f;

        private readonly HashSet<int> parameters = new HashSet<int>();
        private int pSpeed, pGrounded, pIsFalling, pJump, pLanded, pSprint, pCrouch, pMoveDirX, pMoveDirZ;
        private bool jumpParamExists, landedParamExists;
        private float smoothedSpeed;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (motor == null) motor = GetComponent<PlayerMotor>();
            if (body == null) body = transform;
            CacheParameters();
        }

        private void OnEnable()
        {
            if (motor != null)
            {
                motor.Jumped += OnJumped;
                motor.Landed += OnLanded;
            }
        }

        private void OnDisable()
        {
            if (motor != null)
            {
                motor.Jumped -= OnJumped;
                motor.Landed -= OnLanded;
            }
        }

        private void CacheParameters()
        {
            parameters.Clear();
            if (animator == null) return;
            foreach (AnimatorControllerParameter p in animator.parameters)
                parameters.Add(p.nameHash);

            pSpeed       = Animator.StringToHash("Speed");
            pGrounded    = Animator.StringToHash("Grounded");
            pIsFalling   = Animator.StringToHash("IsFalling");
            pJump        = Animator.StringToHash("Jump");
            pLanded      = Animator.StringToHash("Landed");
            pSprint      = Animator.StringToHash("Sprint");
            pCrouch      = Animator.StringToHash("Crouch");
            pMoveDirX    = Animator.StringToHash("MoveDirectionX");
            pMoveDirZ    = Animator.StringToHash("MoveDirectionZ");

            jumpParamExists = parameters.Contains(pJump);
            landedParamExists = parameters.Contains(pLanded);
        }

        private void Update()
        {
            if (animator == null || motor == null) return;

            smoothedSpeed = Mathf.MoveTowards(smoothedSpeed, motor.Speed, speedDamping * Time.deltaTime);
            SetFloat(pSpeed, smoothedSpeed);
            SetBool(pGrounded, motor.IsGrounded);
            SetBool(pIsFalling, motor.IsFalling);
            SetBool(pSprint, motor.IsSprinting);
            SetBool(pCrouch, motor.IsCrouching);

            if (body != null)
            {
                Vector3 local = body.InverseTransformDirection(motor.MoveDirectionWorld);
                SetFloat(pMoveDirX, local.x);
                SetFloat(pMoveDirZ, local.z);
            }
        }

        private void OnJumped()
        {
            if (jumpParamExists) animator.SetTrigger(pJump);
        }

        private void OnLanded(float impact)
        {
            if (landedParamExists) animator.SetTrigger(pLanded);
        }

        private void SetFloat(int hash, float value)
        {
            if (parameters.Contains(hash)) animator.SetFloat(hash, value);
        }

        private void SetBool(int hash, bool value)
        {
            if (parameters.Contains(hash)) animator.SetBool(hash, value);
        }
    }
}
