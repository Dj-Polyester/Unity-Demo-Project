using UnityEngine;
#if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED
using UnityEngine.InputSystem;
using System;
#endif

namespace StarterAssets
{
    [RequireComponent(typeof(Rigidbody))]
#if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class ThirdPersonController : MonoBehaviour
    {
        [Header("Generic")]
        [Tooltip("Generic epsilon value used for approximations to alleviate floating point errors")]
        public float Epsilon = 0.001f;
        [Tooltip("Mesh (Geometric interpretation) of the player to deduce info such as size")]
        public GameObject ArmatureMesh;

        [Space(10)]
        [Header("Orientation")]
        [Tooltip("Up vector used for orientation. Up vector for movement is calculated using the ground normal")]
        public Vector3 UpForOrientation = Vector3.up;
        [Tooltip("Forward vector used for orientation.")]
        public Vector3 ForwardForOrientation = Vector3.forward;

        [Space(10)]
        [Header("Player")]
        [Tooltip("Health of the player")]
        public float Health = 100.0f;

        [Space(10)]
        [Header("High Fall")]
        [Tooltip("The minimum height that the player takes a damage when fallen from. The player transitions into special air and hard landing animations after this height")]
        public float FallDamageHeight = 2.0f;
        [Tooltip("Timeout in seconds before the player can do a roll on the ground")]
        public float RollTimeout = 5.0f;

        [Header("Basic Movement")]
        [Tooltip("Whether the player should move on input")]
        public bool CanMove = true;
        [Tooltip("Move speed of the character in m/s")]
        public float MoveSpeed = 2.0f;
        [Tooltip("Sprint speed of the character in m/s when standing")]
        public float MoveSprintMultiplier = 5.335f;
        [Tooltip("How fast the character turns to face movement direction")]
        [Range(0.0f, 0.3f)]
        public float RotationSmoothTime = 0.12f;
        [Tooltip("Acceleration and deceleration")]
        public float SpeedChangeRate = 10.0f;
        [Tooltip("Power in the math expression used for interpolating speed values. Linear if 1, quadratic if 2 ,cubic if 3 and so on")]
        public uint SpeedChangeRatePower = 1;
        [Header("Jumping")]
        [Tooltip("Whether the player should jump on input")]
        public bool CanJump = true;
        [Tooltip("The height the player can jump")]
        public float JumpHeight = 1.2f;
        [Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
        public Vector3 Gravity = Physics.gravity;
        [Tooltip("Time required to pass in seconds before being able to jump again. Set to 0f to instantly jump again")]
        public float JumpTimeout = 0f;
        [Tooltip("Time required to pass in seconds before entering the fall state. Useful for walking down stairs")]
        public float FallTimeout = 0.15f;

        [Space(10)]
        [Header("Climbing")]
        [Tooltip("Climb speed of the character in m/s")]
        public float ClimbSpeed = 2.0f;
        [Tooltip("Sprint speed of the character in m/s when climbing")]
        public float ClimbSprintMultiplier = 2f;
        [Tooltip("Time required to pass in seconds before being able to climb again.")]
        public float ClimbTimeout = 0.15f;
        [Tooltip("Upward offset from which the climb ray is fired from")]
        public float ClimbRayOffset = 0.1f;
        [Tooltip("Length of the climb ray")]
        public float ClimbRayLength = 1f;
        [Tooltip("Rate of change of up for orientation change")]
        public float SecondsToCompleteUpForOrientationAngleChange = 5.0f;
        [Tooltip("Whether the player is climbing")]
        public bool Climbing = false;

        [Space(10)]
        [Header("Orienting (aka Climbing on convex surfaces)")]
        [Tooltip("Upward offset from which the orient ray is fired from")]
        public float OrientRayOffset = 0.5f;
        [Tooltip("Length of the orient ray")]
        public float OrientRayLength = 0.5f;
        [Tooltip("The radius of the orient check. Should be more than the radius of the CapsuleCollider")]
        public float OrientRadius = 0.2f;
        [Tooltip("Whether the player is orienting")]
        public bool Orienting = false;

        [Space(10)]
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

        [Space(10)]
        [Header("Rigidbody Check and Gizmo")]
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
        [Tooltip("Forward ray used for climb check.")]
        public bool ClimbRayIntersects = true;
        [Tooltip("Downward ray used for orient check.")]
        public bool OrientRayIntersects = true;
        [Tooltip("Useful for rough ground")]
        public float GroundedOffset = 0.1f;
        [Tooltip("The radius of the grounded check. Should match the radius of the CapsuleCollider")]
        public float GroundedRadius = 0.16f;
        [Tooltip("Upward offset from which the slope ray is fired from")]
        public float SlopeRayOffset = 0.1f;
        [Tooltip("Length of the slope ray. Should be more than jump height")]
        public float SlopeRayLength = 1f;
        [Tooltip("What layers the character uses as ground")]
        public LayerMask GroundLayers;

        [Space(10)]
        [Header("Audio")]
        public AudioClip LandingAudioClip;
        public AudioClip[] FootstepAudioClips;
        [Range(0, 1)] public float FootstepAudioVolume = 0.5f;

        [Space(10)]
        [Header("Animation")]
        [Tooltip("Time in seconds between two consecutive idle states")]
        public float SecondsBetweenIdleStates = 3f;
        [Tooltip("Duration time of each idle state in seconds")]
        public float[] SecondsInTheIdleStates = { 30f, 3f, 1f };
        [Tooltip("Power in the math expression used for blending between idle states. Linear if 1, quadratic if 2 ,cubic if 3 and so on")]
        public uint IdleStateChangeRatePower = 10;
        [Tooltip("The duration in seconds for rejecting input while animating. The default duration of animations are 2 seconds, which is preferred.")]
        public float LockWhileAnimatingTimeout = 2.0f;
        ///GENERIC
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
        private uint _currentIdleState;
        private uint _nextIdleState;
        ///CAMERA
        private float _cinemachineTargetYaw;
        private float _cinemachineTargetPitch;
        ///BASIC MOVEMENT
        private Vector3 _verticalVelocity;
        private Vector3 _horizontalVelocity;
        private Vector3 _gravity;
        private Vector3 _upForOrientation;
        private Vector3 _upForMovement;
        private Vector3 _upForOrientationCamera;
        private Vector3 _upForOrientationTarget;
        private Vector3 _axis2rotateUpForOrientationAround;
        private Vector3 _refVectorOnVWPlane;
        private Vector2 _oldInputMove = Vector2.zero;
        private bool _oldInputSprint = false;
        private bool _oldGrounded = true;
        private bool _highFall = false;
        private bool _crouchPressedWhenRolling = false;
        private float _lastHorizontalSpeedBeforeJump = 0;
        private float _animationBlend;
        private float _targetRotation = 0.0f;
        private float _rotationVelocity;
        private float _upForOrientationAngleChangePerTick;
        //RAYCASTS
        private RaycastHit _slopeHit;
        private RaycastHit _climbHit;
        private RaycastHit _orientHit;
        //TIMEOUT
        private float _jumpTimeoutDelta;
        private float _fallTimeoutDelta;
        private float _climbTimeoutDelta;
        private float _rollTimeoutDelta;
        ///COMPONENTS
#if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED
        private PlayerInput _playerInput;
#endif
        private Animator _animator;
        private Rigidbody _rigidbody;
        private StarterAssetsInputs _input;
        private GameObject _mainCamera;
        private SkinnedMeshRenderer _renderer;
        ///ANIMATION
        private bool _hasAnimator;
        private Timer _animationTimer;
        private Timer _idleStateAnimationTimer;
        private Timer _betweenIdleStatesAnimationTimer;
        ///ANIMATION IDS
        private int _animIDSpeed;
        private int _animIDMotionSpeed;
        private int _animIDGrounded;
        private int _animIDCrouch;
        private int _animIDRoll;
        private int _animIDJump;
        private int _animIDFreeFall;
        private int _animIDHighFall;
        private int _animIDClimb;
        private int _animIDStopped;
        private int _animIDIdleState;



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
            _climbTimeoutDelta = ClimbTimeout;
            _rollTimeoutDelta = RollTimeout;
            //init timers
            _betweenIdleStatesAnimationTimer = new Timer(() => InitIdleAnimation(_nextIdleState), SecondsBetweenIdleStates);
            _idleStateAnimationTimer = new Timer(AnimateIdleAnimation);
            _animationTimer = new Timer(() =>
            {
                _highFall = false;
                CanMove = true;
                CanJump = true;
            }, LockWhileAnimatingTimeout);
            InitIdleAnimation();
            _upForOrientation = UpForOrientation;
            _upForOrientationCamera = _upForOrientation;
            Orient(ForwardForOrientation, _upForOrientation);
        }

        private void Update()
        {
            _hasAnimator = TryGetComponent(out _animator);

            GroundedCheck(_upForOrientation);
            SlopeCheck(_upForOrientation);
            ClimbCheck(_upForOrientation);
            OrientCheck(_upForOrientation);

            HandleClimb(ref _upForOrientation, ref _upForMovement, ref _upForOrientationCamera);
            SetUpForMovement(_upForOrientation, ref _upForMovement);
            UpdateCameraPosition();
            SetAnimationsWhenNotMoving();

            if (Grounded && _hasAnimator)
            {
                _animator.SetBool(_animIDCrouch, _input.crouch);
            }

            HandleHighFall();
            SetVelocity(_upForOrientation, _upForMovement);
        }


        private void FixedUpdate()
        {
            // if (Orienting)
            // {
            //     if (OrientRayIntersects && Mathf.Abs(1 - Vector3.Dot(_orientHit.normal, UpForOrientation)) > Epsilon)
            //     {
            //         _upForMovement = _orientHit.normal;
            //         _upForOrientation = _upForMovement;
            //         OrientWithGravity(_upForOrientation);
            //     }
            //     else
            //     {
            //         _upForMovement = _orientHit.normal;
            //         _upForOrientation = _upForMovement;
            //         OrientWithGravity(_upForOrientation);
            //         zeroOutVelocity();
            //         Orienting = false;
            //     }
            // }
            Move();
        }

        private void LateUpdate()
        {
            CameraRotation(_upForOrientationCamera);
        }
        ///START
        private void AssignAnimationIDs()
        {
            _animIDSpeed = Animator.StringToHash("Speed");
            _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
            _animIDGrounded = Animator.StringToHash("Grounded");
            _animIDCrouch = Animator.StringToHash("Crouch");
            _animIDRoll = Animator.StringToHash("Roll");
            _animIDJump = Animator.StringToHash("Jump");
            _animIDFreeFall = Animator.StringToHash("FreeFall");
            _animIDHighFall = Animator.StringToHash("HighFall");
            _animIDClimb = Animator.StringToHash("Climb");
            _animIDStopped = Animator.StringToHash("Stopped");
            _animIDIdleState = Animator.StringToHash("IdleState");
        }
        ///GENERIC
        private float Eerp(float a, float b, float t, float p)
        {
            t = Mathf.Clamp(t, 0, 1);
            float expPowered = Mathf.Pow(t, p);
            return (1 - expPowered) * a + expPowered * b;
        }
        private void Orient(Vector3 forward, Vector3 up)
        {
            transform.LookAt(transform.position + forward, up);
        }
        private void OrientWithGravity(in Vector3 upForOrientation)
        {
            Vector3 newForward = Vector3.Cross(transform.right, upForOrientation);
            Gravity = -Gravity.magnitude * upForOrientation;
            Orient(newForward, upForOrientation);
        }
        private Vector3 orthogonalizeForwardReturnUp(in Vector3 forward, in Vector3 up)
        {
            return Vector3.Dot(forward, up) * up;
        }
        private Vector3 orthogonalizeForwardReturnForward(in Vector3 forward, in Vector3 up)
        {
            return forward - orthogonalizeForwardReturnUp(forward, up);
        }
        private Vector3 getAnOrthogonalVectorFromVector(in Vector3 vector)
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
        private void zeroOutVelocity()
        {
            zeroOutVectorInAllDirections(ref _horizontalVelocity);
            zeroOutVectorInAllDirections(ref _verticalVelocity);
        }
        private void zeroOutVectorInAllDirections(ref Vector3 vector)
        {
            vector = Vector3.zero;
        }
        private void zeroOutVectorInNegativeDirection(ref Vector3 vector, in Vector3 value2subtractFrom, in Vector3 directionReference)
        {
            Vector3 vectorUp = orthogonalizeForwardReturnUp(value2subtractFrom, directionReference);
            float cosThetaBetween = Vector3.Dot(vectorUp.normalized, directionReference);
            if (cosThetaBetween > -1 - Epsilon && cosThetaBetween < -1 + Epsilon)
            {
                vector = value2subtractFrom - vectorUp;
            }
        }
        private void SetUpForMovement(in Vector3 upForOrientation, ref Vector3 upForMovement)
        {
            if (SlopeRayIntersects)
            {
                upForMovement = _slopeHit.normal;
            }
            // else if (!Orienting && OrientRayIntersects && Mathf.Abs(1 - Vector3.Dot(_orientHit.normal, UpForOrientation)) > Epsilon)
            // {
            //     Orienting = true;
            //     upForMovement = _orientHit.normal;
            // }
            else if (upForMovement != upForOrientation)
            {
                upForMovement = upForOrientation;
            }
        }
        ///BASIC MOVEMENT
        private Vector3 getHorizontalDirection(in Vector3 upForOrientation, in Vector3 upForMovement)
        {
            _refVectorOnVWPlane = getAnOrthogonalVectorFromVector(upForOrientation);

            // note: Vector2's != operator uses approximation so is not floating point error prone, and is cheaper than magnitude
            // if there is a move input rotate player when the player is moving
            if (_input.move != Vector2.zero)
            {
                Vector3 mainCameraForward = orthogonalizeForwardReturnForward(_mainCamera.transform.forward, upForOrientation).normalized;
                if (mainCameraForward == Vector3.zero)
                {
                    mainCameraForward = Vector3.Cross(upForOrientation, transform.right).normalized;
                }

                (float _mainCameraY, Vector3 _) = getAngleBetweenTwoVectors(
                    _refVectorOnVWPlane,
                    mainCameraForward,
                    upForOrientation
                    );
                (float _transformY, Vector3 _) = getAngleBetweenTwoVectors(
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
        private void SetHorizontalVelocity(in Vector3 upForOrientation, in Vector3 upForMovement)
        {
            float horizontalSpeed;
            float targetSpeed = MoveSpeed;
            if (_input.sprint) targetSpeed *= MoveSprintMultiplier;
            float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;
            if (Grounded)
            {
                // set target speed based on move speed, sprint speed and if sprint is pressed

                // a simplistic acceleration and deceleration designed to be easy to remove, replace, or iterate upon

                // note: Vector2's == operator uses approximation so is not floating point error prone, and is cheaper than magnitude
                // if there is no input, set the target speed to 0
                if (_input.move == Vector2.zero)
                {
                    targetSpeed = 0.0f;
                }

                // a reference to the players current horizontal velocity
                float currentHorizontalSpeed = _horizontalVelocity.magnitude;

                float speedOffset = 0.1f;
                // accelerate or decelerate to target speed
                if (currentHorizontalSpeed < targetSpeed - speedOffset ||
                    currentHorizontalSpeed > targetSpeed + speedOffset)
                {
                    // creates curved result rather than a linear one giving a more organic speed change
                    // note T in Lerp is clamped, so we don't need to clamp our speed
                    horizontalSpeed = Eerp(currentHorizontalSpeed, targetSpeed * inputMagnitude,
                        Time.deltaTime * SpeedChangeRate, SpeedChangeRatePower);
                    // round speed to 3 decimal places
                    horizontalSpeed = Mathf.Round(horizontalSpeed * 1000f) / 1000f;
                }
                else
                {
                    horizontalSpeed = targetSpeed;
                }
            }
            else
            {
                horizontalSpeed = _lastHorizontalSpeedBeforeJump;
            }
            _animationBlend = Eerp(_animationBlend, targetSpeed, Time.deltaTime * SpeedChangeRate, SpeedChangeRatePower);
            if (_animationBlend < Epsilon) _animationBlend = 0f;

            // update animator if using character
            if (_hasAnimator)
            {
                _animator.SetFloat(_animIDSpeed, _animationBlend);
                _animator.SetFloat(_animIDMotionSpeed, inputMagnitude);
            }

            Vector3 targetDirection = getHorizontalDirection(upForOrientation, upForMovement);
            _horizontalVelocity = targetDirection * horizontalSpeed;
        }
        private void SetVerticalVelocity(in Vector3 upForOrientation)
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
                zeroOutVectorInNegativeDirection(ref _gravity, Gravity, upForOrientation);
                zeroOutVectorInNegativeDirection(ref _verticalVelocity, _verticalVelocity, upForOrientation);
                // Jump
                if (CanJump && _input.jump && _jumpTimeoutDelta <= 0.0f)
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
                if (_jumpTimeoutDelta > 0.0f)
                {
                    _jumpTimeoutDelta -= Time.deltaTime;
                }
                if (!CanJump)
                {
                    _input.jump = false;
                }
            }
            else
            {
                // just risen from ground
                if (_oldGrounded)
                {
                    _lastHorizontalSpeedBeforeJump = _horizontalVelocity.magnitude;
                }
                // reset the jump timeout timer
                _jumpTimeoutDelta = JumpTimeout;

                // fall timeout
                if (_fallTimeoutDelta > 0.0f)
                {
                    _fallTimeoutDelta -= Time.deltaTime;
                }
                else if (_hasAnimator)
                {
                    _animator.SetBool(_animIDFreeFall, true);
                }

                _gravity = Gravity;
                // if we are not grounded, do not jump
                _input.jump = false;
            }

            _verticalVelocity += _gravity * Time.deltaTime;
            _oldGrounded = Grounded;
        }
        private void SetVelocity(in Vector3 upForOrientation, in Vector3 upForMovement)
        {
            SetVerticalVelocity(_upForOrientation);
            if (CanMove)
            {
                SetHorizontalVelocity(_upForOrientation, _upForMovement);
            }
        }
        private void Move()
        {
            Vector3 velocity = _horizontalVelocity + _verticalVelocity;
            // move the player
            _rigidbody.velocity = velocity;
        }
        ///CLIMBING
        private void HandleClimb(ref Vector3 upForOrientation, ref Vector3 upForMovement, ref Vector3 upForOrientationCamera)
        {
            if (Climbing)
            {
                if (1 - Vector3.Dot(upForOrientationCamera, _upForOrientationTarget) < Epsilon)
                {
                    Climbing = false;
                    CanMove = true;
                    zeroOutVelocity();
                    upForOrientation = upForOrientationCamera;
                    OrientWithGravity(upForOrientation);
                    if (_hasAnimator)
                    {
                        _animator.SetBool(_animIDClimb, Climbing);
                    }
                }
                else
                {
                    upForOrientationCamera =
                Quaternion.AngleAxis(_upForOrientationAngleChangePerTick, _axis2rotateUpForOrientationAround) * upForOrientationCamera;
                    OrientCameraFacingForward(upForOrientationCamera);
                }
            }
            else if (ClimbRayIntersects && _climbTimeoutDelta <= 0.0f)
            {
                Climbing = true;
                CanMove = false;
                _upForOrientationTarget = _climbHit.normal;
                if (_hasAnimator)
                {
                    _animator.SetBool(_animIDClimb, Climbing);
                }
                _axis2rotateUpForOrientationAround = Vector3.Cross(upForOrientationCamera, _upForOrientationTarget).normalized;
                (float totalOrientAngle, Vector3 _) = getAngleBetweenTwoVectors(
                   upForOrientationCamera,
                   _upForOrientationTarget,
                   _axis2rotateUpForOrientationAround
                   );
                _upForOrientationAngleChangePerTick = (totalOrientAngle * Time.deltaTime) / SecondsToCompleteUpForOrientationAngleChange;
                _climbTimeoutDelta = ClimbTimeout;
            }
            if (_climbTimeoutDelta > 0.0f)
            {
                _climbTimeoutDelta -= Time.deltaTime;
            }
        }
        ///CAMERA
        private void OrientCamera(Vector3 forward, Vector3 up)
        {
            _mainCamera.transform.LookAt(transform.position + forward, up);
        }
        private void OrientCameraFacingForward(in Vector3 upForOrientation)
        {
            Vector3 newForward = Vector3.Cross(transform.right, upForOrientation);
            OrientCamera(newForward, upForOrientation);
        }
        private void UpdateCameraPosition()
        {
            float playerHeightHalf = _renderer.bounds.size.y / 2;
            CinemachineCameraTarget.transform.position = transform.position + _upForOrientation * playerHeightHalf;
        }
        private void CameraRotation(in Vector3 upForOrientation)
        {
            // if there is an input and camera position is not fixed
            if (_input.look.sqrMagnitude >= Epsilon && !LockCameraPosition)
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
        ///CHECK
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
        private void SlopeCheck(in Vector3 upForOrientation)
        {
            SlopeRayIntersects = Physics.Raycast(
                transform.position + upForOrientation * SlopeRayOffset,
                -upForOrientation,
                out _slopeHit,
                SlopeRayLength,
                GroundLayers
            );
        }
        private void ClimbCheck(in Vector3 upForOrientation)
        {
            float playerHeightY = _renderer.bounds.size.y;
            float playerHeightZhalf = _renderer.bounds.size.z / 2;
            ClimbRayIntersects = Physics.Raycast(
                transform.position + upForOrientation * playerHeightY + transform.forward * (playerHeightZhalf - ClimbRayOffset),
                transform.forward,
                out _climbHit,
                ClimbRayLength,
                GroundLayers
            );
        }
        private void OrientCheck(in Vector3 upForOrientation)
        {
            OrientRayIntersects = Physics.SphereCast(
                transform.position + upForOrientation * OrientRayOffset,
                OrientRadius,
                -upForOrientation,
                out _orientHit,
                OrientRayLength,
                GroundLayers
                );
        }
        ///GIZMO
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
                float distance;
                if (SlopeRayIntersects)
                {
                    Gizmos.color = transparentGreen;
                    distance = _slopeHit.distance;
                }
                else
                {
                    Gizmos.color = transparentRed;
                    distance = SlopeRayLength;
                }

                from = transform.position + _upForOrientation * SlopeRayOffset;
                to = transform.position - _upForOrientation * (distance - SlopeRayOffset);
                Gizmos.DrawLine(from, to);
            }
            if (ShowClimbCheck)
            {
                float distance;
                if (ClimbRayIntersects)
                {
                    Gizmos.color = transparentGreen;
                    distance = _climbHit.distance;
                }
                else
                {
                    Gizmos.color = transparentRed;
                    distance = ClimbRayLength;
                }
                float playerHeightY = _renderer.bounds.size.y;
                float playerHeightZhalf = _renderer.bounds.size.z / 2;
                from = transform.position + _upForOrientation * playerHeightY + transform.forward * (playerHeightZhalf - ClimbRayOffset);
                to = transform.position + _upForOrientation * playerHeightY + transform.forward * (playerHeightZhalf - ClimbRayOffset + distance);
                Gizmos.DrawLine(from, to);
            }
            if (ShowOrientCheck)
            {
                float distance;
                if (OrientRayIntersects)
                {
                    Gizmos.color = transparentGreen;
                    distance = _orientHit.distance;
                }
                else
                {
                    Gizmos.color = transparentRed;
                    distance = OrientRayLength;
                }

                from = transform.position + _upForOrientation * OrientRayOffset;
                to = transform.position - _upForOrientation * (distance - OrientRayOffset);
                Gizmos.DrawLine(from, to);
                Gizmos.DrawSphere(to, OrientRadius);
            }
        }
        ///AUDIO
        private void OnFootstep(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                if (FootstepAudioClips.Length > 0)
                {
                    var index = UnityEngine.Random.Range(0, FootstepAudioClips.Length);
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
        ///ANIMATION
        private void InitIdleAnimation(uint currentIdleState = 0)
        {
            _currentIdleState = currentIdleState;
            _nextIdleState = (_currentIdleState == 0) ? 1 : (_currentIdleState % ((uint)SecondsInTheIdleStates.Length - 1)) + 1;
            _betweenIdleStatesAnimationTimer.Reset();
            _idleStateAnimationTimer.SetTimeLeft(SecondsInTheIdleStates[_currentIdleState]);
        }
        private void SwitchIdleAnimation()
        {
            if (Grounded && (_oldInputMove != _input.move || !_oldGrounded))
            {
                InitIdleAnimation();
                if (_hasAnimator)
                {
                    _animator.SetFloat(_animIDIdleState, _currentIdleState);
                }
            }
            _idleStateAnimationTimer.Tick(false);
        }
        private void AnimateIdleAnimation()
        {
            float currentIdleStateReal = Eerp(
                _nextIdleState, _currentIdleState,
                _betweenIdleStatesAnimationTimer.timeLeft / SecondsBetweenIdleStates,
                IdleStateChangeRatePower);
            if (_hasAnimator)
            {
                _animator.SetFloat(_animIDIdleState, currentIdleStateReal);
            }
            _betweenIdleStatesAnimationTimer.Tick();
        }
        private void StartStoppedAnimation()
        {
            if (_hasAnimator)
            {
                _animator.SetBool(_animIDStopped, _oldInputSprint && _oldInputMove != _input.move);
            }
        }
        private void SetAnimationsWhenNotMoving()
        {
            if (_input.move == Vector2.zero)
            {
                SwitchIdleAnimation();
                StartStoppedAnimation();
            }
            _oldInputMove = _input.move;
            _oldInputSprint = _input.sprint;
        }
        private void RejectInputWhile(Action before, Action after, ref float durationDelta, float duration)
        {
            if (durationDelta == LockWhileAnimatingTimeout)
            {
                CanMove = false;
                CanJump = false;
                before();
            }
            if (durationDelta > 0.0f)
            {
                durationDelta -= Time.deltaTime;
            }
            else
            {
                after();
                CanMove = true;
                CanJump = true;
                durationDelta = duration;
            }
        }
        //HIGH FALL
        private void HandleHighFall()
        {
            if (Grounded && _highFall)
            {
                //just fell on the ground
                if (!_oldGrounded)
                {
                    CanMove = false;
                    CanJump = false;
                    _crouchPressedWhenRolling = false;
                    _rollTimeoutDelta = RollTimeout;
                }
                if (_rollTimeoutDelta > 0.0f)
                {
                    if (_input.crouch)
                    {
                        _crouchPressedWhenRolling = true;
                    }
                    _rollTimeoutDelta -= Time.deltaTime;
                }
                else
                {
                    if (_crouchPressedWhenRolling)
                    {
                        //successful landing, plunge forward while rolling
                    }
                    else
                    {
                        //hard landing
                    }


                    if (_animationTimer.state == Timer.TimerState.Set)
                    {
                        // CanMove = false;
                        // CanJump = false;
                        if (_hasAnimator)
                        {
                            _animator.SetBool(_animIDHighFall, false);
                            _animator.SetBool(_animIDRoll, _crouchPressedWhenRolling);
                        }
                    }
                    _animationTimer.Tick();
                }
            }
            else if (Mathf.Pow(_verticalVelocity.magnitude, 2) / (2 * Gravity.magnitude) >= FallDamageHeight)
            {
                _highFall = true;
                if (_hasAnimator)
                {
                    _animator.SetBool(_animIDHighFall, _highFall);
                }
            }
        }

    }
}