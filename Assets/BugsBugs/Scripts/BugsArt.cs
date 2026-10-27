using UnityEngine;

// 게임 그림을 코드로 그리는 곳 (그림 파일 없이도 동작).
// 모양을 "거리 함수"로 표현하고 바깥 -> 안쪽 순서로 겹쳐 칠함. 테두리가 부드럽게(안티에일리어싱) 나옴.
//   - Ladybug : 주인공 무당벌레 (위에서 본 모습, 위쪽이 머리)
//   - Beetle  : 적 벌레 (위에서 본 모습, 머리가 아래)
//   - Ground  : 위아래로 이어지는 숲 바닥 배경
public static class BugsArt
{
    public enum BeetleStyle { Plain, Zigzag, Wings, Horns }

    public delegate Color PixelFunc(int x, int y, int w, int h);

    public static Sprite Bake(int w, int h, PixelFunc f, float pixelsPerUnit)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = f(x, y, w, h);
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
    }

    // ================================================================= 다리 걸음 애니메이션
    // phase : -1 ~ 1. 그림을 여러 장(다리 위치만 다르게) 구워서 번갈아 보여주면 걷는 것처럼 보임
    public static readonly float[] WalkPhases = { 0f, 1f, -1f }; // 구울 그림 3장
    public static readonly int[] WalkCycle = { 0, 1, 0, 2 };     // 보여주는 순서 (가운데 -> 앞 -> 가운데 -> 뒤)
    const float LegSwing = 16f; // 다리가 흔들리는 각도 (도)

    // 곤충 걸음 : 한쪽 앞·뒷다리와 반대쪽 가운데 다리가 한 묶음 (+1 / -1)
    static float Tripod(int side, int k) { return ((k % 2 == 0) == (side > 0)) ? 1f : -1f; }

    // pivot 을 중심으로 pt 를 deg 도 회전
    static Vector2 Swing(Vector2 pivot, Vector2 pt, float deg)
    {
        float r = deg * Mathf.Deg2Rad, cs = Mathf.Cos(r), sn = Mathf.Sin(r);
        Vector2 d = pt - pivot;
        return pivot + new Vector2(d.x * cs - d.y * sn, d.x * sn + d.y * cs);
    }

    // 걸음 그림 여러 장 굽기
    public delegate Color PhasePixelFunc(int x, int y, int w, int h, float phase);
    public static Sprite[] BakeWalk(int size, PhasePixelFunc f)
    {
        var frames = new Sprite[WalkPhases.Length];
        for (int i = 0; i < frames.Length; i++)
        {
            float ph = WalkPhases[i];
            frames[i] = Bake(size, size, (x, y, w, h) => f(x, y, w, h, ph), size);
        }
        return frames;
    }

    // ================================================================= 기본 도형 (거리 함수, 음수 = 안쪽, 단위 = 픽셀)
    static float Circle(Vector2 p, Vector2 c, float r) { return (p - c).magnitude - r; }

    static float Ellipse(Vector2 p, Vector2 c, float rx, float ry)
    {
        var q = new Vector2((p.x - c.x) / rx, (p.y - c.y) / ry);
        return (q.magnitude - 1f) * Mathf.Min(rx, ry);
    }

    static float Capsule(Vector2 p, Vector2 a, Vector2 b, float r)
    {
        Vector2 pa = p - a, ba = b - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
        return (pa - ba * h).magnitude - r;
    }

    // 도형을 덮어 칠하기 (가장자리는 부드럽게)
    static Color Paint(Color dst, Color src, float sdf)
    {
        float cov = Mathf.Clamp01(0.5f - sdf);
        if (cov <= 0f) return dst;
        src.a *= cov;
        float a = src.a + dst.a * (1f - src.a);
        if (a <= 0f) return new Color(0, 0, 0, 0);
        Color o = (src * src.a + dst * dst.a * (1f - src.a)) / a;
        o.a = a;
        return o;
    }

    static Color Hex(string hex)
    {
        Color c;
        ColorUtility.TryParseHtmlString(hex, out c);
        return c;
    }

    static Color Shade(Color c, float k) { return new Color(c.r * k, c.g * k, c.b * k, c.a); }

    static readonly Color Clear = new Color(0, 0, 0, 0);

    // 반짝이는 눈 (흰자 + 테두리 + 눈동자 + 하이라이트)
    static Color Eye(Color col, Vector2 p, Vector2 c, float rx, float ry, Vector2 look, Color line, float s)
    {
        col = Paint(col, line, Ellipse(p, c, rx + 0.014f * s, ry + 0.014f * s));
        col = Paint(col, Color.white, Ellipse(p, c, rx, ry));
        float pr = Mathf.Min(rx, ry) * 0.55f;
        Vector2 pc = c + look * Mathf.Min(rx, ry) * 0.35f;
        col = Paint(col, Hex("#2A2430"), Circle(p, pc, pr));
        col = Paint(col, Color.white, Circle(p, pc + new Vector2(-0.3f, 0.35f) * pr, pr * 0.32f));
        return col;
    }

    // ================================================================= 주인공 : 노란 무당벌레
    public static Color Ladybug(int x, int y, int w, int h, float phase)
    {
        float s = w;
        float c = (s - 1) * 0.5f;
        Vector2 p = new Vector2(x, y);
        Vector2 P(float u, float v) => new Vector2(c + u * s, c + v * s);

        Color line = Hex("#3B2A22");
        Color body = Hex("#FFD54A");
        Color dark = Hex("#2E2A33");
        Color col = Clear;

        // 다리 6개 (걸음 : 왼앞·오른가운데·왼뒤 / 오른앞·왼가운데·오른뒤 두 묶음이 번갈아 앞뒤로)
        for (int side = -1; side <= 1; side += 2)
            for (int k = 0; k < 3; k++)
            {
                Vector2 a = P(side * 0.26f, 0.1f - 0.17f * k);
                Vector2 e = P(side * 0.44f, 0.15f - 0.21f * k);
                e = Swing(a, e, phase * LegSwing * Tripod(side, k) * side); // 머리(위쪽)가 앞
                col = Paint(col, dark, Capsule(p, a, e, 0.025f * s));
            }

        // 머리
        col = Paint(col, dark, Circle(p, P(0f, 0.25f), 0.2f * s));

        // 등껍질 (테두리 -> 안쪽, 아래로 갈수록 살짝 어둡게)
        Vector2 sc = P(0f, -0.06f);
        float rx = 0.36f * s, ry = 0.37f * s;
        col = Paint(col, line, Ellipse(p, sc, rx + 0.03f * s, ry + 0.03f * s));
        float shell = Ellipse(p, sc, rx, ry);
        float shade = Mathf.Lerp(1.05f, 0.85f, Mathf.Clamp01((sc.y + ry - y) / (2f * ry)));
        col = Paint(col, Shade(body, shade), shell);

        // 가운데 날개 선, 점무늬 (등껍질 밖으로 안 나가게 Max 로 자름)
        col = Paint(col, line, Mathf.Max(Capsule(p, P(0f, 0.32f), P(0f, -0.44f), 0.013f * s), shell));
        Vector2[] spots = { P(-0.17f, 0.1f), P(0.17f, 0.1f), P(-0.2f, -0.14f), P(0.2f, -0.14f), P(-0.09f, -0.32f), P(0.09f, -0.32f) };
        float[] spotR = { 0.065f, 0.065f, 0.075f, 0.075f, 0.05f, 0.05f };
        for (int i = 0; i < spots.Length; i++)
            col = Paint(col, dark, Mathf.Max(Circle(p, spots[i], spotR[i] * s), shell));

        // 하이라이트
        col = Paint(col, new Color(1f, 1f, 1f, 0.55f), Mathf.Max(Ellipse(p, P(-0.15f, 0.13f), 0.08f * s, 0.05f * s), shell));

        // 눈 (머리 위로 빼꼼)
        col = Eye(col, p, P(-0.09f, 0.36f), 0.065f * s, 0.07f * s, new Vector2(0f, 1f), dark, s);
        col = Eye(col, p, P(0.09f, 0.36f), 0.065f * s, 0.07f * s, new Vector2(0f, 1f), dark, s);
        return col;
    }

    // ================================================================= 적 : 위에서 본 벌레 (머리가 아래 = 떨어지는 방향)
    //  다리 6개 + 더듬이 + 머리 + 가슴 + 등껍질. 종류마다 무늬/날개/큰 턱이 다름
    public static Color Beetle(int x, int y, int w, int h, Color fill, Color line, BeetleStyle style, float phase)
    {
        float s = w;
        float c = (s - 1) * 0.5f;
        Vector2 p = new Vector2(x, y);
        Vector2 P(float u, float v) => new Vector2(c + u * s, c + v * s);
        Color col = Clear;
        Color dark = Shade(line, 0.8f);
        dark.a = 1f;

        // 다리 6개 (앞다리는 앞으로, 뒷다리는 뒤로, 무릎에서 꺾임). 걸음은 세 다리씩 번갈아 움직임
        float legR = 0.02f * s;
        float[,] bl =
        {
            // 시작 x, y / 무릎 x, y / 끝 x, y
            { 0.14f, -0.12f, 0.29f, -0.19f, 0.32f, -0.33f },
            { 0.18f,  0.02f, 0.35f,  0.03f, 0.42f, -0.06f },
            { 0.16f,  0.18f, 0.31f,  0.27f, 0.36f,  0.41f },
        };
        for (int side = -1; side <= 1; side += 2)
            for (int k = 0; k < 3; k++)
            {
                Vector2 a = P(side * bl[k, 0], bl[k, 1]);
                float ang = -phase * LegSwing * Tripod(side, k) * side; // 머리(아래쪽)가 앞
                Vector2 b = Swing(a, P(side * bl[k, 2], bl[k, 3]), ang);
                Vector2 e = Swing(a, P(side * bl[k, 4], bl[k, 5]), ang);
                col = Paint(col, dark, Capsule(p, a, b, legR));
                col = Paint(col, dark, Capsule(p, b, e, legR));
            }

        // 더듬이 또는 큰 턱(사슴벌레)
        for (int side = -1; side <= 1; side += 2)
        {
            if (style == BeetleStyle.Horns)
            {
                col = Paint(col, dark, Capsule(p, P(side * 0.05f, -0.36f), P(side * 0.14f, -0.46f), 0.032f * s));
                col = Paint(col, dark, Capsule(p, P(side * 0.14f, -0.46f), P(side * 0.06f, -0.495f), 0.026f * s));
            }
            else
            {
                col = Paint(col, dark, Capsule(p, P(side * 0.035f, -0.38f), P(side * 0.09f, -0.45f), 0.013f * s));
                col = Paint(col, dark, Capsule(p, P(side * 0.09f, -0.45f), P(side * 0.17f, -0.47f), 0.013f * s));
            }
        }

        // 머리
        col = Paint(col, line, Ellipse(p, P(0f, -0.33f), 0.1f * s + 0.02f * s, 0.08f * s + 0.02f * s));
        col = Paint(col, dark, Ellipse(p, P(0f, -0.33f), 0.1f * s, 0.08f * s));

        // 가슴
        col = Paint(col, line, Ellipse(p, P(0f, -0.2f), 0.165f * s + 0.022f * s, 0.085f * s + 0.022f * s));
        col = Paint(col, Shade(fill, 0.78f), Ellipse(p, P(0f, -0.2f), 0.165f * s, 0.085f * s));

        // 등껍질 (테두리 -> 안쪽, 꼬리 쪽으로 갈수록 어둡게)
        Vector2 sc = P(0f, 0.09f);
        float rx = 0.25f * s, ry = 0.3f * s;
        col = Paint(col, line, Ellipse(p, sc, rx + 0.025f * s, ry + 0.025f * s));
        float shell = Ellipse(p, sc, rx, ry);
        float shade = Mathf.Lerp(1.08f, 0.84f, Mathf.Clamp01((y - (sc.y - ry)) / (2f * ry)));
        col = Paint(col, Shade(fill, shade), shell);

        if (style == BeetleStyle.Wings)
        {
            // 날벌레 : 배에 줄무늬 + 반투명 날개 두 장
            for (int k = 0; k < 3; k++)
                col = Paint(col, Shade(fill, 0.62f), Mathf.Max(Mathf.Abs(y - (sc.y + (0.02f + 0.12f * k) * s)) - 0.02f * s, shell));
            for (int side = -1; side <= 1; side += 2)
            {
                float wing = Capsule(p, P(side * 0.06f, -0.08f), P(side * 0.27f, 0.2f), 0.09f * s);
                col = Paint(col, new Color(line.r, line.g, line.b, 0.55f), wing - 0.012f * s);
                col = Paint(col, new Color(0.95f, 0.97f, 1f, 0.6f), wing);
            }
        }
        else
        {
            // 딱정벌레 : 가운데 날개 선
            col = Paint(col, line, Mathf.Max(Capsule(p, P(0f, -0.2f), P(0f, 0.4f), 0.012f * s), shell));
            if (style == BeetleStyle.Zigzag)
            {
                // 노린재처럼 등에 지그재그 줄무늬
                for (int band = 0; band < 2; band++)
                {
                    float baseV = band == 0 ? 0.0f : 0.2f;
                    float tri = Mathf.Abs(Mathf.Repeat((x - c) / (0.08f * s), 2f) - 1f);
                    float lineY = c + (baseV + (tri - 0.5f) * 0.06f) * s;
                    col = Paint(col, Shade(fill, 0.62f), Mathf.Max(Mathf.Abs(y - lineY) - 0.02f * s, shell));
                }
            }
            // 반짝임
            col = Paint(col, new Color(1f, 1f, 1f, 0.45f), Mathf.Max(Ellipse(p, P(-0.11f, 0.2f), 0.05f * s, 0.1f * s), shell));
        }

        // 작은 눈 (아래쪽을 노려봄)
        for (int side = -1; side <= 1; side += 2)
        {
            Vector2 e = P(side * 0.055f, -0.355f);
            col = Paint(col, Color.white, Circle(p, e, 0.03f * s));
            col = Paint(col, Hex("#1E1A22"), Circle(p, e + new Vector2(0f, -0.01f * s), 0.017f * s));
        }
        return col;
    }

    // ================================================================= UI 그림 (숲 느낌)
    public static Sprite BakeSliced(int w, int h, PixelFunc f, Vector4 border)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = f(x, y, w, h);
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }

    static float RoundBox(Vector2 p, Vector2 c, Vector2 half, float r)
    {
        Vector2 q = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half + new Vector2(r, r);
        return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
    }

    // 삼각형 (거리 함수)
    static float Tri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        Vector2 e0 = b - a, e1 = c - b, e2 = a - c;
        Vector2 v0 = p - a, v1 = p - b, v2 = p - c;
        Vector2 pq0 = v0 - e0 * Mathf.Clamp01(Vector2.Dot(v0, e0) / Vector2.Dot(e0, e0));
        Vector2 pq1 = v1 - e1 * Mathf.Clamp01(Vector2.Dot(v1, e1) / Vector2.Dot(e1, e1));
        Vector2 pq2 = v2 - e2 * Mathf.Clamp01(Vector2.Dot(v2, e2) / Vector2.Dot(e2, e2));
        float s = Mathf.Sign(e0.x * e2.y - e0.y * e2.x);
        float dx = Mathf.Min(Mathf.Min(pq0.sqrMagnitude, pq1.sqrMagnitude), pq2.sqrMagnitude);
        float dy = Mathf.Min(Mathf.Min(s * (v0.x * e0.y - v0.y * e0.x), s * (v1.x * e1.y - v1.y * e1.x)), s * (v2.x * e2.y - v2.y * e2.x));
        return -Mathf.Sqrt(dx) * Mathf.Sign(dy);
    }

    // 원호 (c 중심, 반지름 r, 굵기 t, 각도 a0 ~ a1 도, 끝은 둥글게)
    static float Arc(Vector2 p, Vector2 c, float r, float t, float a0, float a1)
    {
        Vector2 d = p - c;
        float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        float mid = (a0 + a1) * 0.5f, halfSpan = (a1 - a0) * 0.5f;
        if (Mathf.Abs(Mathf.DeltaAngle(mid, ang)) <= halfSpan) return Mathf.Abs(d.magnitude - r) - t;
        Vector2 e0 = c + new Vector2(Mathf.Cos(a0 * Mathf.Deg2Rad), Mathf.Sin(a0 * Mathf.Deg2Rad)) * r;
        Vector2 e1 = c + new Vector2(Mathf.Cos(a1 * Mathf.Deg2Rad), Mathf.Sin(a1 * Mathf.Deg2Rad)) * r;
        return Mathf.Min((p - e0).magnitude, (p - e1).magnitude) - t;
    }

    // 도톰한 버튼 (흰색으로 그려서 Image.color 로 물들임). 테두리 + 아래쪽 그림자 턱 + 위쪽 하이라이트
    //  9-slice 용 : 가장자리 30px 은 늘어나지 않음
    public static Color ChunkyButton(int x, int y, int w, int h)
    {
        Vector2 p = new Vector2(x, y);
        Vector2 c = new Vector2((w - 1) * 0.5f, (h - 1) * 0.5f);
        Vector2 half = new Vector2(w * 0.5f - 1f, h * 0.5f - 1f);
        Color col = Clear;
        col = Paint(col, new Color(0.32f, 0.32f, 0.32f, 1f), RoundBox(p, c, half, 26f));                        // 테두리
        col = Paint(col, new Color(0.72f, 0.72f, 0.72f, 1f), RoundBox(p, c, half - new Vector2(5f, 5f), 21f));   // 아래 턱 (어두운 면)
        col = Paint(col, Color.white, RoundBox(p, c + new Vector2(0f, 5f), half - new Vector2(5f, 10f), 21f));   // 윗면
        col = Paint(col, new Color(1f, 1f, 1f, 0.55f), Capsule(p, new Vector2(22f, h - 16f), new Vector2(w - 23f, h - 16f), 3.5f)); // 반짝
        return col;
    }

    // 도톰한 원형 버튼 (ChunkyButton 의 동그라미 버전, 흰색으로 그려서 물들임)
    public static Color ChunkyCircle(int x, int y, int w, int h)
    {
        Vector2 p = new Vector2(x, y);
        float c = (w - 1) * 0.5f, r = w * 0.5f - 1f;
        Color col = Clear;
        col = Paint(col, new Color(0.32f, 0.32f, 0.32f, 1f), Circle(p, new Vector2(c, c), r));
        col = Paint(col, new Color(0.72f, 0.72f, 0.72f, 1f), Circle(p, new Vector2(c, c), r - 5f));
        col = Paint(col, Color.white, Circle(p, new Vector2(c, c + 5f), r - 10f));
        col = Paint(col, new Color(1f, 1f, 1f, 0.55f), Arc(p, new Vector2(c, c + 5f), r - 19f, 3f, 105f, 165f));
        return col;
    }

    // 게이지 링 (흰색 고리, Image Filled 로 채운 만큼만 보이게)
    public static Color Ring(int x, int y, int w, int h)
    {
        Vector2 p = new Vector2(x, y);
        float c = (w - 1) * 0.5f;
        float r = w * 0.5f - 7f;
        return Paint(Clear, Color.white, Mathf.Abs((p - new Vector2(c, c)).magnitude - r) - 5f);
    }

    // 결과 화면 카드 : 커다란 나뭇잎 (가운데 잎맥 + 옆 잎맥, 위는 뾰족, 아래에 잎자루)
    public static Color Leaf(int x, int y, int w, int h)
    {
        Vector2 p = new Vector2(x, y);
        float cx = (w - 1) * 0.5f;
        float bodyTop = h * 0.9f, bodyBot = h * 0.07f;
        float cy = (bodyTop + bodyBot) * 0.5f;
        float rx = w * 0.47f, ry = (bodyTop - bodyBot) * 0.5f;
        // 둥근 사각형에 가까운 잎 몸통 (초타원)
        float u = Mathf.Abs(x - cx) / rx, v = Mathf.Abs(y - cy) / ry;
        float body = (Mathf.Pow(Mathf.Pow(u, 3f) + Mathf.Pow(v, 3f), 1f / 3f) - 1f) * Mathf.Min(rx, ry);
        // 위쪽 뾰족한 잎끝
        float tip = Tri(p, new Vector2(cx - w * 0.2f, bodyTop - h * 0.06f), new Vector2(cx + w * 0.2f, bodyTop - h * 0.06f), new Vector2(cx, h - 3f));
        float leaf = Mathf.Min(body, tip - 6f) ;
        Color col = Clear;
        // 잎자루
        col = Paint(col, Hex("#6B4A2B"), Capsule(p, new Vector2(cx, 3f), new Vector2(cx, bodyBot + 20f), 6f));
        col = Paint(col, Hex("#2F5E22"), leaf - 6f);
        float k = Mathf.Clamp01((y - bodyBot) / (bodyTop - bodyBot));
        col = Paint(col, Color.Lerp(Hex("#6FAF45"), Hex("#9CD064"), k), leaf);
        // 잎맥 (잎 안에서만)
        Color vein = new Color(0.86f, 0.95f, 0.7f, 0.55f);
        col = Paint(col, vein, Mathf.Max(Capsule(p, new Vector2(cx, bodyBot), new Vector2(cx, h - 20f), 2.2f), leaf + 4f));
        for (int i = 0; i < 6; i++)
        {
            float vy = bodyBot + (bodyTop - bodyBot) * (0.1f + 0.15f * i);
            for (int side = -1; side <= 1; side += 2)
                col = Paint(col, new Color(vein.r, vein.g, vein.b, 0.35f),
                    Mathf.Max(Capsule(p, new Vector2(cx, vy), new Vector2(cx + side * rx * 0.85f, vy + h * 0.09f), 1.5f), leaf + 6f));
        }
        // 가장자리 안쪽 살짝 어둡게 (입체감)
        col = Paint(col, new Color(0.18f, 0.35f, 0.12f, 0.18f), Mathf.Max(leaf, -(leaf + 10f)));
        return col;
    }

    // 방패 아이콘 (색 있음) : 위는 평평, 아래로 뾰족. 흰 테두리 + 가운데 줄 + 반짝
    public static Color Shield(int x, int y, int w, int h)
    {
        float s = w;
        float c = (s - 1) * 0.5f;
        Vector2 p = new Vector2(x, y);
        // 두 원이 겹친 렌즈 모양 아래쪽 + 위를 잘라 평평하게
        float R = s * 0.62f;
        float lens = Mathf.Max(Circle(p, new Vector2(c - s * 0.28f, c + s * 0.12f), R), Circle(p, new Vector2(c + s * 0.28f, c + s * 0.12f), R));
        float top = y - (c + s * 0.36f);
        float shield = Mathf.Max(lens, top);
        Color col = Clear;
        col = Paint(col, Hex("#2B3F66"), shield - 4f);
        col = Paint(col, Color.white, shield - 1f);
        float inner = shield + 4f;
        float k = Mathf.Clamp01((y - c + s * 0.4f) / (s * 0.8f));
        col = Paint(col, Color.Lerp(Hex("#3E7BD6"), Hex("#7DB4F5"), k), inner);
        // 가운데 세로 줄 + 가로 줄 (십자)
        col = Paint(col, new Color(1f, 1f, 1f, 0.85f), Mathf.Max(RoundBox(p, new Vector2(c, c), new Vector2(s * 0.045f, s * 0.4f), 2f), inner + 4f));
        col = Paint(col, new Color(1f, 1f, 1f, 0.85f), Mathf.Max(RoundBox(p, new Vector2(c, c + s * 0.12f), new Vector2(s * 0.3f, s * 0.045f), 2f), inner + 4f));
        // 반짝
        col = Paint(col, new Color(1f, 1f, 1f, 0.45f), Mathf.Max(Ellipse(p, new Vector2(c - s * 0.17f, c + s * 0.24f), s * 0.05f, s * 0.07f), inner));
        return col;
    }

    // 아이콘 (흰색, Image.color 로 물들임)
    public static Color IconHome(int x, int y, int w, int h)
    {
        float s = w; Vector2 p = new Vector2(x, y);
        Vector2 P(float u, float v) => new Vector2(u * s, v * s);
        float roof = Tri(p, P(0.12f, 0.52f), P(0.88f, 0.52f), P(0.5f, 0.88f)) - 0.035f * s;
        float body = RoundBox(p, P(0.5f, 0.36f), new Vector2(0.27f * s, 0.2f * s), 0.03f * s);
        float door = RoundBox(p, P(0.5f, 0.27f), new Vector2(0.07f * s, 0.11f * s), 0.02f * s);
        return Paint(Clear, Color.white, Mathf.Max(Mathf.Min(roof, body), -door));
    }

    public static Color IconRetry(int x, int y, int w, int h)
    {
        float s = w; Vector2 p = new Vector2(x, y);
        Vector2 c = new Vector2(0.5f * s, 0.48f * s);
        float ring = Arc(p, c, 0.28f * s, 0.06f * s, 110f, 400f);
        // 화살촉 (원호 끝 400도 = 40도 위치에서 시계 반대 방향을 가리킴)
        Vector2 tipBase = c + new Vector2(Mathf.Cos(40f * Mathf.Deg2Rad), Mathf.Sin(40f * Mathf.Deg2Rad)) * 0.28f * s;
        float head = Tri(p, tipBase + new Vector2(-0.16f, 0.02f) * s, tipBase + new Vector2(0.1f, -0.13f) * s, tipBase + new Vector2(0.12f, 0.16f) * s);
        return Paint(Clear, Color.white, Mathf.Min(ring, head));
    }

    public static Color IconPause(int x, int y, int w, int h)
    {
        float s = w; Vector2 p = new Vector2(x, y);
        float a = RoundBox(p, new Vector2(0.35f * s, 0.5f * s), new Vector2(0.08f * s, 0.26f * s), 0.04f * s);
        float b = RoundBox(p, new Vector2(0.65f * s, 0.5f * s), new Vector2(0.08f * s, 0.26f * s), 0.04f * s);
        return Paint(Clear, Color.white, Mathf.Min(a, b));
    }

    public static Color IconPlay(int x, int y, int w, int h)
    {
        float s = w; Vector2 p = new Vector2(x, y);
        float t = Tri(p, new Vector2(0.32f * s, 0.2f * s), new Vector2(0.32f * s, 0.8f * s), new Vector2(0.8f * s, 0.5f * s)) - 0.04f * s;
        return Paint(Clear, Color.white, t);
    }

    public static Color IconSound(int x, int y, int w, int h, bool on)
    {
        float s = w; Vector2 p = new Vector2(x, y);
        Vector2 P(float u, float v) => new Vector2(u * s, v * s);
        float box = RoundBox(p, P(0.24f, 0.5f), new Vector2(0.09f * s, 0.12f * s), 0.03f * s);
        float cone = Tri(p, P(0.22f, 0.5f), P(0.52f, 0.82f), P(0.52f, 0.18f)) - 0.02f * s;
        float d = Mathf.Min(box, cone);
        if (on)
        {
            d = Mathf.Min(d, Arc(p, P(0.5f, 0.5f), 0.16f * s, 0.035f * s, -45f, 45f));
            d = Mathf.Min(d, Arc(p, P(0.5f, 0.5f), 0.3f * s, 0.035f * s, -50f, 50f));
        }
        else
        {
            d = Mathf.Min(d, Capsule(p, P(0.64f, 0.38f), P(0.88f, 0.62f), 0.045f * s));
            d = Mathf.Min(d, Capsule(p, P(0.64f, 0.62f), P(0.88f, 0.38f), 0.045f * s));
        }
        return Paint(Clear, Color.white, d);
    }

    // ================================================================= 꽃잎 그림 (스킬)
    // 꽃잎 : 끝이 살짝 뾰족한 물방울 모양, 아래(밑동)는 연하고 끝으로 갈수록 진한 색 + 잎맥 + 하이라이트
    //  스프라이트의 위쪽이 꽃잎 끝 (날아가는 방향)
    public static Color Petal(int x, int y, int w, int h, Color baseCol, Color tipCol, Color edge)
    {
        float s = w;
        float c = (s - 1) * 0.5f;
        Vector2 p = new Vector2(x, y);
        float k = Mathf.Clamp01((y - c * 0.08f) / (s * 0.92f)); // 0 = 밑동, 1 = 끝
        // 위로 갈수록 좁아지는 타원 = 물방울 모양
        float rx = s * 0.3f * Mathf.Lerp(0.75f, 1.05f, Mathf.Sin(Mathf.Clamp01(k * 1.15f) * Mathf.PI * 0.8f));
        float d = Ellipse(p, new Vector2(c, c), rx, s * 0.46f);
        Color col = Clear;
        col = Paint(col, edge, d);
        Color fill = Color.Lerp(baseCol, tipCol, Mathf.SmoothStep(0f, 1f, k));
        col = Paint(col, fill, d + 0.035f * s);
        // 잎맥
        col = Paint(col, new Color(edge.r, edge.g, edge.b, 0.35f),
            Mathf.Max(Capsule(p, new Vector2(c, c - s * 0.36f), new Vector2(c, c + s * 0.2f), 0.012f * s), d + 0.035f * s));
        // 하이라이트
        col = Paint(col, new Color(1f, 1f, 1f, 0.5f),
            Mathf.Max(Ellipse(p, new Vector2(c - s * 0.1f, c + s * 0.08f), s * 0.06f, s * 0.16f), d + 0.035f * s));
        return col;
    }

    // ================================================================= 지렁이 (가로 돌진) : 머리가 아래, 몸이 물결침
    //  phase (0 ~ 2π) 를 바꿔 여러 장 구우면 꿈틀거림. 마디 줄무늬 + 두꺼운 띠(환대) + 작은 눈
    public static readonly float[] WigglePhases = { 0f, Mathf.PI * 0.5f, Mathf.PI, Mathf.PI * 1.5f };

    public static Sprite[] BakeWiggle(int size, PhasePixelFunc f)
    {
        var frames = new Sprite[WigglePhases.Length];
        for (int i = 0; i < frames.Length; i++)
        {
            float ph = WigglePhases[i];
            frames[i] = Bake(size, size, (x, y, w, h) => f(x, y, w, h, ph), size);
        }
        return frames;
    }

    public static Color Worm(int x, int y, int w, int h, float phase)
    {
        float s = w;
        float c = (s - 1) * 0.5f;
        Vector2 p = new Vector2(x, y);
        const int N = 20;
        // 몸 중심선 : u = 0 (머리, 아래) ~ 1 (꼬리, 위)
        Vector2 Pt(float u) => new Vector2(c + Mathf.Sin(u * Mathf.PI * 2.4f - phase) * s * 0.1f * Mathf.Lerp(0.5f, 1f, u), s * (0.1f + 0.8f * u));
        float Rad(float u) => s * Mathf.Lerp(0.085f, 0.05f, u * u);

        // 가장 가까운 몸 마디 찾기
        float best = float.MaxValue, bestU = 0f;
        Vector2 prev = Pt(0f);
        for (int i = 1; i <= N; i++)
        {
            float u1 = i / (float)N, u0 = (i - 1) / (float)N;
            Vector2 cur = Pt(u1);
            Vector2 pa = p - prev, ba = cur - prev;
            float t = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            float u = Mathf.Lerp(u0, u1, t);
            float d = (pa - ba * t).magnitude - Rad(u);
            if (d < best) { best = d; bestU = u; }
            prev = cur;
        }

        Color line = Hex("#8E4452");
        Color col = Paint(Clear, line, best - 3.5f);
        // 몸 색 : 기본 분홍, 앞쪽 1/3 지점에 두꺼운 띠(환대), 마디마다 살짝 진한 줄
        Color body = Hex("#F4A7AE");
        if (Mathf.Abs(bestU - 0.3f) < 0.06f) body = Hex("#E4808C");
        else if (Mathf.Repeat(bestU, 0.065f) < 0.012f) body = Shade(body, 0.86f);
        col = Paint(col, body, best);
        // 몸 한쪽 반짝 (윤기)
        col = Paint(col, new Color(1f, 1f, 1f, 0.35f), Mathf.Max(best + Rad(bestU) * 0.45f, -(best + Rad(bestU) * 0.85f)) + (p.x > Pt(bestU).x ? 99f : 0f));

        // 머리의 작은 눈
        Vector2 head = Pt(0.03f);
        for (int side = -1; side <= 1; side += 2)
        {
            Vector2 e = head + new Vector2(side * s * 0.035f, -s * 0.01f);
            col = Paint(col, Color.white, Circle(p, e, s * 0.022f));
            col = Paint(col, Hex("#1E1A22"), Circle(p, e + new Vector2(0f, -s * 0.006f), s * 0.012f));
        }
        return col;
    }

    // ================================================================= 거미 (돌진 벌레) : 위에서 본 모습, 머리가 아래
    //  다리 8개 + 머리가슴 + 둥근 배(무늬) + 큰 눈 2개 + 작은 눈 2개 + 송곳니
    public static Color Spider(int x, int y, int w, int h, float phase)
    {
        float s = w;
        float c = (s - 1) * 0.5f;
        Vector2 p = new Vector2(x, y);
        Vector2 P(float u, float v) => new Vector2(c + u * s, c + v * s);
        Color body = Hex("#4B3F5C"), line = Hex("#1E1A26"), mark = Hex("#CDB8EE");
        Color col = Clear;

        // 다리 8개 (무릎이 바깥으로 솟은 거미 다리)
        float legR = 0.022f * s;
        float[,] legs =
        {
            // 시작 x, y / 무릎 x, y / 끝 x, y
            { 0.08f, -0.2f,  0.22f, -0.31f, 0.25f, -0.47f },
            { 0.1f, -0.16f,  0.33f, -0.2f,  0.44f, -0.31f },
            { 0.1f, -0.12f,  0.34f, -0.02f, 0.46f,  0.1f  },
            { 0.08f, -0.08f, 0.27f,  0.12f, 0.34f,  0.33f },
        };
        for (int side = -1; side <= 1; side += 2)
            for (int k = 0; k < 4; k++)
            {
                Vector2 a = P(side * legs[k, 0], legs[k, 1]);
                float ang = -phase * LegSwing * 0.8f * Tripod(side, k) * side; // 머리(아래쪽)가 앞
                Vector2 b = Swing(a, P(side * legs[k, 2], legs[k, 3]), ang);
                Vector2 e = Swing(a, P(side * legs[k, 4], legs[k, 5]), ang);
                col = Paint(col, line, Capsule(p, a, b, legR));
                col = Paint(col, line, Capsule(p, b, e, legR * 0.85f));
            }

        // 배 (뒤쪽 큰 동그라미, 꼬리 쪽으로 어둡게)
        Vector2 ac = P(0f, 0.15f);
        float arx = 0.21f * s, ary = 0.24f * s;
        col = Paint(col, line, Ellipse(p, ac, arx + 0.022f * s, ary + 0.022f * s));
        float abd = Ellipse(p, ac, arx, ary);
        float shade = Mathf.Lerp(1.15f, 0.85f, Mathf.Clamp01((y - (ac.y - ary)) / (2f * ary)));
        col = Paint(col, Shade(body, shade), abd);
        // 배 무늬 : 가운데 점 3개 + 갈매기 무늬
        col = Paint(col, mark, Mathf.Max(Circle(p, P(0f, 0.04f), 0.035f * s), abd));
        col = Paint(col, mark, Mathf.Max(Circle(p, P(0f, 0.15f), 0.03f * s), abd));
        col = Paint(col, mark, Mathf.Max(Circle(p, P(0f, 0.25f), 0.025f * s), abd));
        for (int side = -1; side <= 1; side += 2)
            col = Paint(col, Shade(mark, 0.85f), Mathf.Max(Capsule(p, P(side * 0.05f, 0.11f), P(side * 0.13f, 0.18f), 0.014f * s), abd));
        col = Paint(col, new Color(1f, 1f, 1f, 0.35f), Mathf.Max(Ellipse(p, P(-0.09f, 0.24f), 0.04f * s, 0.07f * s), abd));

        // 머리가슴
        Vector2 hc = P(0f, -0.15f);
        col = Paint(col, line, Ellipse(p, hc, 0.135f * s, 0.12f * s));
        col = Paint(col, Shade(body, 0.85f), Ellipse(p, hc, 0.115f * s, 0.1f * s));

        // 송곳니
        for (int side = -1; side <= 1; side += 2)
            col = Paint(col, line, Capsule(p, P(side * 0.035f, -0.24f), P(side * 0.02f, -0.29f), 0.017f * s));

        // 눈 : 큰 눈 2개 (아래를 노려봄) + 작은 빨간 눈 2개
        for (int side = -1; side <= 1; side += 2)
        {
            Vector2 e = P(side * 0.045f, -0.2f);
            col = Paint(col, Color.white, Circle(p, e, 0.036f * s));
            col = Paint(col, Hex("#1E1A22"), Circle(p, e + new Vector2(0f, -0.012f * s), 0.02f * s));
            col = Paint(col, Hex("#E0475A"), Circle(p, P(side * 0.085f, -0.15f), 0.016f * s));
        }
        return col;
    }

    // ================================================================= 배경 : 숲 바닥 (위아래로 끊김 없이 이어짐)
    public static Color Ground(int x, int y, int w, int h)
    {
        // 큰 얼룩 + 중간 얼룩 + 잔디 결
        float n = 0.55f * Noise(x, y, w, h, 48, 1) + 0.3f * Noise(x, y, w, h, 24, 2) + 0.15f * Noise(x, y, w, h, 12, 3);
        Color col = Color.Lerp(Hex("#93C66A"), Hex("#7FB658"), n);

        // 진한 풀숲 덩어리
        float clump = Noise(x, y, w, h, 32, 4) * 0.7f + Noise(x, y, w, h, 16, 5) * 0.3f;
        col = Color.Lerp(col, Hex("#679E4C"), Smooth(0.58f, 0.66f, clump));

        // 흙길 조각
        float dirt = Noise(x, y, w, h, 64, 6) * 0.75f + Noise(x, y, w, h, 16, 7) * 0.25f;
        col = Color.Lerp(col, Hex("#C9B27E"), Smooth(0.72f, 0.78f, dirt) * 0.6f);

        // 잔디 결 (아주 작은 밝기 차이)
        col = Shade(col, 0.96f + 0.08f * Hash(x, y, 8));

        // 작은 꽃 / 조약돌 점 (12px 칸마다 하나씩 있을 수도)
        int cell = 12;
        int cx = x / cell, cy = y / cell;
        float roll = Hash(cx, cy, 9);
        if (roll < 0.09f)
        {
            Vector2 center = new Vector2(cx * cell + 3 + Hash(cx, cy, 10) * 6, cy * cell + 3 + Hash(cx, cy, 11) * 6);
            float d = Vector2.Distance(new Vector2(x, y), center);
            Color dot = roll < 0.03f ? Hex("#FFF7E0") : roll < 0.06f ? Hex("#FFE07A") : Hex("#B9AE96");
            col = Color.Lerp(col, dot, Mathf.Clamp01(1.8f - d));
        }
        col.a = 1f;
        return col;
    }

    static float Smooth(float a, float b, float v)
    {
        float t = Mathf.Clamp01((v - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 1442695041;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0x7fffffff) / (float)int.MaxValue;
        }
    }

    // 주기적인 값 노이즈 (텍스처 가장자리에서 이어지도록 격자를 감싸서 계산)
    static float Noise(int x, int y, int w, int h, int cell, int seed)
    {
        int gw = Mathf.Max(1, w / cell), gh = Mathf.Max(1, h / cell);
        float fx = x / (float)cell, fy = y / (float)cell;
        int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
        float tx = fx - x0, ty = fy - y0;
        tx = tx * tx * (3f - 2f * tx);
        ty = ty * ty * (3f - 2f * ty);
        int ax = Wrap(x0, gw), bx = Wrap(x0 + 1, gw), ay = Wrap(y0, gh), by = Wrap(y0 + 1, gh);
        float a = Mathf.Lerp(Hash(ax, ay, seed), Hash(bx, ay, seed), tx);
        float b = Mathf.Lerp(Hash(ax, by, seed), Hash(bx, by, seed), tx);
        return Mathf.Lerp(a, b, ty);
    }

    static int Wrap(int v, int n) { v %= n; return v < 0 ? v + n : v; }
}
