using UnityEngine;
using UnityEngine.EventSystems;

// 좌측 하단 가상 조이스틱 (Stick) - 플로팅 방식.
// 이 컴포넌트는 넓은 터치 영역(zone)에 붙고, 누른 자리를 중심으로 조이스틱이 생김.
// Value : -1 ~ 1 범위의 방향 값 (상하좌우 자유 이동)
public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IInitializePotentialDragHandler
{
    public RectTransform stickBase; // 조이스틱 바탕 (zone 의 자식, 앵커는 zone 좌하단)
    public RectTransform knob;      // 가운데 움직이는 손잡이 (stickBase 의 자식)
    public float radius = 60f;      // 손잡이가 움직일 수 있는 최대 거리 (UI 픽셀)
    public float deadZone = 0.08f;  // 아주 작은 움직임은 무시
    public bool followFinger = false; // true : 손가락이 반경 밖으로 가면 바탕이 따라감
    public bool hideWhenIdle = true;  // true : 누르고 있을 때만 보임
    public float idleAlpha = 0.45f;   // 손 뗐을 때 흐리게 (기본 위치 안내용)
    public float activeAlpha = 1f;    // 누르고 있을 때 진하게

    CanvasGroup group;

    public Vector2 Value { get; private set; }
    public bool IsHeld { get { return activePointer != int.MinValue; } }

    RectTransform zone;
    Vector2 homePos;                // 손을 떼면 돌아갈 기본 위치
    bool homeSaved;
    int activePointer = int.MinValue;

    RectTransform Zone { get { if (zone == null) zone = (RectTransform)transform; return zone; } }

    // 드래그 임계값(기본 10px, 모바일은 더 큼)을 꺼서 손가락이 조금만 움직여도 바로 반응
    public void OnInitializePotentialDrag(PointerEventData e)
    {
        e.useDragThreshold = false;
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (activePointer != int.MinValue) return; // 이미 다른 손가락이 잡고 있음
        Vector2 local;
        if (!ToLocal(e, out local)) return;
        SaveHome();
        activePointer = e.pointerId;
        stickBase.anchoredPosition = ClampBase(local); // 누른 자리를 중심으로 조이스틱 생성
        stickBase.gameObject.SetActive(true);
        SetAlpha(activeAlpha);
        UpdateStick(local);
    }

    public void OnDrag(PointerEventData e)
    {
        if (e.pointerId != activePointer) return;
        Vector2 local;
        if (ToLocal(e, out local)) UpdateStick(local);
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
        if (stickBase != null && homeSaved) stickBase.anchoredPosition = homePos;
        if (stickBase != null && hideWhenIdle) stickBase.gameObject.SetActive(false);
        SetAlpha(idleAlpha);
    }

    void SetAlpha(float a)
    {
        if (stickBase == null) return;
        if (group == null)
        {
            group = stickBase.GetComponent<CanvasGroup>();
            if (group == null) group = stickBase.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
        }
        group.alpha = a;
    }

    void OnDisable()
    {
        ResetStick();
    }

    void SaveHome()
    {
        if (homeSaved || stickBase == null) return;
        homePos = stickBase.anchoredPosition;
        homeSaved = true;
    }

    bool ToLocal(PointerEventData e, out Vector2 local)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Zone, e.position, e.pressEventCamera, out local))
            return false;
        local -= Zone.rect.min; // zone 좌하단 기준 좌표로 변환 (stickBase 앵커와 맞춤)
        return true;
    }

    // 조이스틱 바탕이 zone 밖으로 반 이상 나가지 않게
    Vector2 ClampBase(Vector2 p)
    {
        Vector2 size = Zone.rect.size;
        float h = stickBase.rect.width * 0.5f;
        p.x = Mathf.Clamp(p.x, h * 0.6f, Mathf.Max(h * 0.6f, size.x - h * 0.6f));
        p.y = Mathf.Clamp(p.y, h * 0.6f, Mathf.Max(h * 0.6f, size.y - h * 0.6f));
        return p;
    }

    void UpdateStick(Vector2 local)
    {
        Vector2 offset = local - stickBase.anchoredPosition;
        // (followFinger 일 때만) 손가락이 반경 밖으로 나가면 바탕이 따라옴
        if (followFinger && offset.magnitude > radius)
        {
            stickBase.anchoredPosition = ClampBase(local - offset.normalized * radius);
            offset = local - stickBase.anchoredPosition;
        }

        Vector2 clamped = Vector2.ClampMagnitude(offset, radius);
        if (knob != null) knob.anchoredPosition = clamped;

        Vector2 v = clamped / radius;
        Value = v.magnitude < deadZone ? Vector2.zero : v;
    }
}
