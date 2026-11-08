using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace WebARDemo
{
  /// <summary>
  /// Grab-трансформер XRI для масштабирования pinch-жестом на телефоне.
  ///
  /// Работает, когда объект удерживает экранный XRRayInteractor (ScaleMode.DistanceDelta — луч касания)
  /// и в <see cref="WebXRTouchGestures"/> идёт pinch. Масштаб абсолютный:
  ///   scale = scaleAtPinchStart * (текущее расстояние между пальцами / расстояние в начале pinch),
  /// поэтому не зависит от частоты кадров (покадровые дельты терялись, если кадр не совпадал с touch-событием).
  ///
  /// Нужен, т.к. штатный ARTransformer XRI компилируется только с AR Foundation (её нет в WebGL),
  /// а XRGeneralGrabTransformer одной «рукой» масштабирует только в режиме ScaleOverTime.
  /// Должен стоять ПОСЛЕ XRGeneralGrabTransformer в списке single grab transformers.
  /// </summary>
  public class PinchScaleGrabTransformer : XRBaseGrabTransformer
  {
    public WebXRTouchGestures gestures;

    [Tooltip("Минимальный масштаб относительно исходного.")]
    public float minimumScaleRatio = 0.3f;

    [Tooltip("Максимальный масштаб относительно исходного.")]
    public float maximumScaleRatio = 4f;

    public bool debugLog = false;

    Vector3 m_InitialScale;
    bool m_HasInitialScale;

    bool m_InPinch;
    Vector3 m_PinchStartScale;
    float m_PinchStartGap;

    protected override RegistrationMode registrationMode => RegistrationMode.Single;

    public override void OnLink(XRGrabInteractable grabInteractable)
    {
      base.OnLink(grabInteractable);
      if (!m_HasInitialScale)
      {
        m_InitialScale = grabInteractable.transform.localScale;
        m_HasInitialScale = true;
      }
      if (gestures == null)
      {
        gestures = FindAnyObjectByType<WebXRTouchGestures>();
      }
    }

    public override void OnGrab(XRGrabInteractable grabInteractable)
    {
      base.OnGrab(grabInteractable);
      m_InPinch = false;
    }

    public override void Process(XRGrabInteractable grabInteractable, XRInteractionUpdateOrder.UpdatePhase updatePhase, ref Pose targetPose, ref Vector3 localScale)
    {
      if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic &&
          updatePhase != XRInteractionUpdateOrder.UpdatePhase.OnBeforeRender)
      {
        return;
      }

      bool pinching = gestures != null && gestures.PinchActive && gestures.PinchGap > 1f &&
                      grabInteractable.interactorsSelecting.Count == 1 &&
                      grabInteractable.interactorsSelecting[0] is IXRScaleValueProvider provider &&
                      provider.scaleMode == UnityEngine.XR.Interaction.Toolkit.Interactors.ScaleMode.DistanceDelta;

      if (!pinching)
      {
        m_InPinch = false;
        return;
      }

      if (!m_InPinch)
      {
        m_InPinch = true;
        m_PinchStartScale = localScale;
        m_PinchStartGap = gestures.PinchGap;
        if (debugLog)
        {
          Debug.Log($"[PinchScale] start gap={m_PinchStartGap:F0} scale={localScale.x:F3}");
        }
      }

      Vector3 newScale = m_PinchStartScale * (gestures.PinchGap / m_PinchStartGap);
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
