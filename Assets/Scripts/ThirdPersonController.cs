using UnityEngine;
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
        public GameObject ArmatureMesh;

        [Tooltip("Move speed of the character in m/s")]
        public float MoveSpeed = 2.0f;

        [Tooltip("Sprint speed of the character in m/s")]
        public float SprintMultiplier = 5.335f;

        [Tooltip("How fast the character turns to face movement direction")]
        [Range(0.0f, 0.3f)]
        public float RotationSmoothTime = 0.12f;

        [Tooltip("Acceleration and deceleration")]
        public float SpeedChangeRate = 10.0f;
        [Tooltip("Up vector used for orientation. Up vector for movement is calculated using the ground normal")]
        public Vector3 UpForOrientation = Vector3.up;
        [Tooltip("Forward vector used for orientation.")]
        public Vector3 ForwardForOrientation = Vector3.forward;

        public AudioClip LandingAudioClip;
        public AudioClip[] FootstepAudioClips;
        [Range(0, 1)] public float FootstepAudioVolume = 0.5f;

        [Space(10)]
        [Tooltip("The height the player can jump")]
        public float JumpHeight = 1.2f;

        [Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
        public Vector3 Gravity = Physics.gravity;

        [Space(10)]
        [Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
        public float JumpTimeout = 0.50f;

        [Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
        public float FallTimeout = 0.15f;
        [Tooltip("Time required to pass before being able to climb again. Set to 0f to instantly climb again")]
        public float ClimbTimeout = 1.0f;

        [Header("Rigidbody Control and Gizmo Variables")]
        [Tooltip("Whether to show grounded check sphere gizmo")]
        public bool ShowGroundedCheck = true;
        [Tooltip("Whether to show slope check line gizmo")]
        public bool ShowSlopeCheck = true;
        [Tooltip("Whether to show climb check line gizmo")]
        public bool ShowClimbCheck = true;
        [Tooltip("Whether to show orient check sphere gizmo")]
        public bool ShowOrientCheck = true;
        [Tooltip("If the character is grounded or not.")]
        public bool Grounded = true;
        [Tooltip("Downward ray used for slope check.")]
        public bool SlopeRayIntersects = true;
        [Tooltip("Downward ray used for orient check.")]
        public bool OrientRayIntersects = true;
        [Tooltip("Forward ray used for climb check.")]
        public bool ClimbRayIntersects = true;

        [Tooltip("Useful for rough ground (positive)")]
        public float GroundedOffset = 0.1f;

        [Tooltip("A downward ray is casted between two points of distance twice this point")]
        public float SlopeRayOffset = 0.1f;

        [Tooltip("Useful for orienting while climbing (positive)")]
        public float OrientOffset = 0.1f;

        [Tooltip("The radius of the grounded check. Should match the radius of the CapsuleCollider")]
        public float GroundedRadius = 0.16f;

        [Tooltip("The radius of the orient check. Should be more than the radius of the CapsuleCollider")]
        public float OrientRadius = 0.4f;

        [Tooltip("What layers the character uses as ground")]
        public LayerMask GroundLayers;

        [Header("Player Climbing")]
        [Tooltip("Useful for climbing (positive)")]
        public float ClimbOffset = 0.1f;
        [Tooltip("Orient While Climbing?")]
        public bool OrientWhileClimbing = true;

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
        [Tooltip("Generic epsilon value used for approximations to alleviate floating point errors")]
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
        private Vector3 _upForOrientation;
        private Vector3 _upForMovement;
        private Vector3 _refVectorOnVWPlane = Vector3.zero;
        // collision detection
        RaycastHit _groundHit;
        RaycastHit _climbHit;
        RaycastHit _orientHit;
        // timeout deltatime
        private float _climbTimeoutDelta;
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
            if (_mainCamera == null)
            {
                _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
            }
            if (ArmatureMesh == null)
            {
                ArmatureMesh = GameObject.FindGameObjectWithTag("ArmatureMesh");
            }
        }

        private void Start()
        {
            _cinemachineTargetYaw = CinemachineCameraTarget.transform.rotation.eulerAngles.y;

            _hasAnimator = TryGetComponent(out _animator);
            _rigidbody = GetComponent<Rigidbody>();
            _input = GetComponent<StarterAssetsInputs>();
            _renderer = ArmatureMesh.GetComponent<SkinnedMeshRenderer>();
#if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED
            _playerInput = GetComponent<PlayerInput>();
#else   
			Debug.LogError( "Starter Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it");
#endif

            AssignAnimationIDs();

            // reset our timeouts on start
            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;

            Orient(ForwardForOrientation, UpForOrientation);
        }

        private void Update()
        {
            _hasAnimator = TryGetComponent(out _animator);

            _upForOrientation = transform.up;

            GroundedCheck(_upForOrientation);
            SlopeCheck(_upForOrientation);
            ClimbCheck(_upForOrientation);

            SetUpForMovement(ref _upForOrientation, out _upForMovement);
            UpdateCameraPosition();
        }


        private void FixedUpdate()
        {
            setVerticalVelocity(_upForOrientation);
            setHorizontalVelocity(_upForOrientation, _upForMovement);
            Move();
        }

        private void LateUpdate()
        {
            CameraRotation(_upForOrientation);
        }
        private void SetUpForMovement(ref Vector3 upForOrientation, out Vector3 upForMovement)
        {
            upForMovement =
            (_groundHit.normal == Vector3.zero) ?
            upForOrientation :
            _groundHit.normal;

            if (ClimbRayIntersects && _climbTimeoutDelta <= 0.0f)
            {
                Vector3 _climbhitNormalClean = vectorCleaned(_climbHit.normal);
                // Debug.Log(string.Format("{0} {1}", _climbHit.normal, _climbhitNormalClean));
                upForMovement = _climbhitNormalClean;

                if (OrientWhileClimbing)
                {
                    OrientWithGravity(out upForOrientation, upForMovement);
                }
                _climbTimeoutDelta = ClimbTimeout;
            }
            if (OrientRayIntersects)
            {
                Debug.Log(_orientHit.normal);
            }
            _climbTimeoutDelta -= Time.deltaTime;
        }
        private void Orient(Vector3 forward, Vector3 up)
        {
            transform.LookAt(transform.position + forward, up);
        }
        private void OrientWithGravity(out Vector3 upForOrientation, in Vector3 up)
        {
            upForOrientation = up;
            Vector3 newForward = Vector3.Cross(transform.right, upForOrientation);
            Gravity = -Gravity.magnitude * upForOrientation;
            Orient(newForward, upForOrientation);
        }
        //Zeroes out coordinates below a threshold
        private Vector3 vectorCleaned(in Vector3 vector)
        {
            Vector3 tmp = vector;
            if (Mathf.Abs(tmp.x) < Epsilon)
            {
                tmp.x = 0;
            }
            if (Mathf.Abs(tmp.y) < Epsilon)
            {
                tmp.y = 0;
            }
            if (Mathf.Abs(tmp.z) < Epsilon)
            {
                tmp.z = 0;
            }
            return tmp;
        }
        // CONTROL METHODS
        private void ClimbCheck(in Vector3 upForOrientation)
        {
            float playerHeightY = _renderer.bounds.size.y;
            float playerHeightZhalf = _renderer.bounds.size.z / 2;
            ClimbRayIntersects = Physics.Raycast(
                transform.position + upForOrientation * (playerHeightY - GroundedOffset) + transform.forward * (playerHeightZhalf - ClimbOffset),
                transform.forward,
                out _climbHit, 2.0f * ClimbOffset, GroundLayers
            );
        }
        private void GroundedCheck(in Vector3 upForOrientation)
        {
            Grounded = Physics.CheckSphere(
                transform.position + upForOrientation * GroundedOffset,
                GroundedRadius,
                GroundLayers,
                QueryTriggerInteraction.Ignore
                );

            // update animator if using character
            if (_hasAnimator)
            {
                _animator.SetBool(_animIDGrounded, Grounded);
            }
        }
        private void OrientCheck(in Vector3 upForOrientation)
        {
            OrientRayIntersects = Physics.SphereCast(
                transform.position + upForOrientation * OrientOffset,
                OrientRadius,
                -upForOrientation,
                out _orientHit,
                2.0f * OrientOffset,
                GroundLayers
                );

            // update animator if using character
            if (_hasAnimator)
            {
                _animator.SetBool(_animIDGrounded, Grounded);
            }
        }
        private void SlopeCheck(in Vector3 upForOrientation)
        {
            SlopeRayIntersects = Physics.Raycast(
                transform.position + upForOrientation * SlopeRayOffset,
                -upForOrientation,
                out _groundHit, 2.0f * SlopeRayOffset, GroundLayers
            );
        }
        private void UpdateCameraPosition()
        {
            float playerHeightHalf = _renderer.bounds.size.y / 2;
            CinemachineCameraTarget.transform.position = transform.position + _upForOrientation * playerHeightHalf;
        }

        private void AssignAnimationIDs()
        {
            _animIDSpeed = Animator.StringToHash("Speed");
            _animIDGrounded = Animator.StringToHash("Grounded");
            _animIDJump = Animator.StringToHash("Jump");
            _animIDFreeFall = Animator.StringToHash("FreeFall");
            _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
        }

        Vector3 orthogonalizeForwardReturnUp(in Vector3 forward, in Vector3 up)
        {
            return Vector3.Dot(forward, up) * up;
        }
        Vector3 orthogonalizeForwardReturnForward(in Vector3 forward, in Vector3 up)
        {
            return forward - orthogonalizeForwardReturnUp(forward, up);
        }
        Vector3 getAnOrthogonalVectorFromVector(in Vector3 vector)
        {
            if (Mathf.Abs(vector.x) < Epsilon && Mathf.Abs(vector.z) < Epsilon)
            {
                return Vector3.forward;
            }
            return new Vector3(-vector.z, 0, vector.x).normalized;
        }
        (float, Vector3) getAngleBetweenTwoVectors(in Vector3 fromDirection, in Vector3 toDirection, in Vector3 up)
        {
            Quaternion rot2CameraForward = Quaternion.FromToRotation(fromDirection, toDirection);
            float rotation = 0.0f;
            Vector3 upAxis = Vector3.zero;
            rot2CameraForward.ToAngleAxis(out rotation, out upAxis);

            if (upAxis == -up)
            {
                upAxis = up;
                rotation = 360.0f - rotation;
            }
            if (rotation > 180.0f)
            {
                rotation -= 360.0f;
            }

            return (rotation, upAxis);
        }


        private static float ClampAngle(float lfAngle, float lfMin = float.MinValue, float lfMax = float.MaxValue)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }

        private void CameraRotation(in Vector3 upForOrientation)
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
            _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw);
            _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

            Vector3 _newRight = orthogonalizeForwardReturnForward(
                CinemachineCameraTarget.transform.right,
                upForOrientation
                );
            Quaternion _transformCam =
            Quaternion.AngleAxis(
                _cinemachineTargetPitch + CameraAngleOverride,
                CinemachineCameraTarget.transform.right
            ) * Quaternion.AngleAxis(_cinemachineTargetYaw, upForOrientation);


            Vector3 _newForward = _transformCam * _refVectorOnVWPlane;
            Vector3 _newUp = _transformCam * upForOrientation;

            CinemachineCameraTarget.transform.LookAt(
                CinemachineCameraTarget.transform.position + _newForward,
                _newUp
                );
        }

        private void Move()
        {
            Vector3 _velocity = _horizontalVelocity + _verticalVelocity;
            // move the player
            _rigidbody.velocity = _velocity;
        }
        Vector3 getHorizontalDirection(in Vector3 upForOrientation, in Vector3 upForMovement)
        {
            _refVectorOnVWPlane = getAnOrthogonalVectorFromVector(upForOrientation);

            // note: Vector2's != operator uses approximation so is not floating point error prone, and is cheaper than magnitude
            // if there is a move input rotate player when the player is moving
            if (_input.move != Vector2.zero)
            {
                Vector3 _mainCameraForward = orthogonalizeForwardReturnForward(_mainCamera.transform.forward, upForOrientation).normalized;
                if (_mainCameraForward == Vector3.zero)
                {
                    _mainCameraForward = Vector3.Cross(upForOrientation, transform.right).normalized;
                }

                (float _mainCameraY, Vector3 _mainCameraUp) = getAngleBetweenTwoVectors(
                    _refVectorOnVWPlane,
                    _mainCameraForward,
                    upForOrientation
                    );
                (float _transformY, Vector3 _transformUp) = getAngleBetweenTwoVectors(
                    _refVectorOnVWPlane,
                    transform.forward,
                    upForOrientation
                    );
                _targetRotation = Mathf.Atan2(_input.move.x, _input.move.y) * Mathf.Rad2Deg + _mainCameraY;

                float _smoothedTargetRotation = Mathf.SmoothDampAngle(_transformY, _targetRotation, ref _rotationVelocity,
                    RotationSmoothTime);
                Vector3 smoothedTargetDirection = Quaternion.AngleAxis(_smoothedTargetRotation, upForOrientation) * _refVectorOnVWPlane;
                Orient(smoothedTargetDirection, upForOrientation);
            }
            return Vector3.Cross(transform.right, upForMovement).normalized;
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
            float currentHorizontalSpeed = _horizontalVelocity.magnitude;

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
        void setVerticalVelocity(in Vector3 upForOrientation)
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
                    // the square root of 2 * G * H = how much velocity needed to reach desired height
                    _verticalVelocity = Mathf.Sqrt(2f * Gravity.magnitude * JumpHeight) * upForOrientation;
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



        private void OnDrawGizmosSelected()
        {
            Vector3 from, to;
            if (_renderer == null)
            {
                _renderer = ArmatureMesh.GetComponent<SkinnedMeshRenderer>();
            }
            Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
            Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

            // when selected, draw a gizmo in the position of, and matching radius of, the grounded collider
            if (ShowGroundedCheck)
            {
                if (Grounded) Gizmos.color = transparentGreen;
                else Gizmos.color = transparentRed;

                Vector3 spherePosition = transform.position + _upForOrientation * GroundedOffset;
                Gizmos.DrawSphere(spherePosition, GroundedRadius);
            }
            if (ShowSlopeCheck)
            {
                if (SlopeRayIntersects) Gizmos.color = transparentGreen;
                else Gizmos.color = transparentRed;

                float _sizeY2 = _renderer.bounds.size.y / 2;
                from = transform.position + _upForOrientation * SlopeRayOffset;
                to = transform.position - _upForOrientation * SlopeRayOffset;
                Gizmos.DrawLine(from, to);
            }
            if (ShowOrientCheck)
            {
                if (OrientRayIntersects) Gizmos.color = transparentGreen;
                else Gizmos.color = transparentRed;

                from = transform.position + _upForOrientation * OrientOffset;
                to = transform.position - _upForOrientation * OrientOffset;
                Gizmos.DrawLine(from, to);
                Gizmos.DrawSphere(to, OrientRadius);
            }
            if (ShowClimbCheck)
            {
                if (ClimbRayIntersects) Gizmos.color = transparentGreen;
                else Gizmos.color = transparentRed;
                float playerHeightY = _renderer.bounds.size.y;
                float playerHeightZhalf = _renderer.bounds.size.z / 2;
                from = transform.position + _upForOrientation * (playerHeightY - GroundedOffset) + transform.forward * (playerHeightZhalf - ClimbOffset);
                to = transform.position + _upForOrientation * (playerHeightY - GroundedOffset) + transform.forward * (playerHeightZhalf + ClimbOffset);
                Gizmos.DrawLine(from, to);
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