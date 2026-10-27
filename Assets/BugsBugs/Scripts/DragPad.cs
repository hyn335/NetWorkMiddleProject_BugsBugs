using UnityEngine;
using UnityEngine.EventSystems;

// 레이디버그식 드래그 조작.
// 화면 아무 곳이나 누르고 끌면, 손가락이 움직인 만큼 캐릭터가 똑같이 움직임 (상대 이동).
// 손가락이 캐릭터를 가리지 않고, 조이스틱처럼 위치를 찾을 필요가 없음.
// PC 에서는 마우스로 누르고 끌면 똑같이 동작.
public class DragPad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IInitializePotentialDragHandler
{
    public float sensitivity = 1.2f; // 1 = 손가락이 움직인 거리 그대로, 크면 더 많이 움직임

    int activePointer = int.MinValue;
    Vector2 pending; // 아직 반영 안 된 손가락 이동량 (화면 픽셀)

    public bool IsHeld { get { return activePointer != int.MinValue; } }

    public void OnInitializePotentialDrag(PointerEventData e)
    {
        e.useDragThreshold = false; // 조금만 움직여도 바로 반응
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (activePointer != int.MinValue) return; // 첫 번째 손가락만 사용
        activePointer = e.pointerId;
    }

    public void OnDrag(PointerEventData e)
    {
        if (e.pointerId != activePointer) return;
        pending += e.delta;
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (e.pointerId != activePointer) return;
        activePointer = int.MinValue;
    }

    // 이번 프레임에 움직일 거리 (월드 단위). 읽으면 0 으로 비워짐
    public Vector2 ConsumeWorldDelta(Camera cam)
    {
        Vector2 d = pending;
        pending = Vector2.zero;
        if (cam == null || Screen.height <= 0) return Vector2.zero;
        float unitsPerPixel = cam.orthographicSize * 2f / Screen.height;
        return d * unitsPerPixel * sensitivity;
    }

    public void ResetPad()
    {
        activePointer = int.MinValue;
        pending = Vector2.zero;
    }

    void OnDisable()
    {
        ResetPad();
    }
}
