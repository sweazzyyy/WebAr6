using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using XRControllerDevice = UnityEngine.InputSystem.XR.XRController;

namespace WebARDemo
{
  /// <summary>
  /// Включает луч контроллера (hover/select/линия), только когда устройство из webxr-xrit-bridge
  /// реально отслеживается. Иначе луч стоит в начале координат и «наводится» на объект без контроллера
  /// (например, на телефоне, где контроллеров нет).
  /// </summary>
  public class ControllerRayGate : MonoBehaviour
  {
    [Tooltip("Usage устройства в Input System: LeftHand или RightHand.")]
    public string usage = "RightHand";

    XRBaseInteractor m_Interactor;
    XRInteractorLineVisual m_LineVisual;
    LineRenderer m_Line;
    bool m_Tracked = true;
    bool m_LastTrigger;

    [Tooltip("Писать в консоль состояние трекинга/триггера (отладка).")]
    public bool debugLog = false;

    void Awake()
    {
      m_Interactor = GetComponent<XRBaseInteractor>();
      m_LineVisual = GetComponent<XRInteractorLineVisual>();
      m_Line = GetComponent<LineRenderer>();
      Apply(false);
    }

    void Update()
    {
      var device = InputSystem.GetDevice<XRControllerDevice>(usage);
      bool tracked = device != null && device.isTracked.isPressed;
      if (tracked != m_Tracked)
      {
        Apply(tracked);
        if (debugLog) Debug.Log($"[RayGate] {usage} tracked={tracked} pos={transform.localPosition} fwd={transform.forward}");
      }
      bool trig = device != null && device.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("triggerPressed") is { } b && b.isPressed;
      if (trig != m_LastTrigger)
      {
        m_LastTrigger = trig;
        string hitName = "none";
        if (m_Interactor is XRRayInteractor ray && ray.TryGetCurrent3DRaycastHit(out var hit))
        {
          hitName = hit.collider.name;
        }
        if (debugLog) Debug.Log($"[RayGate] {usage} trigger={trig} pos={transform.localPosition} fwd={transform.forward} hit={hitName} allowHover={m_Interactor.allowHover}");
      }
    }

    void Apply(bool tracked)
    {
      m_Tracked = tracked;
      if (m_Interactor != null)
      {
        m_Interactor.allowHover = tracked;
        m_Interactor.allowSelect = tracked;
      }
      if (m_LineVisual != null)
      {
        m_LineVisual.enabled = tracked;
      }
      if (m_Line != null)
      {
        m_Line.enabled = tracked;
      }
    }
  }
}
