using System.Collections;
using UnityEngine;
using WebXR;

namespace WebARDemo
{
  /// <summary>
  /// Размещает объект в центре поля зрения (viewer-space) при старте сцены
  /// и при каждом входе/выходе из XR-сессии (WebXRManager.OnXRChange).
  /// Позиция берётся у камеры из WebXRCameraSet, которую двигает WebXR (TrackedPoseDriver),
  /// а объект остаётся в пространстве WebXRManager (мировые координаты сессии).
  /// </summary>
  public class ViewerSpacePlacer : MonoBehaviour
  {
    [Tooltip("Камера зрителя (Main Camera из WebXRCameraSet). Если пусто — Camera.main.")]
    public Transform viewer;

    [Tooltip("Расстояние от зрителя до объекта, м.")]
    public float distance = 0.6f;

    [Tooltip("Сколько кадров ждать после старта/смены режима, чтобы пришла первая поза viewer.")]
    public int framesToWait = 3;

    [Tooltip("true — объект всё время держится в центре экрана (HUD); false — ставится один раз и остаётся в мире.")]
    public bool followViewer = false;

    [Tooltip("Скорость вращения объекта, град/с (0 — без вращения).")]
    public float spinSpeed = 30f;

    private Coroutine placing;

    private void OnEnable()
    {
      WebXRManager.OnXRChange += OnXRChange;
    }

    private void OnDisable()
    {
      WebXRManager.OnXRChange -= OnXRChange;
    }

    private void Start()
    {
      if (viewer == null && Camera.main != null)
      {
        viewer = Camera.main.transform;
      }
      PlaceDeferred();
    }

    private void OnXRChange(WebXRState state, int viewsCount, Rect leftRect, Rect rightRect)
    {
      Debug.Log($"[ViewerSpacePlacer] XR state: {state}, views: {viewsCount}");
      PlaceDeferred();
    }

    private void PlaceDeferred()
    {
      if (!isActiveAndEnabled)
      {
        return;
      }
      if (placing != null)
      {
        StopCoroutine(placing);
      }
      placing = StartCoroutine(PlaceAfterFrames());
    }

    private IEnumerator PlaceAfterFrames()
    {
      for (int i = 0; i < framesToWait; i++)
      {
        yield return null;
      }
      Place();
      placing = null;
    }

    /// <summary>Ставит объект на луч взгляда на расстоянии distance.</summary>
    public void Place()
    {
      if (viewer == null)
      {
        return;
      }
      transform.position = viewer.position + viewer.forward * distance;
      // Повернуть «лицом» к зрителю, сохранив вертикаль.
      Vector3 toViewer = viewer.position - transform.position;
      toViewer.y = 0f;
      if (toViewer.sqrMagnitude > 0.0001f)
      {
        transform.rotation = Quaternion.LookRotation(-toViewer.normalized, Vector3.up);
      }
      Debug.Log($"[ViewerSpacePlacer] placed at {transform.position}");
    }

    private void LateUpdate()
    {
      if (followViewer)
      {
        Place();
      }
      if (spinSpeed != 0f)
      {
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
      }
    }
  }
}
