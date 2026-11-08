using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace WebARDemo
{
  /// <summary>
  /// Grab-трансформер XRI для масштабирования pinch-жестом.
  /// Интерактор (XRRayInteractor) в режиме ScaleMode.DistanceDelta отдаёт scaleValue
  /// (из ScreenSpacePinchScaleInput), трансформер меняет масштаб объекта.
  /// Нужен, т.к. штатный ARTransformer XRI компилируется только с AR Foundation (её нет в WebGL),
  /// а XRGeneralGrabTransformer одной рукой поддерживает только ScaleOverTime.
  /// Должен стоять ПОСЛЕ XRGeneralGrabTransformer в списке single grab transformers.
  /// </summary>
  public class PinchScaleGrabTransformer : XRBaseGrabTransformer
  {
    [Tooltip("Изменение масштаба на единицу pinch (XRI отдаёт пиксели * screenDpiRatio, ~0.17 на 16 px).")]
    public float sensitivity = 0.25f;

    [Tooltip("Минимальный масштаб относительно исходного.")]
    public float minimumScaleRatio = 0.3f;

    [Tooltip("Максимальный масштаб относительно исходного.")]
    public float maximumScaleRatio = 4f;

    public bool debugLog = false;

    Vector3 m_InitialScale;
    bool m_HasInitialScale;

    protected override RegistrationMode registrationMode => RegistrationMode.Single;

    public override void OnLink(XRGrabInteractable grabInteractable)
    {
      base.OnLink(grabInteractable);
      if (!m_HasInitialScale)
      {
        m_InitialScale = grabInteractable.transform.localScale;
        m_HasInitialScale = true;
      }
    }

    public override void Process(XRGrabInteractable grabInteractable, XRInteractionUpdateOrder.UpdatePhase updatePhase, ref Pose targetPose, ref Vector3 localScale)
    {
      // scaleValue обновляется раз в кадр (фаза Dynamic) — применяем один раз, иначе масштаб удвоится
      if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic)
      {
        return;
      }
      if (grabInteractable.interactorsSelecting.Count != 1)
      {
        return;
      }
      if (grabInteractable.interactorsSelecting[0] is not IXRScaleValueProvider provider ||
          provider.scaleMode != UnityEngine.XR.Interaction.Toolkit.Interactors.ScaleMode.DistanceDelta)
      {
        return;
      }

      float delta = provider.scaleValue;
      if (Mathf.Approximately(delta, 0f))
      {
        return;
      }
      if (debugLog)
      {
        Debug.Log($"[PinchScale] scaleValue {delta:F2} -> scale {localScale.x:F3}");
      }

      float factor = Mathf.Max(0.01f, 1f + delta * sensitivity);
      Vector3 newScale = localScale * factor;
      float ratio = newScale.x / Mathf.Max(0.0001f, m_InitialScale.x);
      if (ratio < minimumScaleRatio)
      {
        newScale = m_InitialScale * minimumScaleRatio;
      }
      else if (ratio > maximumScaleRatio)
      {
        newScale = m_InitialScale * maximumScaleRatio;
      }
      localScale = newScale;
    }
  }
}
