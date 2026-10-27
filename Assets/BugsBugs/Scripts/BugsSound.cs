using UnityEngine;

// 효과음 - 소리 파일 없이 코드로 합성. "벌레 숲" 컨셉:
//   귀뚜라미 울음, 날갯짓 붕~, 말벌 윙윙, 뾱 터지는 소리, 나무 껍질 톡, 이슬방울 똑
// Assets/Resources/BugsBugs/Sounds/ 에 같은 이름의 소리 파일(Dash.wav 등)을 넣으면 그 소리로 교체됨.
public class BugsSound : MonoBehaviour
{
    public enum Sfx { Start, Click, Dash, Shield, Item, Kill, Warn, LevelUp, Die, NewBest, Roll, Worm }

    [Range(0f, 1f)] public float volume = 0.8f;

    AudioSource src;
    AudioClip[] clips;
    float lastKillTime = -1f;

    // 소리 끄기 (PlayerPrefs 에 저장돼서 다음에 켜도 유지)
    public bool Muted
    {
        get { return src != null && src.mute; }
        set
        {
            if (src != null) src.mute = value;
            PlayerPrefs.SetInt("BugsBugs_Mute", value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    void Awake()
    {
        src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.mute = PlayerPrefs.GetInt("BugsBugs_Mute", 0) == 1;
        BuildClips();
    }

    public void Play(Sfx s)
    {
        if (src == null || clips == null) return;
        // 여러 마리를 한꺼번에 없앨 때 소리가 겹쳐서 시끄러워지지 않게
        if (s == Sfx.Kill)
        {
            if (Time.unscaledTime - lastKillTime < 0.06f) return;
            lastKillTime = Time.unscaledTime;
        }
        var c = clips[(int)s];
        if (c != null) src.PlayOneShot(c, volume);
    }

    // ================================================================= 합성
    const int Rate = 22050;
    enum Wave { Sine, Saw, Noise }

    void BuildClips()
    {
        var names = System.Enum.GetNames(typeof(Sfx));
        clips = new AudioClip[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            var custom = Resources.Load<AudioClip>("BugsBugs/Sounds/" + names[i]);
            clips[i] = custom != null ? custom : Synth((Sfx)i);
        }
    }

    static AudioClip Synth(Sfx s)
    {
        float[] b;
        switch (s)
        {
            case Sfx.Click: // 잔가지 톡
                b = Buf(0.04f);
                Add(b, 0f, 0.03f, 2200f, 1100f, Wave.Sine, 0.35f, decay: 6f);
                break;

            case Sfx.Start: // 귀뚜라미 "찌르르 찌르르"
                b = Buf(0.32f);
                Chirp(b, 0f, 0.11f, 4300f, 0.22f);
                Chirp(b, 0.17f, 0.11f, 4500f, 0.22f);
                break;

            case Sfx.Dash: // 날갯짓 "부웅" (빠른 떨림 + 바람)
                b = Buf(0.24f);
                Add(b, 0f, 0.22f, 150f, 260f, Wave.Saw, 0.32f, amHz: 48f, amDepth: 0.85f, lowpass: 1400f, attack: 0.02f, decay: 2f);
                Add(b, 0f, 0.2f, 0f, 0f, Wave.Noise, 0.14f, lowpass: 2200f, attack: 0.03f, decay: 2.5f);
                break;

            case Sfx.Shield: // 등껍질 닫히는 "톡" + 반짝이는 울림
                b = Buf(0.45f);
                Add(b, 0f, 0.09f, 520f, 240f, Wave.Sine, 0.5f, decay: 4f);
                Add(b, 0.01f, 0.03f, 0f, 0f, Wave.Noise, 0.12f, lowpass: 3000f, decay: 5f);
                Add(b, 0.04f, 0.4f, 1320f, 1320f, Wave.Sine, 0.12f, amHz: 9f, amDepth: 0.6f, attack: 0.05f, decay: 2f);
                Add(b, 0.04f, 0.4f, 1980f, 1980f, Wave.Sine, 0.06f, amHz: 11f, amDepth: 0.6f, attack: 0.05f, decay: 2f);
                break;

            case Sfx.Item: // 이슬방울 "똑 똑" (위로 튀는 물방울)
                b = Buf(0.22f);
                Add(b, 0f, 0.06f, 650f, 1500f, Wave.Sine, 0.4f, decay: 3f);
                Add(b, 0.08f, 0.08f, 900f, 2100f, Wave.Sine, 0.35f, decay: 3f);
                break;

            case Sfx.Kill: // 벌레 "뾱" (작게 터지는 소리)
                b = Buf(0.09f);
                Add(b, 0f, 0.06f, 700f, 160f, Wave.Sine, 0.4f, decay: 4f);
                Add(b, 0f, 0.05f, 0f, 0f, Wave.Noise, 0.25f, lowpass: 1600f, decay: 5f);
                break;

            case Sfx.Warn: // 거미줄 쏘는 "슉~ 띠잉" (줄이 쳐지는 소리 + 줄이 팽팽하게 떨림)
                b = Buf(0.5f);
                Add(b, 0f, 0.14f, 0f, 0f, Wave.Noise, 0.28f, lowpass: 5000f, attack: 0.01f, decay: 2f);
                Add(b, 0.02f, 0.12f, 2600f, 900f, Wave.Sine, 0.12f, decay: 2f);
                Add(b, 0.1f, 0.4f, 330f, 320f, Wave.Sine, 0.3f, amHz: 18f, amDepth: 0.3f, decay: 2.5f);
                Add(b, 0.1f, 0.4f, 660f, 640f, Wave.Sine, 0.1f, amHz: 18f, amDepth: 0.3f, decay: 3f);
                break;

            case Sfx.LevelUp: // 귀뚜라미 세 번, 점점 높게
                b = Buf(0.45f);
                Chirp(b, 0f, 0.08f, 3600f, 0.2f);
                Chirp(b, 0.13f, 0.08f, 4100f, 0.2f);
                Chirp(b, 0.26f, 0.14f, 4700f, 0.22f);
                break;

            case Sfx.Die: // "푸슉" 찌그러지는 소리 + 날개가 멎는 붕~
                b = Buf(0.6f);
                Add(b, 0f, 0.35f, 300f, 55f, Wave.Sine, 0.5f, decay: 2.5f);
                Add(b, 0f, 0.25f, 0f, 0f, Wave.Noise, 0.3f, lowpass: 900f, decay: 3f);
                Add(b, 0.05f, 0.5f, 180f, 50f, Wave.Saw, 0.14f, amHz: 30f, amDepth: 0.8f, lowpass: 900f, decay: 1.5f);
                break;

            case Sfx.NewBest: // 귀뚜라미 합창 + 이슬방울
                b = Buf(0.75f);
                Add(b, 0f, 0.6f, 4200f, 4200f, Wave.Sine, 0.13f, amHz: 34f, amDepth: 1f, attack: 0.05f, decay: 1.2f);
                Add(b, 0.05f, 0.6f, 4800f, 4800f, Wave.Sine, 0.08f, amHz: 29f, amDepth: 1f, attack: 0.1f, decay: 1.2f);
                Add(b, 0f, 0.07f, 700f, 1500f, Wave.Sine, 0.32f, decay: 3f);
                Add(b, 0.12f, 0.07f, 900f, 1900f, Wave.Sine, 0.32f, decay: 3f);
                Add(b, 0.24f, 0.12f, 1100f, 2400f, Wave.Sine, 0.32f, decay: 3f);
                break;

            case Sfx.Roll: // 콩벌레 "또르르르" 굴러가는 소리
                b = Buf(0.4f);
                Add(b, 0f, 0.38f, 0f, 0f, Wave.Noise, 0.35f, amHz: 22f, amDepth: 0.9f, lowpass: 600f, attack: 0.02f, decay: 1.5f);
                Add(b, 0f, 0.38f, 110f, 80f, Wave.Sine, 0.3f, amHz: 22f, amDepth: 0.7f, decay: 1.5f);
                break;

            case Sfx.Worm: // 지렁이 "꾸물꾸물" (흙 속에서 부스럭 + 낮게 꿀렁)
                b = Buf(0.55f);
                Add(b, 0f, 0.5f, 0f, 0f, Wave.Noise, 0.3f, amHz: 9f, amDepth: 0.85f, lowpass: 900f, attack: 0.05f, decay: 1.2f);
                Add(b, 0f, 0.5f, 140f, 95f, Wave.Sine, 0.25f, amHz: 9f, amDepth: 0.7f, attack: 0.05f, decay: 1.5f);
                break;

            default:
                b = Buf(0.01f);
                break;
        }
        var clip = AudioClip.Create(s.ToString(), b.Length, 1, Rate, false);
        clip.SetData(b, 0);
        return clip;
    }

    static float[] Buf(float seconds)
    {
        return new float[Mathf.CeilToInt(seconds * Rate)];
    }

    // 귀뚜라미 울음 : 높은 음을 아주 빠르게 끊어서 "찌르르"
    static void Chirp(float[] b, float start, float dur, float freq, float vol)
    {
        Add(b, start, dur, freq, freq * 1.03f, Wave.Sine, vol, amHz: 55f, amDepth: 1f, attack: 0.01f, decay: 1.2f);
    }

    // 소리 하나를 버퍼에 겹쳐 더함
    //  f0 -> f1 : 주파수가 미끄러짐 / amHz, amDepth : 떨림(벌레 날개, 울음) / lowpass : 높은 소리 깎기(부드럽게)
    //  attack : 커지는 시간 / decay : 클수록 빨리 작아짐
    static void Add(float[] b, float start, float dur, float f0, float f1, Wave wave, float vol,
        float amHz = 0f, float amDepth = 0f, float lowpass = 0f, float attack = 0.004f, float decay = 2f)
    {
        int s0 = Mathf.RoundToInt(start * Rate);
        int n = Mathf.RoundToInt(dur * Rate);
        float phase = 0f, lp = 0f;
        float lpA = lowpass > 0f ? 1f - Mathf.Exp(-2f * Mathf.PI * lowpass / Rate) : 1f;
        for (int i = 0; i < n && s0 + i < b.Length; i++)
        {
            float t = i / (float)Rate;
            float k = i / (float)n;
            float f = f0 > 0f ? f0 * Mathf.Pow(f1 / f0, k) : 0f;
            phase += f / Rate;
            phase -= Mathf.Floor(phase);

            float x;
            switch (wave)
            {
                case Wave.Saw:   x = phase * 2f - 1f; break;
                case Wave.Noise: x = Random.value * 2f - 1f; break;
                default:         x = Mathf.Sin(phase * Mathf.PI * 2f); break;
            }
            lp += (x - lp) * lpA; // 한 단계 저역 통과 필터
            x = lp;

            if (amDepth > 0f)
                x *= 1f - amDepth * (0.5f + 0.5f * Mathf.Sin(t * amHz * Mathf.PI * 2f));

            float env = Mathf.Min(1f, t / Mathf.Max(attack, 0.001f)) * Mathf.Pow(1f - k, decay);
            b[s0 + i] += x * env * vol;
        }
    }
}
