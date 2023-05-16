﻿using UnityEngine;
#if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED
using UnityEngine.InputSystem;
using System.Collections.Generic;
#endif

/* Note: animations are called via the controller for both the character and capsule using animator null checks
 */

namespace StarterAssets
{
    [RequireComponent(typeof(Rigidbody))]
#if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class ThirdPersonController : MonoBehaviour
    {
        [Header("Player")]
        [Tooltip("Mesh (Geometric interpretation) of the player to deduce info such as size")]
        public GameObject PlayerMesh;

        [Tooltip("Move speed of the character in m/s")]
        public float MoveSpeed = 2.0f;

        [Tooltip("Sprint speed of the character in m/s")]
        public float SprintMultiplier = 5.335f;

        [Tooltip("How fast the character turns to face movement direction")]
        [Range(0.0f, 0.3f)]
        public float RotationSmoothTime = 0.12f;

        [Tooltip("Acceleration and deceleration")]
        public float SpeedChangeRate = 10.0f;

        public AudioClip LandingAudioClip;
        public AudioClip[] FootstepAudioClips;
        [Range(0, 1)] public float FootstepAudioVolume = 0.5f;

        [Space(10)]
        [Tooltip("The height the player can jump")]
        public float JumpHeight = 1.2f;

        [Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
        public Vector3 Gravity = Vector3.up * -15.0f;

        [Space(10)]
        [Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
        public float JumpTimeout = 0.50f;

        [Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
        public float FallTimeout = 0.15f;

        [Header("Player Grounded")]
        [Tooltip("Gizmo geometry indicating the state of groundedness")]
        public GroundedGizmoType_ GroundedGizmoType;
        [Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
        public bool Grounded = true;
        [Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
        public bool DownwardRayIntersects = true;

        [Tooltip("Useful for rough ground (positive)")]
        public float GroundedOffset = 0.1f;

        [Tooltip("A downward ray is casted between two points of distance twice this point")]
        public float DownwardRayOffset = 0.1f;

        [Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
        public float GroundedRadius = 0.16f;

        [Tooltip("What layers the character uses as ground")]
        public LayerMask GroundLayers;

        [Header("Cinemachine")]
        [Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
        public GameObject CinemachineCameraTarget;

        [Tooltip("How far in degrees can you move the camera up")]
        public float TopClamp = 70.0f;

        [Tooltip("How far in degrees can you move the camera down")]
        public float BottomClamp = -30.0f;

        [Tooltip("Additional degress to override the camera. Useful for fine tuning camera position when locked")]
        public float CameraAngleOverride = 0.0f;

        [Tooltip("For locking the camera position on all axis")]
        public bool LockCameraPosition = false;

        [Header("General")]
        [Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
        public float Epsilon = 0.001f;

        // cinemachine
        private float _cinemachineTargetYaw;
        private float _cinemachineTargetPitch;

        // player
        private Vector3 _verticalVelocity;
        private Vector3 _horizontalVelocity;
        private Vector3 _gravity;
        private float _animationBlend;
        float _targetRotation = 0.0f;
        private float _rotationVelocity;
        private float _terminalVelocity = 53.0f;
        private Vector3 _upForOrientation;
        private Vector3 _upForMovement;
        // collision detection
        public enum GroundedGizmoType_
        {
            Line,
            Sphere,
        }
        RaycastHit _groundHit;
        // timeout deltatime
        private float _jumpTimeoutDelta;
        private float _fallTimeoutDelta;

        // animation IDs
        private int _animIDSpeed;
        private int _animIDGrounded;
        private int _animIDJump;
        private int _animIDFreeFall;
        private int _animIDMotionSpeed;

#if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED
        private PlayerInput _playerInput;
#endif
        private Animator _animator;
        private Rigidbody _rigidbody;
        private StarterAssetsInputs _input;
        private GameObject _mainCamera;
        private SkinnedMeshRenderer _renderer;
        private const float _threshold = 0.01f;

        private bool _hasAnimator;

        private bool IsCurrentDeviceMouse
        {
            get
            {
#if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED
                return _playerInput.currentControlScheme == "KeyboardMouse";
#else
				return false;
#endif
            }
        }


        private void Awake()
        {
            // get a reference to our main camera
            if (_mainCamera == null)
            {
                _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
            }
        }

        private void Start()
        {
            _cinemachineTargetYaw = CinemachineCameraTarget.transform.rotation.eulerAngles.y;

            _hasAnimator = TryGetComponent(out _animator);
            _rigidbody = GetComponent<Rigidbody>();
            _input = GetComponent<StarterAssetsInputs>();
            _renderer = PlayerMesh.GetComponent<SkinnedMeshRenderer>();
#if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED
            _playerInput = GetComponent<PlayerInput>();
#else   
			Debug.LogError( "Starter Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it");
#endif

            AssignAnimationIDs();

            // reset our timeouts on start
            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;
        }

        private void Update()
        {
            _hasAnimator = TryGetComponent(out _animator);

            _upForOrientation = transform.up;
            _upForMovement = (_groundHit.normal == Vector3.zero) ? transform.up : _groundHit.normal;

            GroundedCheck(_upForOrientation);
            SlopeCheck(_upForOrientation);
        }

        private void FixedUpdate()
        {
            setVerticalVelocity(_upForOrientation);
            setHorizontalVelocity(_upForOrientation, _upForMovement);
            Move();
        }

        private void LateUpdate()
        {
            CameraRotation();
        }

        private void AssignAnimationIDs()
        {
            _animIDSpeed = Animator.StringToHash("Speed");
            _animIDGrounded = Animator.StringToHash("Grounded");
            _animIDJump = Animator.StringToHash("Jump");
            _animIDFreeFall = Animator.StringToHash("FreeFall");
            _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
        }

        Vector3 orthogonalizeForwardReturnUp(Vector3 forward, Vector3 up)
        {
            return Vector3.Dot(forward, up) * up;
        }
        Vector3 orthogonalizeForwardReturnForward(Vector3 forward, Vector3 up)
        {
            return forward - orthogonalizeForwardReturnUp(forward, up);
        }
        Vector3 getAnOrthogonalVectorFromVector(Vector3 vector)
        {
            if (vector.x < Epsilon && vector.z < Epsilon)
            {
                return Vector3.forward;
            }
            return new Vector3(-vector.z, 0, vector.x).normalized;
        }
        (float, Vector3) getAngleBetweenTwoVectors(Vector3 fromDirection, Vector3 toDirection)
        {
            Quaternion rot2CameraForward = Quaternion.FromToRotation(fromDirection, toDirection);
            float rotation = 0.0f;
            Vector3 upAxis = Vector3.zero;
            rot2CameraForward.ToAngleAxis(out rotation, out upAxis);

            if (upAxis == -transform.up)
            {
                upAxis = transform.up;
                rotation = 360.0f - rotation;
            }
            if (rotation > 180.0f)
            {
                rotation -= 360.0f;
            }

            return (rotation, upAxis);
        }
        private void GroundedCheck(Vector3 upForOrientation)
        {
            // set sphere position, with offset
            Vector3 spherePosition = transform.position + upForOrientation * GroundedOffset;
            Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers,
                QueryTriggerInteraction.Ignore);

            // update animator if using character
            if (_hasAnimator)
            {
                _animator.SetBool(_animIDGrounded, Grounded);
            }
        }
        private void SlopeCheck(Vector3 upForOrientation)
        {
            // set sphere position, with offset
            DownwardRayIntersects = Physics.Raycast(
                transform.position + upForOrientation * DownwardRayOffset,
                -upForOrientation,
                out _groundHit, 2.0f * DownwardRayOffset, GroundLayers
            );
        }

        private void CameraRotation()
        {
            // if there is an input and camera position is not fixed
            if (_input.look.sqrMagnitude >= _threshold && !LockCameraPosition)
            {
                //Don't multiply mouse input by Time.deltaTime;
                float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

                _cinemachineTargetYaw += _input.look.x * deltaTimeMultiplier;
                _cinemachineTargetPitch += _input.look.y * deltaTimeMultiplier;
            }

            // clamp our rotations so our values are limited 360 degrees
            _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
            _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

            // Cinemachine will follow this target
            CinemachineCameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch + CameraAngleOverride,
                _cinemachineTargetYaw, 0.0f);
        }

        private void Move()
        {
            Vector3 _velocity = _horizontalVelocity + _verticalVelocity;
            // Debug.Log(
            //     string.Format(
            //         "_horizontalSpeed:{0}, _verticalSpeed:{1}, targetDirection:{2}, _velocity:{3}",
            //         _horizontalSpeed,
            //         _verticalVelocity,
            //         targetDirection,
            //         _velocity
            //         )
            //     );
            // move the player
            _rigidbody.velocity = _velocity;
        }
        Vector3 getHorizontalDirection(in Vector3 upForOrientation, in Vector3 upForMovement)
        {
            Vector3 refVectorOnVWPlane = getAnOrthogonalVectorFromVector(upForOrientation);
            // note: Vector2's != operator uses approximation so is not floating point error prone, and is cheaper than magnitude
            // if there is a move input rotate player when the player is moving
            if (_input.move != Vector2.zero)
            {
                Vector3 _mainCameraForward = orthogonalizeForwardReturnForward(_mainCamera.transform.forward, upForOrientation).normalized;

                if (_mainCameraForward == Vector3.zero)
                {
                    _mainCameraForward = Vector3.Cross(upForOrientation, transform.right).normalized;
                }

                (float _mainCameraY, Vector3 _mainCameraUp) = getAngleBetweenTwoVectors(refVectorOnVWPlane, _mainCameraForward);
                (float _transformY, Vector3 _transformUp) = getAngleBetweenTwoVectors(refVectorOnVWPlane, transform.forward);
                _targetRotation = Mathf.Atan2(_input.move.x, _input.move.y) * Mathf.Rad2Deg + _mainCameraY;

                float _smoothedTargetRotation = Mathf.SmoothDampAngle(_transformY, _targetRotation, ref _rotationVelocity,
                    RotationSmoothTime);
                Vector3 smoothedTargetDirection = Quaternion.AngleAxis(_smoothedTargetRotation, upForOrientation) * refVectorOnVWPlane;
                transform.LookAt(transform.position + smoothedTargetDirection, upForOrientation);
            }
            Vector3 _targetDirectionForOrientation = (Quaternion.AngleAxis(_targetRotation, upForOrientation) * refVectorOnVWPlane).normalized;
            Vector3 _targetDirectionForMovement = orthogonalizeForwardReturnForward(_targetDirectionForOrientation, upForMovement).normalized;
            return _targetDirectionForMovement;
        }
        void setHorizontalVelocity(in Vector3 upForOrientation, in Vector3 upForMovement)
        {
            float _horizontalSpeed;
            float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;
            // set target speed based on move speed, sprint speed and if sprint is pressed
            float targetSpeed = MoveSpeed;
            if (_input.sprint) targetSpeed *= SprintMultiplier;

            // a simplistic acceleration and deceleration designed to be easy to remove, replace, or iterate upon

            // note: Vector2's == operator uses approximation so is not floating point error prone, and is cheaper than magnitude
            // if there is no input, set the target speed to 0
            if (_input.move == Vector2.zero) targetSpeed = 0.0f;

            // a reference to the players current horizontal velocity
            float currentHorizontalSpeed = orthogonalizeForwardReturnForward(_rigidbody.velocity, upForMovement).magnitude;

            float speedOffset = 0.1f;

            // accelerate or decelerate to target speed
            if (currentHorizontalSpeed < targetSpeed - speedOffset ||
                currentHorizontalSpeed > targetSpeed + speedOffset)
            {
                // creates curved result rather than a linear one giving a more organic speed change
                // note T in Lerp is clamped, so we don't need to clamp our speed
                _horizontalSpeed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude,
                    Time.deltaTime * SpeedChangeRate);
                // round speed to 3 decimal places
                _horizontalSpeed = Mathf.Round(_horizontalSpeed * 1000f) / 1000f;
            }
            else
            {
                _horizontalSpeed = targetSpeed;
            }
            _animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Time.deltaTime * SpeedChangeRate);
            if (_animationBlend < 0.01f) _animationBlend = 0f;

            // update animator if using character
            if (_hasAnimator)
            {
                _animator.SetFloat(_animIDSpeed, _animationBlend);
                _animator.SetFloat(_animIDMotionSpeed, inputMagnitude);
            }

            Vector3 targetDirection = getHorizontalDirection(upForOrientation, upForMovement);

            _horizontalVelocity = targetDirection * _horizontalSpeed;
        }
        void setVerticalVelocity(Vector3 upForOrientation)
        {
            if (Grounded)
            {
                // reset the fall timeout timer
                _fallTimeoutDelta = FallTimeout;

                // update animator if using character
                if (_hasAnimator)
                {
                    _animator.SetBool(_animIDJump, false);
                    _animator.SetBool(_animIDFreeFall, false);
                }

                Vector3 _gravityUp = orthogonalizeForwardReturnUp(Gravity, upForOrientation);
                if (_gravityUp.normalized == -upForOrientation.normalized)
                {
                    _gravity = Gravity - _gravityUp;
                }
                Vector3 _verticalVelocityUp = orthogonalizeForwardReturnUp(_verticalVelocity, upForOrientation);
                if (_verticalVelocityUp.normalized == -upForOrientation.normalized)
                {
                    _verticalVelocity -= _verticalVelocityUp;
                }
                // Jump
                if (_input.jump && _jumpTimeoutDelta <= 0.0f)
                {
                    // the square root of H * -2 * G = how much velocity needed to reach desired height
                    _verticalVelocity = Mathf.Sqrt(2f * JumpHeight * Gravity.magnitude) * upForOrientation;
                    // update animator if using character
                    if (_hasAnimator)
                    {
                        _animator.SetBool(_animIDJump, true);
                    }
                }

                // jump timeout
                if (_jumpTimeoutDelta >= 0.0f)
                {
                    _jumpTimeoutDelta -= Time.deltaTime;
                }
            }
            else
            {
                // reset the jump timeout timer
                _jumpTimeoutDelta = JumpTimeout;

                // fall timeout
                if (_fallTimeoutDelta >= 0.0f)
                {
                    _fallTimeoutDelta -= Time.deltaTime;
                }
                else
                {
                    // update animator if using character
                    if (_hasAnimator)
                    {
                        _animator.SetBool(_animIDFreeFall, true);
                    }
                }

                _gravity = Gravity;
                // if we are not grounded, do not jump
                _input.jump = false;
            }
            _verticalVelocity += _gravity * Time.deltaTime;
        }

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }

        private void OnDrawGizmosSelected()
        {
            if (_renderer == null)
            {
                _renderer = PlayerMesh.GetComponent<SkinnedMeshRenderer>();
            }
            Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
            Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);



            // when selected, draw a gizmo in the position of, and matching radius of, the grounded collider

            switch (GroundedGizmoType)
            {
                default://Line
                    if (DownwardRayIntersects) Gizmos.color = transparentGreen;
                    else Gizmos.color = transparentRed;

                    float _sizeY2 = _renderer.bounds.size.y / 2;
                    Vector3 from = transform.position + transform.up * DownwardRayOffset;
                    Vector3 to = transform.position - transform.up * DownwardRayOffset;
                    Gizmos.DrawLine(from, to);
                    break;
                case GroundedGizmoType_.Sphere:
                    if (Grounded) Gizmos.color = transparentGreen;
                    else Gizmos.color = transparentRed;

                    Vector3 spherePosition = transform.position + transform.up * GroundedOffset;
                    Gizmos.DrawSphere(spherePosition, GroundedRadius);
                    break;
            }
        }

        private void OnFootstep(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                if (FootstepAudioClips.Length > 0)
                {
                    var index = Random.Range(0, FootstepAudioClips.Length);
                    AudioSource.PlayClipAtPoint(FootstepAudioClips[index], transform.TransformPoint(_rigidbody.position), FootstepAudioVolume);
                }
            }
        }

        private void OnLand(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                AudioSource.PlayClipAtPoint(LandingAudioClip, transform.TransformPoint(_rigidbody.position), FootstepAudioVolume);
            }
        }
    }
}