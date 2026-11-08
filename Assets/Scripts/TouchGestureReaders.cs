using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

namespace WebARDemo
{
  /// <summary>Ридер Vector2 для XRI: позиция начала/текущая позиция drag из <see cref="WebXRTouchGestures"/>.</summary>
  public class TouchGestureVector2Reader : MonoBehaviour, IXRInputValueReader<Vector2>
  {
    public enum Kind { DragStart, DragCurrent }

    public WebXRTouchGestures source;
    public Kind kind;

    public Vector2 ReadValue()
    {
      TryReadValue(out var v);
      return v;
    }

    public bool TryReadValue(out Vector2 value)
    {
      if (source == null || !source.DragActive)
      {
        value = default;
        return false;
      }
      value = kind == Kind.DragStart ? source.DragStart : source.DragCurrent;
      return true;
    }
  }

  /// <summary>Ридер float для XRI: изменение расстояния между пальцами (pinch) за кадр.</summary>
  public class TouchGestureFloatReader : MonoBehaviour, IXRInputValueReader<float>
  {
    public WebXRTouchGestures source;

    public float ReadValue()
    {
      TryReadValue(out var v);
      return v;
    }

    public bool TryReadValue(out float value)
    {
      if (source == null || !source.PinchActive)
      {
        value = 0f;
        return false;
      }
      value = source.PinchGapDelta;
      return true;
    }
  }

  /// <summary>Ридер int для XRI: число пальцев на экране.</summary>
  public class TouchGestureIntReader : MonoBehaviour, IXRInputValueReader<int>
  {
    public WebXRTouchGestures source;

    public int ReadValue()
    {
      TryReadValue(out var v);
      return v;
    }

    public bool TryReadValue(out int value)
    {
      value = source != null ? source.FingerCount : 0;
      return source != null;
    }
  }
}
