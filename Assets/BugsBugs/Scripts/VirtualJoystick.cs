using UnityEngine;
using UnityEngine.EventSystems;

// 좌측 하단 가상 조이스틱 (Stick).
// Value : -1 ~ 1 범위의 방향 값 (상하좌우 자유 이동)
public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public RectTransform knob;      // 가운데 움직이는 손잡이
    public float radius = 60f;      // 손잡이가 움직일 수 있는 최대 거리 (UI 픽셀)

    public Vector2 Value { get; private set; }

    RectTransform rect;
    int activePointer = int.MinValue;

    void Awake()
    {
        rect = (RectTransform)transform;
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (activePointer != int.MinValue) return; // 이미 다른 손가락이 잡고 있음
        activePointer = e.pointerId;
        UpdateStick(e);
    }

    public void OnDrag(PointerEventData e)
    {
        if (e.pointerId != activePointer) return;
        UpdateStick(e);
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (e.pointerId != activePointer) return;
        ResetStick();
    }

    public void ResetStick()
    {
        activePointer = int.MinValue;
        Value = Vector2.zero;
        if (knob != null) knob.anchoredPosition = Vector2.zero;
    }

    void OnDisable()
    {
        ResetStick();
    }

    void UpdateStick(PointerEventData e)
    {
        if (rect == null) rect = (RectTransform)transform;
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, e.pressEventCamera, out local))
            return;

        Vector2 clamped = Vector2.ClampMagnitude(local, radius);
        if (knob != null) knob.anchoredPosition = clamped;

        Vector2 v = clamped / radius;
        // 아주 작은 움직임은 무시 (데드존)
        Value = v.magnitude < 0.1f ? Vector2.zero : v;
    }
}
