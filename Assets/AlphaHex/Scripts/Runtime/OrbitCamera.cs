using UnityEngine;
using UnityEngine.EventSystems;

namespace AlphaHex.Runtime
{
    /// <summary>Mouse, keyboard and touch camera. Drag state survives release for tile selection.</summary>
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(-80)]
    public sealed class OrbitCamera : MonoBehaviour
    {
        public bool PointerOverUI { get; set; }
        public bool WasDragging { get; private set; }

        private Camera _camera;
        private Vector3 _center, _target, _smoothedTarget;
        private float _radius = 8, _distance = 19, _smoothedDistance = 19, _yaw = -32, _smoothedYaw = -32;
        private const float Pitch = 53f;
        private const float DragThreshold = 9f;
        private Vector2 _mouseStart, _lastMouse, _touchStart, _lastTouch;
        private bool _mouseBlocked, _touchBlocked, _mouseHeld;
        private int _touchFinger = -1;
        private float _lastPinchDistance, _lastPinchAngle;
        private bool _pinching;

        public void Initialize(Vector3 target, float radius)
        {
            _camera = GetComponent<Camera>();
            _camera.orthographic = false;
            _camera.fieldOfView = 42f;
            _camera.nearClipPlane = 0.08f;
            _camera.farClipPlane = Mathf.Max(180f, radius * 20f);
            _center = _target = _smoothedTarget = target;
            _radius = Mathf.Max(2, radius);
            _distance = _smoothedDistance = Mathf.Clamp(_radius * 2.6f, MinimumDistance, MaximumDistance);
            _smoothedYaw = _yaw;
            ApplyCamera();
        }

        private float MinimumDistance { get { return 4f; } }
        private float MaximumDistance { get { return Mathf.Max(18f, _radius * 4f); } }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        private void Update()
        {
            if (Input.touchCount > 0)
            {
                TouchInput();
                return;
            }
            _pinching = false;
            _touchFinger = -1;
            _touchBlocked = false;
            MouseInput();
            KeyboardInput();
        }

        private bool IsOverUI(int pointerId = -1)
        {
            if (PointerOverUI) return true;
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(pointerId);
        }

        private void MouseInput()
        {
            Vector2 mouse = Input.mousePosition;
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
            {
                _mouseStart = _lastMouse = mouse;
                _mouseBlocked = IsOverUI();
                _mouseHeld = true;
                WasDragging = false;
            }
            if (_mouseHeld && !_mouseBlocked)
            {
                if ((mouse - _mouseStart).sqrMagnitude > DragThreshold * DragThreshold) WasDragging = true;
                Vector2 delta = mouse - _lastMouse;
                if (Input.GetMouseButton(1) && WasDragging) _yaw += delta.x * 0.24f;
                if (Input.GetMouseButton(2) && WasDragging) PanPixels(delta);
            }
            _lastMouse = mouse;
            if (!Input.GetMouseButton(0) && !Input.GetMouseButton(1) && !Input.GetMouseButton(2)) _mouseHeld = false;
            if (!IsOverUI())
            {
                float scroll = Input.mouseScrollDelta.y;
                if (Mathf.Abs(scroll) > 0.001f) Zoom(Mathf.Exp(-scroll * 0.11f));
            }
        }

        private void KeyboardInput()
        {
            // A focused UI control owns the keyboard while it is being edited.
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
            {
                var selected = EventSystem.current.currentSelectedGameObject;
                if (selected.GetComponent<UnityEngine.UI.InputField>() != null) return;
            }
            float x = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0)
                    - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
            float z = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1 : 0)
                    - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1 : 0);
            Vector3 forward = Quaternion.Euler(0, _yaw, 0) * Vector3.forward;
            Vector3 right = Quaternion.Euler(0, _yaw, 0) * Vector3.right;
            float speed = _distance * 0.45f * Time.unscaledDeltaTime;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) speed *= 1.7f;
            _target += Vector3.ClampMagnitude(right * x + forward * z, 1) * speed;
            if (Input.GetKey(KeyCode.Q)) _yaw -= 65 * Time.unscaledDeltaTime;
            if (Input.GetKey(KeyCode.E)) _yaw += 65 * Time.unscaledDeltaTime;
            ClampTarget();
        }

        private void TouchInput()
        {
            if (Input.touchCount >= 2)
            {
                Touch first = Input.GetTouch(0);
                Touch second = Input.GetTouch(1);
                Vector2 difference = second.position - first.position;
                float distance = difference.magnitude;
                float angle = Mathf.Atan2(difference.y, difference.x) * Mathf.Rad2Deg;
                if (!_pinching || first.phase == TouchPhase.Began || second.phase == TouchPhase.Began)
                {
                    _touchBlocked = _touchBlocked || IsOverUI(first.fingerId) || IsOverUI(second.fingerId);
                    _lastPinchDistance = distance;
                    _lastPinchAngle = angle;
                    _pinching = true;
                    WasDragging = true;
                }
                else if (!_touchBlocked)
                {
                    if (distance > 1 && _lastPinchDistance > 1) Zoom(_lastPinchDistance / distance);
                    _yaw -= Mathf.DeltaAngle(_lastPinchAngle, angle);
                }
                _lastPinchDistance = distance;
                _lastPinchAngle = angle;
                return;
            }

            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                _touchFinger = touch.fingerId;
                _touchStart = _lastTouch = touch.position;
                _touchBlocked = IsOverUI(touch.fingerId);
                WasDragging = false;
                _pinching = false;
            }
            else if (_pinching || _touchFinger != touch.fingerId)
            {
                // Ending a pinch cannot immediately become a tap or jump the pan position.
                _touchFinger = touch.fingerId;
                _touchStart = _lastTouch = touch.position;
                _pinching = false;
                WasDragging = true;
            }

            if (!_touchBlocked && touch.phase == TouchPhase.Moved)
            {
                if ((touch.position - _touchStart).sqrMagnitude > DragThreshold * DragThreshold) WasDragging = true;
                if (WasDragging) PanPixels(touch.position - _lastTouch);
            }
            _lastTouch = touch.position;
            if (touch.phase == TouchPhase.Canceled) WasDragging = true;
        }

        private void PanPixels(Vector2 delta)
        {
            float unitsPerPixel = 2f * _distance * Mathf.Tan(_camera.fieldOfView * Mathf.Deg2Rad * 0.5f)
                                  / Mathf.Max(1, Screen.height);
            Vector3 right = Quaternion.Euler(0, _yaw, 0) * Vector3.right;
            Vector3 forward = Quaternion.Euler(0, _yaw, 0) * Vector3.forward;
            _target -= right * (delta.x * unitsPerPixel);
            _target -= forward * (delta.y * unitsPerPixel / Mathf.Sin(Pitch * Mathf.Deg2Rad));
            ClampTarget();
        }

        private void Zoom(float multiplier)
        {
            _distance = Mathf.Clamp(_distance * multiplier, MinimumDistance, MaximumDistance);
        }

        private void ClampTarget()
        {
            Vector3 offset = _target - _center;
            offset.y = 0;
            offset = Vector3.ClampMagnitude(offset, _radius * 0.95f);
            _target = _center + offset;
        }

        private void LateUpdate()
        {
            float blend = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
            _smoothedTarget = Vector3.Lerp(_smoothedTarget, _target, blend);
            _smoothedDistance = Mathf.Lerp(_smoothedDistance, _distance, blend);
            _smoothedYaw = Mathf.LerpAngle(_smoothedYaw, _yaw, blend);
            ApplyCamera();
        }

        private void ApplyCamera()
        {
            Quaternion rotation = Quaternion.Euler(Pitch, _smoothedYaw, 0);
            transform.position = _smoothedTarget - rotation * Vector3.forward * _smoothedDistance;
            transform.rotation = rotation;
        }
    }
}
