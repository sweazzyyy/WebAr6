using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace WebARDemo
{
  /// <summary>
  /// Источник экранных жестов для XR Interaction Toolkit в WebXR.
  ///
  /// В мобильной WebXR AR-сессии касания экрана — это input source с handedness "none";
  /// WebXR Export переводит их в обычные touch-события canvas, а не в контроллеры.
  /// Штатный источник жестов XRI (TouchscreenGestureInputController) требует AR Foundation,
  /// которой нет в WebGL, поэтому жесты считаются здесь и отдаются штатным XRI-компонентам
  /// (ScreenSpaceRayPoseDriver, ScreenSpaceSelectInput, ScreenSpacePinchScaleInput)
  /// через ридеры TouchGesture*Reader (режим ObjectReference у XRInputValueReader).
  ///
  /// Жесты:
  ///  - 1 палец: drag (луч из точки касания, select удерживается) -> перемещение;
  ///  - 2 пальца: pinch (изменение расстояния между пальцами) -> масштаб;
  ///  - мышь (обычный браузер): ЛКМ = 1 палец, колесо во время drag = pinch.
  /// </summary>
  [DefaultExecutionOrder(-32000)] // раньше XRI ScreenSpace* (-31000) и XRInteractionManager (-105)
  public class WebXRTouchGestures : MonoBehaviour
  {
    [Tooltip("Мышь работает как один палец (удобно для проверки в обычном браузере).")]
    public bool mouseEmulation = true;

    [Tooltip("Сколько пикселей pinch даёт один щелчок колеса мыши.")]
    public float wheelPinchPixels = 40f;

    [Tooltip("Экранный ray interactor: hover разрешён только пока есть касание (иначе луч \"висит\" в последней позиции).")]
    public XRBaseInteractor screenInteractor;

    public int FingerCount { get; private set; }
    public bool DragActive { get; private set; }
    public Vector2 DragStart { get; private set; }
    public Vector2 DragCurrent { get; private set; }
    public bool PinchActive { get; private set; }
    public float PinchGapDelta { get; private set; }
    /// <summary>Текущее расстояние между пальцами (px) во время pinch; для колеса мыши — виртуальное.</summary>
    public float PinchGap { get; private set; }

    readonly List<Vector2> m_Points = new List<Vector2>(4);
    bool m_BlockUntilRelease;
    float m_PrevGap = -1f;
    float m_WheelGap = -1f;

    [Tooltip("Писать в консоль смену состояния жестов (для отладки на устройстве через chrome://inspect).")]
    public bool debugLog = false;

    string m_LastState;

    void Update()
    {
      UpdateGestures();
      if (screenInteractor != null)
      {
        screenInteractor.allowHover = FingerCount > 0 || DragActive || PinchActive;
      }
      if (debugLog)
      {
        string state = $"fingers={FingerCount} drag={DragActive} pinch={PinchActive} " +
                       $"src(ts={(Touchscreen.current != null ? Touchscreen.current.touches.Count(t => t.isInProgress) : -1)}, " +
#if ENABLE_LEGACY_INPUT_MANAGER
                       $"legacy={Input.touchCount}, " +
#endif
                       $"mouse={(Mouse.current != null && Mouse.current.leftButton.isPressed)})";
        if (state != m_LastState)
        {
          Debug.Log("[Gestures] " + state);
          m_LastState = state;
        }
      }
    }

    void UpdateGestures()
    {
      CollectPoints(m_Points);
      FingerCount = m_Points.Count;
      PinchActive = false;
      PinchGapDelta = 0f;

      if (m_Points.Count == 0)
      {
        DragActive = false;
        m_BlockUntilRelease = false;
        m_PrevGap = -1f;
        m_WheelGap = -1f;
        return;
      }

      if (m_Points.Count == 1)
      {
        m_PrevGap = -1f;
        if (!m_BlockUntilRelease)
        {
          if (!DragActive)
          {
            DragActive = true;
            DragStart = m_Points[0];
          }
          DragCurrent = m_Points[0];
        }
      }
      else
      {
        // Второй палец: drag заканчивается, начинается pinch. Новый drag — только после отпускания всех пальцев.
        DragActive = false;
        m_BlockUntilRelease = true;
        float gap = Vector2.Distance(m_Points[0], m_Points[1]);
        PinchGap = gap;
        if (m_PrevGap > 0f)
        {
          PinchGapDelta = gap - m_PrevGap;
        }
        m_PrevGap = gap;
        PinchActive = true;
      }

      // Эмуляция pinch колесом мыши во время drag
      if (mouseEmulation && DragActive && Mouse.current != null)
      {
        float wheel = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(wheel) > 0.01f)
        {
          PinchActive = true;
          PinchGapDelta = Mathf.Sign(wheel) * wheelPinchPixels;
          if (m_WheelGap <= 0f) m_WheelGap = 200f;
          m_WheelGap = Mathf.Max(20f, m_WheelGap + PinchGapDelta);
          PinchGap = m_WheelGap;
        }
      }
    }

    void CollectPoints(List<Vector2> points)
    {
      points.Clear();

      var ts = Touchscreen.current;
      if (ts != null)
      {
        foreach (var t in ts.touches)
        {
          if (t.isInProgress)
          {
            points.Add(t.position.ReadValue());
          }
        }
      }

#if ENABLE_LEGACY_INPUT_MANAGER
      if (points.Count == 0 && Input.touchCount > 0)
      {
        for (int i = 0; i < Input.touchCount; i++)
        {
          var t = Input.GetTouch(i);
          if (t.phase != UnityEngine.TouchPhase.Ended && t.phase != UnityEngine.TouchPhase.Canceled)
          {
            points.Add(t.position);
          }
        }
      }
#endif

      if (points.Count == 0 && mouseEmulation && Mouse.current != null && Mouse.current.leftButton.isPressed)
      {
        points.Add(Mouse.current.position.ReadValue());
      }
    }
  }
}
