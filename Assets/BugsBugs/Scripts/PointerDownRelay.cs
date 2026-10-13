using System;
using UnityEngine;
using UnityEngine.EventSystems;

// 터치(또는 마우스)가 눌리는 순간을 알려주는 작은 도우미 컴포넌트.
// 탭 대시 영역, 시작 화면, 실드 버튼에서 사용.
public class PointerDownRelay : MonoBehaviour, IPointerDownHandler
{
    public Action<PointerEventData> onDown;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (onDown != null) onDown(eventData);
    }
}
