using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
  [SerializeField] PlayerInput _input;

	public event Action Debug;
	public event Action<bool> OnPrimaryUsePressed;
	public event Action<bool> Interact;
	public event Action<bool> Jump;
	public event Action<bool> OnFreeLookPressed;
	public event Action<bool> OnCrouchPressed;

	[Header("Character Input Values")]
	public Vector2 move;
	public Vector2 look;
	public bool jump;
	public bool sprint;
	public bool FreeLook;
	public bool Crouching;
	public bool UsingPrimary;

	[Header("Movement Settings")]
	public bool analogMovement;

	[Header("Mouse Cursor Settings")]
	public bool cursorLocked = true;
	public bool cursorInputForLook = true;

	public void OnMove(InputValue value)
	{
		MoveInput(value.Get<Vector2>());
	}

	public void OnLook(InputValue value)
	{
		if (cursorInputForLook)
		{
			LookInput(value.Get<Vector2>());
		}
	}

	public void OnFreeLook(InputValue value)
  {
		bool enabled = Convert.ToBoolean(value.Get<float>());

		OnFreeLookPressed?.Invoke(enabled);
		FreeLook = enabled;
	}

	public void OnCrouch(InputValue value)
	{
		Crouching = AsBool(value);
		InvokeAsBool(OnCrouchPressed, value);
	}

	public void OnJump(InputValue value) 
	{
		InvokeAsBool(Jump, value);
		jump = AsBool(value);
	}

	public void OnSprint(InputValue value)
	{
		SprintInput(value.isPressed);
	}

	public void MoveInput(Vector2 newMoveDirection)
	{
		move = newMoveDirection;
	}

	public void LookInput(Vector2 newLookDirection)
	{
		look = newLookDirection;
	}

	public void JumpInput(bool newJumpState)
	{
		jump = newJumpState;
	}

	public void SprintInput(bool newSprintState)
	{
		sprint = newSprintState;
	}

	void OnDebug(InputValue value) => Debug?.Invoke();
	void OnPrimaryUse(InputValue value) 
	{
		UsingPrimary = Convert.ToBoolean(value.Get<float>());
		OnPrimaryUsePressed?.Invoke(UsingPrimary);
	}
	void OnInteract(InputValue value) => Interact?.Invoke(Convert.ToBoolean(value.Get<float>()));

	private void InvokeAsBool(Action<bool> cb, InputValue val) => cb?.Invoke(AsBool(val));

	private bool AsBool(InputValue val) => Convert.ToBoolean(val.Get<float>());

	private void OnApplicationFocus(bool hasFocus)
	{
		SetCursorState(cursorLocked);
	}

	private void SetCursorState(bool newState)
	{
		Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
	}
}