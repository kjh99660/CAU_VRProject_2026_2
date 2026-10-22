using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;

namespace VRWebSwing
{
    public enum SwingHand { Left, Right }
    public enum SwingCommandKind { Attach, Release, Respawn, Restart, Jump }
    public struct SwingCommand
    {
        public SwingCommandKind kind;
        public SwingHand hand;
        public Ray ray;
    }

    // Update callbacks enqueue edges (and their original aim rays); FixedUpdate drains in order.
    public sealed class SwingInput : MonoBehaviour
    {
        public Transform head, leftHand, rightHand;
        public TrackedPoseDriver headTracking, leftTracking, rightTracking;
        public bool forceDesktop;
        [Tooltip("Desktop mouse look sensitivity in degrees per pixel.")] [Min(0.001f)] public float mouseSensitivity = 0.1f;
        [Tooltip("Capture the mouse automatically when Play starts without an HMD.")] public bool captureMouseOnStart = true;
        public bool IsVR { get; private set; }
        public Vector2 Move { get; private set; }
        public SwingHand PreviewHand { get; private set; } = SwingHand.Right;
        readonly Queue<SwingCommand> commands = new Queue<SwingCommand>(32);
        InputAction leftTrigger, rightTrigger, reset, restart, jump, move;
        float yaw, pitch;
        public int PendingCount => commands.Count;
        public bool DesktopMenuOpen { get; private set; }
        public event Action ComfortModeRequested, PauseRequested, AbortRequested, ExportRequested;
        InputAction comfortMode, pause, abort, export;

        void Awake()
        {
            comfortMode = new InputAction("Comfort mode", InputActionType.Button, "<Keyboard>/m");
            comfortMode.AddBinding("<XRController>{LeftHand}/primaryButton");
            pause = new InputAction("Pause virtual movement", InputActionType.Button, "<Keyboard>/p");
            pause.AddBinding("<Keyboard>/f1");
            pause.AddBinding("<XRController>{RightHand}/menuButton");
            abort = new InputAction("Stop experience", InputActionType.Button, "<Keyboard>/backspace");
            abort.AddBinding("<XRController>{RightHand}/secondaryButton");
            export = new InputAction("Export comfort CSV", InputActionType.Button, "<Keyboard>/f8");
            comfortMode.performed += ModePressed; pause.performed += PausePressed;
            abort.performed += AbortPressed; export.performed += ExportPressed;
            leftTrigger = new InputAction("LeftWeb", InputActionType.Button, "<XRController>{LeftHand}/triggerPressed");
            rightTrigger = new InputAction("RightWeb", InputActionType.Button, "<XRController>{RightHand}/triggerPressed");
            reset = new InputAction("Respawn", InputActionType.Button, "<XRController>{LeftHand}/secondaryButton");
            restart = new InputAction("Restart", InputActionType.Button, "<XRController>{LeftHand}/menuButton");
            restart.AddBinding("<Keyboard>/t");
            jump = new InputAction("Jump", InputActionType.Button, "<XRController>{RightHand}/primaryButton");
            move = new InputAction("Move", InputActionType.Value, "<XRController>{LeftHand}/primary2DAxis");
            leftTrigger.AddBinding("<Mouse>/leftButton");
            rightTrigger.AddBinding("<Mouse>/rightButton");
            reset.AddBinding("<Keyboard>/r"); jump.AddBinding("<Keyboard>/space");
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            leftTrigger.performed += LeftDown; leftTrigger.canceled += LeftUp;
            rightTrigger.performed += RightDown; rightTrigger.canceled += RightUp;
            reset.performed += ResetPressed; jump.performed += JumpPressed;
            restart.performed += RestartPressed;
        }
        void OnEnable()
        {
            comfortMode.Enable(); pause.Enable(); abort.Enable(); export.Enable();
            leftTrigger.Enable(); rightTrigger.Enable(); reset.Enable(); restart.Enable(); jump.Enable(); move.Enable();
        }
        void Start()
        {
            if (captureMouseOnStart && (forceDesktop || !UnityEngine.XR.InputDevices.GetDeviceAtXRNode(XRNode.Head).isValid))
                CaptureMouse();
        }
        void CaptureMouse()
        {
            if (Application.isBatchMode) return;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        void ReleaseMouse()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        void OnDisable()
        {
            comfortMode.Disable(); pause.Disable(); abort.Disable(); export.Disable();
            leftTrigger.Disable(); rightTrigger.Disable(); reset.Disable(); restart.Disable(); jump.Disable(); move.Disable();
            Clear(); ReleaseMouse();
        }
        void OnDestroy()
        {
            comfortMode.performed -= ModePressed; pause.performed -= PausePressed;
            abort.performed -= AbortPressed; export.performed -= ExportPressed;
            comfortMode.Dispose(); pause.Dispose(); abort.Dispose(); export.Dispose();
            leftTrigger.performed -= LeftDown; leftTrigger.canceled -= LeftUp;
            rightTrigger.performed -= RightDown; rightTrigger.canceled -= RightUp;
            reset.performed -= ResetPressed; jump.performed -= JumpPressed;
            restart.performed -= RestartPressed;
            leftTrigger.Dispose(); rightTrigger.Dispose(); reset.Dispose(); restart.Dispose(); jump.Dispose(); move.Dispose();
        }
        void LeftDown(InputAction.CallbackContext c) => Fire(c, SwingHand.Left);
        void ModePressed(InputAction.CallbackContext c) => ComfortModeRequested?.Invoke();
        void PausePressed(InputAction.CallbackContext c) => PauseRequested?.Invoke();
        void AbortPressed(InputAction.CallbackContext c) => AbortRequested?.Invoke();
        void ExportPressed(InputAction.CallbackContext c) => ExportRequested?.Invoke();
        void LeftUp(InputAction.CallbackContext c) => Enqueue(SwingCommandKind.Release, SwingHand.Left);
        void RightDown(InputAction.CallbackContext c) => Fire(c, SwingHand.Right);
        void RightUp(InputAction.CallbackContext c) => Enqueue(SwingCommandKind.Release, SwingHand.Right);
        void ResetPressed(InputAction.CallbackContext c) => Enqueue(SwingCommandKind.Respawn);
        void RestartPressed(InputAction.CallbackContext c) => Enqueue(SwingCommandKind.Restart);
        void JumpPressed(InputAction.CallbackContext c) => Enqueue(SwingCommandKind.Jump);
        void Fire(InputAction.CallbackContext context, SwingHand hand)
        {
            if (!IsVR && context.control.device is Mouse && DesktopMenuOpen) return;
            if (!IsVR && context.control.device is Mouse) CaptureMouse();
            Enqueue(SwingCommandKind.Attach, hand);
        }
        public void SetDesktopMenuOpen(bool open)
        {
            DesktopMenuOpen = open && !IsVR;
            if (IsVR) return;
            if (open) ReleaseMouse(); else CaptureMouse();
        }
        public void Enqueue(SwingCommandKind kind, SwingHand hand = SwingHand.Right)
        {
            if (kind == SwingCommandKind.Attach) PreviewHand = hand;
            commands.Enqueue(new SwingCommand { kind = kind, hand = hand, ray = Aim(hand) });
        }
        public bool TryConsume(out SwingCommand command)
        {
            if (commands.Count > 0) { command = commands.Dequeue(); return true; }
            command = default; return false;
        }
        public void Clear() { commands.Clear(); Move = Vector2.zero; }
        public void ShiftOrigin(Vector3 offset)
        {
            int count = commands.Count;
            for (int i = 0; i < count; ++i)
            {
                var command = commands.Dequeue();
                command.ray.origin -= offset;
                commands.Enqueue(command);
            }
        }
        public Transform Hand(SwingHand hand) => hand == SwingHand.Left ? leftHand : rightHand;
        // Desktop shots follow the screen-center crosshair; the rendered web still starts at its hand.
        public Ray Aim(SwingHand hand) => IsVR
            ? new Ray(Hand(hand).position, Hand(hand).forward)
            : new Ray(head.position, head.forward);
        void Update()
        {
            bool vr = !forceDesktop && UnityEngine.XR.InputDevices.GetDeviceAtXRNode(XRNode.Head).isValid;
            if (vr != IsVR)
            {
                IsVR = vr; Clear();
                headTracking.enabled = leftTracking.enabled = rightTracking.enabled = vr;
                if (vr) ReleaseMouse();
                if (!vr) { head.localPosition = new Vector3(0, 1.65f, 0); yaw = pitch = 0; }
            }
            Move = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1);
            if (IsVR) return;
            if (DesktopMenuOpen) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ReleaseMouse();
            if (Mouse.current != null && Mouse.current.middleButton.wasPressedThisFrame) CaptureMouse();
            if (Mouse.current != null && Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 delta = Mouse.current.delta.ReadValue() * mouseSensitivity;
                yaw += delta.x; pitch = Mathf.Clamp(pitch - delta.y, -80, 80);
            }
            head.localRotation = Quaternion.Euler(pitch, yaw, 0);
            PlaceDesktopHand(leftHand, -0.25f); PlaceDesktopHand(rightHand, 0.25f);
        }
        void PlaceDesktopHand(Transform hand, float x)
        {
            hand.position = head.TransformPoint(new Vector3(x, -0.2f, 0.3f));
            hand.rotation = head.rotation;
        }
    }
}
