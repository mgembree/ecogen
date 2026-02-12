using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class CameraPanZoom2D : MonoBehaviour
{
	[SerializeField] private float panSpeed = 10f;
	[SerializeField] private float zoomSpeed = 5f;
	[SerializeField] private float minZoom = 2f;
	[SerializeField] private float maxZoom = 30f;
	[SerializeField] private bool useMiddleMouseDrag = true;
	[SerializeField] private bool useRightMouseDrag = false;

	private Camera cam;
	private Vector3 lastMouseWorld;
	private bool dragging;

	private void Awake()
	{
		cam = GetComponent<Camera>();
		if (cam == null)
		{
			cam = Camera.main;
		}
	}

	private void Update()
	{
		if (cam == null)
		{
			return;
		}

		HandleZoom();
		HandleKeyboardPan();
		HandleMouseDrag();
	}

	private void HandleZoom()
	{
		var scroll = GetScrollDelta();
		if (Mathf.Abs(scroll) < 0.001f)
		{
			return;
		}

		if (cam.orthographic)
		{
			cam.orthographicSize = Mathf.Clamp(cam.orthographicSize - scroll * zoomSpeed, minZoom, maxZoom);
		}
		else
		{
			cam.fieldOfView = Mathf.Clamp(cam.fieldOfView - scroll * zoomSpeed, 20f, 90f);
		}
	}

	private void HandleKeyboardPan()
	{
		var input = GetMoveInput();
		if (input.sqrMagnitude > 0f)
		{
			transform.position += input.normalized * panSpeed * Time.unscaledDeltaTime;
		}
	}

	private void HandleMouseDrag()
	{
		var useDrag = (useMiddleMouseDrag && GetMouseButton(2)) || (useRightMouseDrag && GetMouseButton(1));
		if (useDrag)
		{
			var mouseWorld = cam.ScreenToWorldPoint(GetMousePosition());
			mouseWorld.z = transform.position.z;
			if (!dragging)
			{
				lastMouseWorld = mouseWorld;
				dragging = true;
				return;
			}

			var delta = lastMouseWorld - mouseWorld;
			transform.position += delta;
			lastMouseWorld = mouseWorld;
		}
		else
		{
			dragging = false;
		}
	}

	private float GetScrollDelta()
	{
#if ENABLE_INPUT_SYSTEM
		return Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
#else
		return Input.mouseScrollDelta.y;
#endif
	}

	private Vector3 GetMoveInput()
	{
#if ENABLE_INPUT_SYSTEM
		var dir = Vector3.zero;
		if (Keyboard.current != null)
		{
			if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) dir.x -= 1f;
			if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) dir.x += 1f;
			if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) dir.y -= 1f;
			if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) dir.y += 1f;
		}
		return dir;
#else
		return new Vector3(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"), 0f);
#endif
	}

	private bool GetMouseButton(int button)
	{
#if ENABLE_INPUT_SYSTEM
		if (Mouse.current == null) return false;
		return button switch
		{
			0 => Mouse.current.leftButton.isPressed,
			1 => Mouse.current.rightButton.isPressed,
			2 => Mouse.current.middleButton.isPressed,
			_ => false
		};
#else
		return Input.GetMouseButton(button);
#endif
	}

	private Vector3 GetMousePosition()
	{
#if ENABLE_INPUT_SYSTEM
		return Mouse.current != null ? (Vector3)Mouse.current.position.ReadValue() : Vector3.zero;
#else
		return Input.mousePosition;
#endif
	}
}