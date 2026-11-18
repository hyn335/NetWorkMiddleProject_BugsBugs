using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class ScoreSaver : MonoBehaviour
{
    public string dbUrl;                            // Realtime Database 주소. 끝에 / 없이
    public static ScoreSaver Instance;              // 다른 스크립트에서 ScoreSaver.Instance 로 접근
    bool busy;                                      // 저장 요청이 진행 중인가

    void Awake() { Instance = this; }

    public void Save(int score)
    {
        if (AuthManager.Uid == "" || busy) return;  // 로그인 전이거나 저장 중이면 보내지 않음
        busy = true; StartCoroutine(Put(score));
    }

    IEnumerator Put(int score)
    {
        string url = dbUrl.TrimEnd('/') + "/scores/" + AuthManager.Uid + ".json?auth=" + AuthManager.IdToken;   // 주소 끝에 / 가 있어도 //scores 가 되지 않게
        UnityWebRequest get = UnityWebRequest.Get(url);
        yield return get.SendWebRequest();          // 서버에 있는 내 기록을 먼저 읽음. 없으면 null
        string old = get.downloadHandler.text;
        int best = old.StartsWith("{") ? JsonUtility.FromJson<ScoreData>(old).score : 0;
        if (score <= best) { Debug.Log("not saved: server best " + best + " >= " + score); busy = false; yield break; }  // 서버 기록이 더 높으면 덮어쓰지 않음
        string body = "{\"name\":\"" + AuthManager.Name + "\",\"score\":" + score + "}";
        UnityWebRequest req = UnityWebRequest.Put(url, body);
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();
        Debug.Log(req.result == UnityWebRequest.Result.Success ? "saved" : "save failed " + req.error);
        busy = false;
    }
}

[System.Serializable] public class ScoreData { public string name; public int score; }
