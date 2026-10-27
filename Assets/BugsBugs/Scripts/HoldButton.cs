using UnityEngine;
using UnityEngine.EventSystems;

// 누르고 있는 동안 IsHeld = true 인 버튼 (홀드 조작).
// 손가락이 버튼 밖으로 미끄러져도 뗄 때까지는 누르고 있는 것으로 봄.
public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    int pointer = int.MinValue;

    public bool IsHeld { get { return pointer != int.MinValue; } }

    public void OnPointerDown(PointerEventData e)
    {
        if (pointer == int.MinValue) pointer = e.pointerId;
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (e.pointerId == pointer) pointer = int.MinValue;
    }

    public void Release()
    {
        pointer = int.MinValue;
    }

    void OnDisable()
    {
        Release();
    }
}
