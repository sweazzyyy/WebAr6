using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using UnityEngine.Scripting;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using WebXR;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WebXR.XRITBridge
{
#if UNITY_EDITOR
    [InitializeOnLoad] // Call static class constructor in editor.
#endif
    [InputControlLayout(stateType = typeof(XRSimulatedHMDState), isGenericTypeOfDevice = false, displayName = "WebXR HMD", updateBeforeRender = true)]
    [Preserve]
    public class WebXRInteractionHeadset : XRHMD
    {
#if UNITY_EDITOR
        static WebXRInteractionHeadset()
        {
            InputSystem.RegisterLayout<WebXRInteractionHeadset>(
                matches: new InputDeviceMatcher()
                    .WithInterface("WebXRHeadset"));
        }
#endif

        // Patched for WebXR Export 0.25:
        // - WebXRManager.OnHeadsetUpdate now only provides projection matrices, so the head pose
        //   is read from the camera of the WebXR rig (driven by the WebXR tracking provider).
        // - Cameras are no longer created here: WebXRCamera on the rig already switches
        //   between Main / VR / AR cameras, creating extra cameras would render twice.
        internal Transform head;

        protected override void FinishSetup()
        {
            base.FinishSetup();
            WebXRManager.OnHeadsetUpdate += OnHeadsetUpdate;
        }

        private void OnHeadsetUpdate(Matrix4x4 leftProjectionMatrix, Matrix4x4 rightProjectionMatrix)
        {
            if (head == null)
            {
                var cam = Camera.main;
                if (cam == null)
                    return;
                head = cam.transform;
            }

            var position = head.localPosition;
            var rotation = head.localRotation;

            XRSimulatedHMDState state = new();
            state.isTracked = true;
            state.trackingState = (int)(InputTrackingState.Position | InputTrackingState.Rotation);
            state.leftEyePosition = position;
            state.leftEyeRotation = rotation;
            state.rightEyePosition = position;
            state.rightEyeRotation = rotation;
            state.devicePosition = position;
            state.deviceRotation = rotation;
            state.centerEyePosition = position;
            state.centerEyeRotation = rotation;

            InputSystem.QueueStateEvent(this, state);
        }
    }

    public class WebXRHeadsetAdapter : MonoBehaviour
    {
        [Tooltip("Head transform of the WebXR rig. If empty, Camera.main is used.")]
        public Transform head;

        private void Awake()
        {
#if UNITY_WEBGL
            DontDestroyOnLoad(this);

            var device = InputSystem.AddDevice<WebXRInteractionHeadset>();
            device.head = head;
#endif
        }
    }
}
