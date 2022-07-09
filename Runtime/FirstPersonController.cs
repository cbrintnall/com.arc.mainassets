using Cinemachine;
using UnityEngine;
using System.Linq;
using Arc.Lib.Debug;

public enum MovementState
{
	Running,
	Idle,
	Jumping,
	Climbing,
	Sliding,
	Crouching,
	WallRunning,
	HookShotPull,
	ChargingAttack,
	ReleasingAttack
}

namespace Arc.Lib.Controllers
{
	[RequireComponent(typeof(CharacterController))]
	public class FirstPersonController : MonoBehaviour
	{
		public float Speed { get => new Vector2(_controller.velocity.x, _controller.velocity.z).sqrMagnitude; }
		public bool IsWallRunning { get => _moveState == MovementState.WallRunning; }
		public MovementState CurrentState { get => _moveState; }

		[Header("Audio")]
		[SerializeField] AudioSource _audioPlayer;
		[SerializeField] AudioClip _jumpSound;

		[Header("Combat")]
		 float _attackChargeTime;
		 float _releaseStartedAt;
		[SerializeField] float _maxAttackChargeTimeSeconds = 1f;
		[SerializeField] float _releaseTimeSeconds = .5f;

		[Header("Player")]
		[Tooltip("Move speed of the character in m/s")]
		public float MoveSpeed = 4.0f;
		[Tooltip("Sprint speed of the character in m/s")]
		public float SprintSpeed = 6.0f;
		[Tooltip("Rotation speed of the character")]
		public float RotationSpeed = 1.0f;
		[Tooltip("Acceleration and deceleration")]
		public float SpeedChangeRate = 10.0f;

		[Space(10)]
		[Tooltip("The height the player can jump")]
		public float JumpHeight = 1.2f;
		[Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
		public float Gravity = -15.0f;

		[Space(10)]
		[Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
		public float JumpTimeout = 0.1f;
		[Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
		public float FallTimeout = 0.15f;

		[Header("Player Grounded")]
		[Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
		public bool Grounded = true;
		[Tooltip("Useful for rough ground")]
		public float GroundedOffset = -0.14f;
		[Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
		public float GroundedRadius = 0.5f;
		[Tooltip("What layers the character uses as ground")]
		public LayerMask GroundLayers;

		[Header("Cinemachine")]
		[Tooltip("The first person camera matches the y value of this bone")]
		public Transform HeadBone;
		[Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
		public GameObject CinemachineCameraTarget;
		public CinemachineVirtualCamera FirstPersonCamera;
		[Tooltip("How far in degrees can you move the camera up")]
		public float TopClamp = 90.0f;
		[Tooltip("How far in degrees can you move the camera down")]
		public float BottomClamp = -90.0f;
		public float LeftFreeLookClamp = -90.0f;
		public float RightFreeLookClamp = 90.0f;

		[Header("Speed")]
		 float _currentTargetSpeed;
		[SerializeField] float _topTargetSpeed = 40f;

		[Header("Climbing")]
		[SerializeField] float _climbSpaceBuffer = .01f;
		[SerializeField] float _climbSpeed;
		[SerializeField] float _climbCastDistance;
		[SerializeField] Transform _upperClimb;
		[SerializeField] Transform _lowerClimb;
		[SerializeField] Transform _ledgeClimb;
		[SerializeField] LayerMask _climbableLayers;

		[Header("Crouching")]
		[SerializeField] float _crouchCapsuleHeight;
		[Range(0, 1f)][SerializeField] float _crouchSpeedMultiplier = .3f;

		[Header("Sliding")]
		 float _slideTargetSpeed;
		[SerializeField] Transform _slidePoint;
		[SerializeField] float _slideCheckDistance = .5f;
		[SerializeField] float _slideSpeedChangeRate = 2.5f;
		[SerializeField] float _slideMinimumSpeed = .25f;
		[SerializeField] float _slideSlopeMultiplier = 1f;
		[SerializeField] float _slideFlatSlowdownRate = .5f;

		[Header("Wall running")]
		 float _currentWallRunTargetSpeed;
		[SerializeField] float _wallRayDistance = .5f;
		[SerializeField] float _wallRunSpeed = 1f;
		[SerializeField] float _wallRunSpeedBurst = 3f;
		[SerializeField] float _wallRunMaxSpeed = 25f;
		[SerializeField] float _wallRunSpeedBurstTimeSeconds = .5f;
		[Tooltip("The maximum angle a player can be looking away from a wall to automatically wallrun on it while falling")]
		[SerializeField] float _fallWallAttachmentAngleMaxDegrees = 35f;
		[SerializeField] float _fallWallRunBaseDegrees = 60f;
		[SerializeField] MovementState[] _wallRunSpeedResetStates = new MovementState[] { };

		[Header("Jumping")]
		 int _jumpsSinceLand = 0;
		[SerializeField] MovementState[] _verticalVelocityResetStates = new MovementState[] { };
		[SerializeField] int _maxJumps = 2;
		[SerializeField] float _coyoteTime = .5f;
		[SerializeField] float _jumpSpeedBumpMultiplier = .25f;

		 MovementState _moveState;
		 MovementState _lastMoveState;

		[Header("Hookshot")]
		 Vector3 _landingPoint;
		[SerializeField] float _hookShotTargetSpeed = 10f;
		[SerializeField] float _distanceToSatisfyHookShot = .25f;

		// cinemachine
		private float _cinemachineTargetPitch;

		// player
		private float _speed;
		private float _rotationVelocity;
		private float _verticalVelocity;
		private float _terminalVelocity = 53.0f;
		private float _rotationYOffset;

		// timeout deltatime
		private float _jumpTimeoutDelta;
		private float _fallTimeoutDelta;

		private CharacterController _controller;
		private InputManager _input;
		private GameObject _mainCamera;

		DebugManager _debug;

		public void OnHookShotLanded(Vector3 landingPoint)
    {
			_landingPoint = landingPoint;
			SetMoveState(MovementState.HookShotPull);
    }

		public void HookShotBreak()
    {
			SetMoveState(MovementState.Idle);
    }

		private void Awake()
		{
			_controller = GetComponent<CharacterController>();

			// get a reference to our main camera
			if (_mainCamera == null)
			{
				_mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
			}
		}

		private void Start()
		{
			_input = FindObjectOfType<InputManager>();
			_debug = FindObjectOfType<DebugManager>();

			_input.OnCrouchPressed += OnCrouch;
			_input.Jump += OnJump;
			_input.OnPrimaryUsePressed += OnPrimaryUse;

			// reset our timeouts on start
			_jumpTimeoutDelta = JumpTimeout;
			_fallTimeoutDelta = FallTimeout;

			SetMoveState(MovementState.Idle);
		}

		private void OnPrimaryUse(bool started)
    {
			if (started && _moveState != MovementState.ChargingAttack || _moveState != MovementState.ReleasingAttack)
      {
				SetMoveState(MovementState.ChargingAttack);
      }
			else if (!started && _moveState == MovementState.ChargingAttack)
      {
				SetMoveState(MovementState.ReleasingAttack);
			}
    }

		private void OnCrouch(bool started)
    {
			if (started)
      {
				if (_currentTargetSpeed > 0f)
        {
					SetMoveState(MovementState.Sliding);
        }
        else
        {
					SetMoveState(MovementState.Crouching);
        }
      }
      else
      {
				SetMoveState(MovementState.Idle);
			}

			float crouchAnimationWeight = started ? 1f : 0f;
			float standingAnimationWeight = Mathf.Abs(crouchAnimationWeight - 1f);
		}

		private void SetMoveState(MovementState state)
    {
			// Ignore state changes into the same state
			if (state == _moveState)
				return;

			_lastMoveState = _moveState;
			_moveState = state;

			MoveStateChanged();
    }

		private void MoveStateChanged()
    {
			if (_lastMoveState == MovementState.Jumping && _moveState != MovementState.Jumping)
      {
				_jumpsSinceLand = 0;
			}

			if (_moveState == MovementState.ChargingAttack)
				_attackChargeTime = 0f;

			if (_verticalVelocityResetStates.Contains(_moveState))
      {
				_verticalVelocity = 0f;
			}

			if (_moveState == MovementState.WallRunning)
      {
				_currentWallRunTargetSpeed += _wallRunSpeedBurst;
      }

			if (_moveState == MovementState.ReleasingAttack)
      {
				_releaseStartedAt = Time.realtimeSinceStartup;
			}

			if (_moveState == MovementState.Crouching)
      {
				_currentTargetSpeed = MoveSpeed * _crouchSpeedMultiplier;
			}
			
			if (_moveState == MovementState.Idle)
      {
				_currentTargetSpeed = 0f;
      }

			if (_moveState != MovementState.Sliding)
      {
				_slideTargetSpeed = 0f;
      }
      else
      {
				_slideTargetSpeed = _currentTargetSpeed;
      }
      
			if (_wallRunSpeedResetStates.Contains(_moveState))
      {
				_currentWallRunTargetSpeed = SprintSpeed;
      }
		}

    private void Update()
    {
			CameraRotation();
		}

    private void FixedUpdate()
		{
			switch(_moveState)
      {
				case MovementState.Climbing:
					MoveClimbing();
					break;
				case MovementState.Sliding:
					GroundedCheck();
					AdjustSlideTargetSpeed();
					JumpAndGravity();
					MoveSliding();
					break;
				case MovementState.Crouching:
					AdjustSlideTargetSpeed();
					JumpAndGravity();
					GroundedCheck();
					_currentTargetSpeed = MoveSpeed * _crouchSpeedMultiplier;
					Move(_input.move, SpeedChangeRate, true);
					break;
				case MovementState.WallRunning:
					GroundedCheck();
					MoveWallRun();
					break;
				case MovementState.Jumping:
					GroundedCheck();
					JumpAndGravity();

					Vector2 direction = (_input.move + Vector2.up).normalized;

					Move(direction, SpeedChangeRate, false);
					break;
				case MovementState.Running:
					if (_input.move == Vector2.zero)
						SetMoveState(MovementState.Idle);

					GroundedCheck();
					JumpAndGravity();

					_currentTargetSpeed = _input.sprint ? SprintSpeed : MoveSpeed;
					Move(_input.move, SpeedChangeRate, true);
					break;
				case MovementState.Idle:
					if (_input.move != Vector2.zero)
						SetMoveState(MovementState.Running);

					GroundedCheck();
					JumpAndGravity();
					break;
				case MovementState.HookShotPull:
					float distance = Vector3.Distance(transform.position, _landingPoint);

					if (distance <= _distanceToSatisfyHookShot)
          {
						OnHookShotSatisfied();
          }
					else
          {
						Vector3 directionToPoint = (_landingPoint- transform.position).normalized;
						_currentTargetSpeed = Mathf.Max(_hookShotTargetSpeed, _currentTargetSpeed);
						Move(directionToPoint, SpeedChangeRate, false);
					}
					break;
				case MovementState.ChargingAttack:
					GroundedCheck();
					DoChargingAttack();
					break;
				case MovementState.ReleasingAttack:
					GroundedCheck();
					DoReleasingAttack();
					break;
      }

			_debug.Track("MoveState", _moveState);
			_debug.Track("Speed", Speed);
			_debug.Track("Target Speed", _currentTargetSpeed);
		}

		void DoReleasingAttack()
    {
			if (Time.realtimeSinceStartup - _releaseStartedAt >= _releaseTimeSeconds)
      {
				SetMoveState(MovementState.Running);
				return;
      }
			Move(transform.forward, 0f, false);
    }

		void DoChargingAttack()
    {
			_attackChargeTime = Mathf.Clamp(_attackChargeTime + Time.fixedDeltaTime, 0, _maxAttackChargeTimeSeconds);

			_debug.Track("Attack Charge Time", _attackChargeTime);

			if (_attackChargeTime >= _maxAttackChargeTimeSeconds)
      {
				SetMoveState(MovementState.ReleasingAttack);
      }
    }

		void OnHookShotSatisfied()
    {
			/*
			 * Temp: logic here should be more advanced..
			 * a. can we wall run? we would have to check multiple angles since the player would be facing the wall
			 * b. is the user trying to slide? they could use momentum from hookshot to continue into a slide
			 */

			SetMoveState(MovementState.Idle);
    }

		void MoveSliding()
    {
			Move(transform.forward, _slideSpeedChangeRate, Grounded);

			if (_slideTargetSpeed <= 0f)
      {
				if (_input.Crouching)
        {
					SetMoveState(MovementState.Crouching);
        }
        else
        {
					SetMoveState(MovementState.Idle);
				}
      }
    }

		void AdjustSlideTargetSpeed()
    {
			if (Physics.Raycast(transform.position, -transform.up, out RaycastHit hit, 1f, ~0, QueryTriggerInteraction.Ignore))
      {
				float slideScalar = Vector3.Dot(transform.forward, hit.normal);

				_debug.Track("Slide Scalar", slideScalar);

				if (slideScalar == 0f)
        {
					_slideTargetSpeed = Mathf.Lerp(_slideTargetSpeed, Mathf.Max(_slideTargetSpeed - _slideFlatSlowdownRate, 0f), .5f);
				}
        else
        {
					_slideTargetSpeed = Mathf.Lerp(_slideTargetSpeed, _slideTargetSpeed + (slideScalar * _slideSlopeMultiplier), .5f);
				}
      }

			_currentTargetSpeed = _slideTargetSpeed;

			if (_slideTargetSpeed > 0f)
      {
				SetMoveState(MovementState.Sliding);
      }
    }

		void MoveWallRun()
    {
			Vector3 hitDir = Vector3.zero;

			RaycastHit upper = default;
			RaycastHit bottom = default;

			// Check if we can wall run right
			if (CanWallRun(transform.right, out RaycastHit topRight, out RaycastHit bottomRight))
			{
				hitDir = transform.right;
				upper = topRight;
				bottom = bottomRight;
			}
			// If not, we check left
			else if (CanWallRun(-transform.right, out RaycastHit topLeft, out RaycastHit bottomLeft))
			{
				hitDir = -transform.right;
				upper = topLeft;
				bottom = bottomLeft;
			}

			if (Grounded || hitDir == Vector3.zero)
      {
				SetMoveState(MovementState.Idle);
				return;
      }

			_debug.Track("WallAngle", Vector3.Angle(transform.forward, -bottom.normal));

			Vector3 moveDirection = transform.forward - bottom.normal * Vector3.Dot(transform.forward, bottom.normal);
			_currentWallRunTargetSpeed = Mathf.Clamp(_currentWallRunTargetSpeed, SprintSpeed, _wallRunMaxSpeed);

			_debug.Track("WallRunTargetSpeed", _currentWallRunTargetSpeed);
			_currentTargetSpeed = _currentWallRunTargetSpeed;

			Move(moveDirection, SpeedChangeRate, false);
		}

		private void MoveClimbing()
    {
			int castsHit = 0;
			Vector3 centerPoint = (_upperClimb.position + _lowerClimb.position) / 2;
			Vector3 normals = Vector3.zero;
			Vector3 offset = transform.TransformDirection(Vector2.left * .5f);
			//Vector3 inputOffset = transform.TransformDirection(_input.move).normalized;
			Vector3 inputOffset = Vector3.zero;

			for(int i = 0; i < 4; i ++)
      {
				Vector3 origin = centerPoint + offset;

				bool castHit = Physics.Raycast(origin, transform.forward + inputOffset, out RaycastHit hit);

				_debug.DebugRaycast(origin, transform.forward + inputOffset, castHit);

				if (castHit)
        {
					normals += hit.normal;
					castsHit++;
        }

				offset = Quaternion.AngleAxis(90f, transform.forward) * offset;
      }

			normals /= castsHit;
			normals = -normals;

			bool finalCastHit = Physics.Raycast(centerPoint, normals, out RaycastHit finalHit);

			_debug.DebugRaycast(centerPoint, normals, finalCastHit);

			Vector3 finalMovement = Vector3.zero;
			if (finalCastHit)
      {
				transform.forward = -finalHit.normal;
				finalMovement += transform.right * _input.move.x + transform.up * _input.move.y;
			}

			_controller.Move(finalMovement * _climbSpeed * Time.deltaTime);
		}

		private void OnJump(bool started)
    {
			// Jump
			if (started)
			{
				if (!Grounded && !IsWallRunning)
        {
					bool canWallRun = CanWallRun(transform.right) || CanWallRun(-transform.right);

					if (canWallRun)
					{
						SetMoveState(MovementState.WallRunning);
						return;
					}
				}
				
				if (_jumpsSinceLand < _maxJumps)
        {
					Jump();
				}
			}
		}

		private void Jump()
    {
			_jumpsSinceLand++;

			// the square root of H * -2 * G = how much velocity needed to reach desired height
			_verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
			_audioPlayer.PlayOneShot(_jumpSound);

			SetMoveState(MovementState.Jumping);
		}

		private bool CanWallRun(Vector3 inDirection)
    {
			bool
				top = Physics.Raycast(_lowerClimb.position, inDirection, out RaycastHit upper, 1f, _climbableLayers, QueryTriggerInteraction.Ignore),
				bottom = Physics.Raycast(_lowerClimb.position, inDirection, out RaycastHit lower, 1f, _climbableLayers, QueryTriggerInteraction.Ignore);

			return top && bottom;
		}

		private bool CanWallRun(Vector3 inDirection, out RaycastHit topHit, out RaycastHit bottomHit)
		{
			bool
				top = Physics.Raycast(_lowerClimb.position, inDirection, out RaycastHit upper, _wallRayDistance, _climbableLayers, QueryTriggerInteraction.Ignore),
				bottom = Physics.Raycast(_lowerClimb.position, inDirection, out RaycastHit lower, _wallRayDistance, _climbableLayers, QueryTriggerInteraction.Ignore);

			topHit = upper;
			bottomHit = lower;

			return top && bottom;
		}

		private void GroundedCheck()
		{
			// set sphere position, with offset
			Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
			Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);
		}

		private void CameraRotation()
		{
			bool climbing = _moveState == MovementState.Climbing;

			//Don't multiply mouse input by Time.deltaTime
			float deltaTimeMultiplier = 1f;
				
			_rotationVelocity = _input.look.x * RotationSpeed * deltaTimeMultiplier;

			_cinemachineTargetPitch = ClampAngle(
				_cinemachineTargetPitch + (_input.look.y * RotationSpeed * deltaTimeMultiplier),
				BottomClamp,
				TopClamp
			);

      if (climbing || _input.FreeLook)
      {
				_rotationYOffset = ClampAngle(
					_rotationYOffset + (_input.look.x * RotationSpeed * deltaTimeMultiplier),
					LeftFreeLookClamp,
					RightFreeLookClamp
				);
			}
      else
      {
				// rotate the player left and right
				transform.Rotate(Vector3.up * _rotationVelocity);
			}

			// Update Cinemachine camera target pitch
			CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(_cinemachineTargetPitch, _rotationYOffset, 0.0f);

			if (!_input.FreeLook && !climbing)
      {
				_rotationYOffset = Mathf.MoveTowards(_rotationYOffset, 0f, Time.deltaTime);
			}
		}

		private void Move(Vector3 direction, float speedChangeRate, bool canLower)
    {
			// a reference to the players current horizontal velocity
			float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;
			float targetSpeed = Mathf.Clamp(_currentTargetSpeed, 0f, _topTargetSpeed);
			float speedOffset = 0.1f;

			bool shouldRaise = currentHorizontalSpeed < targetSpeed - speedOffset;
			bool shouldLower = (currentHorizontalSpeed > targetSpeed + speedOffset && canLower);
			bool changingSpeed = shouldRaise || shouldLower;

			_debug.Track("ChangingSpeed", changingSpeed);

			// accelerate or decelerate to target speed
			if (changingSpeed)
			{
				// creates curved result rather than a linear one giving a more organic speed change
				// note T in Lerp is clamped, so we don't need to clamp our speed
				_speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed, Time.deltaTime * speedChangeRate);

				// round speed to 3 decimal places
				_speed = Mathf.Round(_speed * 1000f) / 1000f;
			}
			else if(_speed > targetSpeed && canLower)
			{
				_speed = targetSpeed;
			}

			_debug.Track("RawSpeed", _speed);
			_debug.Track("TargetSpeed", targetSpeed);
			_debug.Track("SpeedOffset", speedOffset);

			// move the player
			_controller.Move(direction.normalized * (_speed * Time.deltaTime) + new Vector3(0f, _verticalVelocity, 0f) * Time.deltaTime);
		}

		private void Move(Vector2 direction, float speedChangeRate, bool canLower)
    {
			Vector3 movementDirection = transform.right * direction.x + transform.forward * direction.y;
			Move(movementDirection, speedChangeRate, canLower);
		}

		private void JumpAndGravity()
		{
			if (Grounded)
			{
				// reset the fall timeout timer
				_fallTimeoutDelta = FallTimeout;

				// stop our velocity dropping infinitely when grounded
				if (_verticalVelocity < 0.0f)
				{
					_verticalVelocity = -2f;

					if (CurrentState == MovementState.Jumping)
					{
						SetMoveState(_lastMoveState);
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

				_debug.Track("VerticalVelocity", _verticalVelocity);

				// Allow wall running only while falling
				if (_input.jump && _verticalVelocity < 0f)
        {
					RaycastHit bottom = default;

					// Check if we can wall run right
					if (CanWallRun(transform.right, out RaycastHit topRight, out RaycastHit bottomRight))
					{
						bottom = bottomRight;
					}
					// If not, we check left
					else if (CanWallRun(-transform.right, out RaycastHit topLeft, out RaycastHit bottomLeft))
					{
						bottom = bottomLeft;
					}

					float angle = Mathf.Abs(Vector3.Angle(transform.forward, -bottom.normal));
					_debug.Track("FallWallCheckAngle", angle);

					if (angle >= _fallWallRunBaseDegrees && angle <= _fallWallRunBaseDegrees + _fallWallAttachmentAngleMaxDegrees)
					{
						SetMoveState(MovementState.WallRunning);
						return;
					}
				}
			}

			// apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
			if (_verticalVelocity < _terminalVelocity)
			{
				float nextVerticalVelocity = _verticalVelocity + (Gravity * Time.deltaTime);
				_verticalVelocity = nextVerticalVelocity;
			}
		}

		private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
		{
			if (lfAngle < -360f) lfAngle += 360f;
			if (lfAngle > 360f) lfAngle -= 360f;
			return Mathf.Clamp(lfAngle, lfMin, lfMax);
		}

		private void OnDrawGizmosSelected()
		{
			Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
			Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

			if (Grounded) Gizmos.color = transparentGreen;
			else Gizmos.color = transparentRed;

			// when selected, draw a gizmo in the position of, and matching radius of, the grounded collider
			Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z), GroundedRadius);

			Gizmos.color = Color.green;
		}
	}
}