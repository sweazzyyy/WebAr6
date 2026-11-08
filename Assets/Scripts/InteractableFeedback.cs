using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace WebARDemo
{
  /// <summary>
  /// Визуальный отклик на взаимодействие XRI:
  ///  - hover (луч/палец на объекте): цвет светлеет, появляется жёлтый контур;
  ///  - select (захват): объект оранжевый, контур оранжевый и короткая «пульсация» контура.
  /// Контур — дочерний меш с отсечением передних граней (inverted hull) на URP Unlit.
  /// </summary>
  [RequireComponent(typeof(XRBaseInteractable))]
  public class InteractableFeedback : MonoBehaviour
  {
    public Renderer body;
    public Renderer outline;

    public Color hoverColor = new Color(0.45f, 0.8f, 1f);
    public Color selectColor = new Color(1f, 0.55f, 0.1f);
    public Color outlineHoverColor = new Color(1f, 0.95f, 0.3f);
    public Color outlineSelectColor = new Color(1f, 0.45f, 0f);

    [Tooltip("Толщина контура относительно размера объекта.")]
    public float outlineScale = 1.08f;
    [Tooltip("Насколько контур «вспухает» в момент захвата.")]
    public float pulseScale = 1.2f;
    public float pulseDuration = 0.2f;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    XRBaseInteractable m_Interactable;
    MaterialPropertyBlock m_Block;
    Color m_BaseColor;
    float m_PulseTime = -1f;

    void Awake()
    {
      m_Interactable = GetComponent<XRBaseInteractable>();
      m_Block = new MaterialPropertyBlock();
      if (body == null)
      {
        body = GetComponent<Renderer>();
      }
      m_BaseColor = body.sharedMaterial.HasProperty(BaseColorId) ? body.sharedMaterial.GetColor(BaseColorId) : Color.white;
    }

    void OnEnable()
    {
      m_Interactable.hoverEntered.AddListener(OnHoverChanged);
      m_Interactable.hoverExited.AddListener(OnHoverChanged);
      m_Interactable.selectEntered.AddListener(OnSelectEntered);
      m_Interactable.selectExited.AddListener(OnSelectExited);
      Refresh();
    }

    void OnDisable()
    {
      m_Interactable.hoverEntered.RemoveListener(OnHoverChanged);
      m_Interactable.hoverExited.RemoveListener(OnHoverChanged);
      m_Interactable.selectEntered.RemoveListener(OnSelectEntered);
      m_Interactable.selectExited.RemoveListener(OnSelectExited);
    }

    void OnHoverChanged(BaseInteractionEventArgs args)
    {
      Refresh();
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
      m_PulseTime = 0f;
      Debug.Log($"[Feedback] grabbed by {args.interactorObject.transform.name}");
      Refresh();
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
      Debug.Log($"[Feedback] released by {args.interactorObject.transform.name}, scale = {transform.localScale.x:F3}");
      Refresh();
    }

    void Refresh()
    {
      bool selected = m_Interactable.isSelected;
      bool hovered = m_Interactable.isHovered;

      SetColor(body, selected ? selectColor : hovered ? hoverColor : m_BaseColor);

      if (outline != null)
      {
        outline.enabled = selected || hovered;
        SetColor(outline, selected ? outlineSelectColor : outlineHoverColor);
        outline.transform.localScale = Vector3.one * outlineScale;
      }
    }

    void SetColor(Renderer r, Color c)
    {
      r.GetPropertyBlock(m_Block);
      m_Block.SetColor(BaseColorId, c);
      r.SetPropertyBlock(m_Block);
    }

    void Update()
    {
      if (m_PulseTime < 0f || outline == null)
      {
        return;
      }
      m_PulseTime += Time.deltaTime;
      float t = Mathf.Clamp01(m_PulseTime / pulseDuration);
      float s = Mathf.Lerp(pulseScale, outlineScale, t);
      outline.transform.localScale = Vector3.one * s;
      if (t >= 1f)
      {
        m_PulseTime = -1f;
      }
    }
  }
}
