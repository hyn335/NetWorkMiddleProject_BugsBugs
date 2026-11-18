using UnityEngine;
using TMPro;

public class MenuUI : MonoBehaviour
{
    public TMP_Text uidText;

    void Start() { uidText.text = AuthManager.Name; }

    public void Logout() { AuthManager.Logout(); }      // 로그아웃 버튼
}
