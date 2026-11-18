using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;

public class AuthManager : MonoBehaviour
{
    public string apiKey;
    public TMP_Text statusText;                                  // Login 씬의 상태 글자
    public static string Uid = "", IdToken = "", Name = "";      // static: 씬이 바뀌어도 남음

    public void GuestLogin() { statusText.text = "로그인 중..."; StartCoroutine(SignIn()); }   // 게스트 로그인 버튼
    public static void Logout() { Debug.Log("logout " + Name); Uid = ""; IdToken = ""; Name = ""; SceneManager.LoadScene("Login"); }

    IEnumerator SignIn()
    {
        string saved = PlayerPrefs.GetString("refreshToken", "");     // 저장해 둔 증명서. 처음이면 빈 문자열
        string signUp = "https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=" + apiKey;
        string refresh = "https://securetoken.googleapis.com/v1/token?key=" + apiKey;
        UnityWebRequest req = saved == ""                             // 처음: 새 손님 가입 / 아니면: 같은 손님의 새 토큰
            ? UnityWebRequest.Post(signUp, "{\"returnSecureToken\":true}", "application/json")
            : UnityWebRequest.Post(refresh, "grant_type=refresh_token&refresh_token=" + saved, "application/x-www-form-urlencoded");
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success) { PlayerPrefs.DeleteKey("refreshToken"); statusText.text = req.error; yield break; }
        AuthResponse r = JsonUtility.FromJson<AuthResponse>(req.downloadHandler.text);
        Uid = saved == "" ? r.localId : r.user_id;  IdToken = saved == "" ? r.idToken : r.id_token;
        PlayerPrefs.SetString("refreshToken", saved == "" ? r.refreshToken : r.refresh_token); PlayerPrefs.Save();
        Name = "손님 " + Uid.Substring(0, 6); Debug.Log("login " + Name); SceneManager.LoadScene("Game");   // 로그인 끝 → 게임 씬으로 (로그아웃은 게임 로비 / 일시정지 화면에 있음)
    }
}

[System.Serializable] public class AuthResponse { public string idToken, localId, refreshToken, id_token, user_id, refresh_token; }
