using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

// =====================================================================
//  BugsBugs - 2D 세로 화면 장애물 피하기 (아케이드)
//
//  이 스크립트 하나가 카메라, UI, 플레이어, 장애물을 전부 코드로 만듭니다.
//  -> Assets 폴더에 스크립트 3개만 넣고 Play 를 누르면 바로 실행됩니다.
//     (씬에 아무것도 배치하지 않아도 자동으로 생성됨)
//
//  플로우 : 시작 화면(터치) -> 플레이 -> 결과 화면(다시 하기 / 홈)
//  조작   : Stick(좌하단 조이스틱 이동) / Tap(우하단 대시 버튼, 쿨타임 10초) / Click(실드 버튼, 쿨타임 40초)
//  PC 테스트 : WASD·방향키 이동, Shift 대시, Space 실드, 마우스 클릭 = 터치
// =====================================================================
public class BugsBugsGame : MonoBehaviour
{
    // false 로 바꾸면 자동 생성이 꺼집니다 (직접 빈 오브젝트에 붙여서 쓰고 싶을 때)
    public static bool AutoCreate = true;
    static BugsBugsGame instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (!AutoCreate || instance != null) return;
        new GameObject("BugsBugsGame").AddComponent<BugsBugsGame>();
    }

    // ------------------------------------------------------------ 밸런스 값
    [Header("Player")]
    public float moveSpeed = 5.5f;      // 조이스틱 이동 속도 (유닛/초)
    public float playerSize = 0.7f;     // 캐릭터 지름
    public float dashSpeed = 24f;       // 대시 속도
    public float dashDistance = 2.5f;   // 대시 거리 (이동 방향으로)
    public float dashCooldown = 10f;    // 대시 쿨타임

    [Header("Obstacles")]
    public float startFallSpeed = 2.5f; // 처음 낙하 속도
    public float fallSpeedGain = 0.05f; // 1초마다 늘어나는 낙하 속도
    public float maxFallSpeed = 11f;
    public float startSpawnInterval = 0.9f;
    public float minSpawnInterval = 0.22f;
    public float rampSeconds = 90f;     // 이 시간 동안 생성 간격이 점점 짧아짐
    public float minObstacleSize = 0.5f;
    public float maxObstacleSize = 1.1f;

    [Header("Shield")]
    public float shieldDuration = 2f;   // 무적 시간
    public float shieldCooldown = 30f;  // 쿨타임

    [Header("Items (Flower)")]
    public float firstItemTime = 8f;    // 첫 아이템이 나오는 시간
    public float itemIntervalMin = 10f; // 이후 아이템 간격 (랜덤)
    public float itemIntervalMax = 15f;
    public float itemFallSpeed = 2f;
    public float itemSize = 0.7f;
    public float petalSize = 0.45f;     // 꽃잎 크기
    public int killBonus = 20;          // 꽃잎으로 적을 없앴을 때 보너스 점수
    // 분홍 꽃 : 꽃잎이 플레이어 주위를 돎
    public float orbitDuration = 3.5f;
    public int orbitCount = 3;
    public float orbitRadius = 1.05f;
    public float orbitSpeed = 280f;     // 도/초
    // 보라 꽃 : 꽃잎이 3갈래로 날아감
    public float shotDuration = 2.5f;
    public float shotInterval = 0.35f;
    public float shotSpeed = 9f;
    public float shotSpreadAngle = 22f; // 갈래 사이 각도

    // ------------------------------------------------------------ 색상
    static readonly Color BgColor       = new Color32(0xD8, 0xEE, 0xC0, 0xFF);
    static readonly Color PlayerFill    = new Color32(0x9F, 0xE2, 0xC4, 0xFF);
    static readonly Color PlayerBorder  = new Color32(0x2E, 0x7D, 0x5B, 0xFF);
    static readonly Color ObstacleFill  = new Color32(0xF6, 0xC1, 0xAE, 0xFF);
    static readonly Color ObstacleBorder= new Color32(0xA2, 0x4B, 0x35, 0xFF);
    static readonly Color TextDark      = new Color32(0x22, 0x2B, 0x22, 0xFF);
    static readonly Color ShieldBlue    = new Color32(0x9C, 0xC8, 0xF0, 0xFF);
    static readonly Color DashOrange    = new Color32(0xFF, 0xC2, 0x7A, 0xFF);
    static readonly Color OrbitPink     = new Color32(0xFF, 0xB3, 0xD1, 0xFF);
    static readonly Color OrbitEdge     = new Color32(0xC2, 0x4E, 0x84, 0xFF);
    static readonly Color ShotPurple    = new Color32(0xC9, 0xB6, 0xFF, 0xFF);
    static readonly Color ShotEdge      = new Color32(0x6A, 0x4F, 0xC0, 0xFF);
    static readonly Color AccentYellow  = new Color32(0xFF, 0xD8, 0x4A, 0xFF);

    // ------------------------------------------------------------ 상태
    enum State { Title, Playing, Dying, Result }
    State state;

    class Obstacle
    {
        public Transform tr;
        public SpriteRenderer sr;
        public float half;
        public float speed;
    }

    readonly List<Obstacle> obstacles = new List<Obstacle>();
    readonly Stack<Obstacle> pool = new Stack<Obstacle>();

    enum ItemType { Orbit, Shot }

    class Item
    {
        public Transform tr;
        public SpriteRenderer sr;
        public ItemType type;
        public float baseX, t;
    }

    class Bullet
    {
        public Transform tr;
        public Vector2 vel;
    }

    readonly List<Item> items = new List<Item>();
    readonly Stack<Item> itemPool = new Stack<Item>();
    readonly List<Bullet> bullets = new List<Bullet>();
    readonly Stack<Bullet> bulletPool = new Stack<Bullet>();
    SpriteRenderer[] orbitPetals;
    Sprite itemOrbitSprite, itemShotSprite, petalSprite;
    float itemTimer, orbitTimer, orbitAngle, shotTimer, shotFireTimer;
    int bonusScore;

    Camera cam;
    Vector3 camBasePos = new Vector3(0f, 0f, -10f);
    float halfW, halfH;
    const float HudTop = 0.9f; // 상단 점수/홈 영역만큼 캐릭터가 못 올라가게

    Transform player;
    SpriteRenderer playerSr;
    SpriteRenderer shieldSr;
    Sprite obstacleSprite;

    float elapsed, spawnTimer, shieldTimer, shieldCdTimer, dashCdTimer, dyingTimer, shakeTimer;
    int score, best;
    bool dashing;
    Vector3 dashTarget;
    Vector2 lastMoveDir = Vector2.up; // 멈춰 있을 때 대시 방향

    // UI
    Font font;
    Sprite uiCircle, uiRound;
    GameObject hud, titlePanel, resultPanel;
    Text scoreText, tapToStartText, titleBestText, resultScoreText, resultBestText, newBestText;
    VirtualJoystick joystick;
    Image shieldButtonImg, shieldCdFill, dashButtonImg, dashCdFill;
    Text shieldLabel, dashLabel;

    // =================================================================
    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        Application.targetFrameRate = 60;
        best = PlayerPrefs.GetInt("BugsBugs_Best", 0);

        SetupCamera();
        CreateSprites();
        CreatePlayer();
        CreateOrbitPetals();
        CreateUI();
        GoTitle();
    }

    // ================================================================= 카메라
    void SetupCamera()
    {
        cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            cam = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
        }
        cam.orthographic = true;
        cam.orthographicSize = 5f;          // 화면 세로 = 10 유닛
        cam.transform.position = camBasePos;
        cam.transform.rotation = Quaternion.identity;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = BgColor;
        UpdateBounds();
    }

    void UpdateBounds()
    {
        halfH = cam.orthographicSize;
        halfW = halfH * cam.aspect;
    }

    // ================================================================= 스프라이트 (코드로 생성)
    void CreateSprites()
    {
        uiCircle = MakeSprite(128, (x, y, s) => CirclePixel(x, y, s, Color.white, Color.white, 0f), Vector4.zero);
        uiRound = MakeSprite(64, (x, y, s) => RoundRectPixel(x, y, s, 18f, Color.white, Color.white, 0f), new Vector4(22, 22, 22, 22));

        // Assets/Resources/BugsBugs/Obstacle.png 를 넣으면 그 그림으로 교체됨
        obstacleSprite = Resources.Load<Sprite>("BugsBugs/Obstacle");
        if (obstacleSprite == null)
            obstacleSprite = MakeSprite(64, ObstaclePixel, Vector4.zero);

        // ItemOrbit.png / ItemShot.png / Petal.png 도 같은 폴더에 넣으면 교체됨
        itemOrbitSprite = Resources.Load<Sprite>("BugsBugs/ItemOrbit");
        if (itemOrbitSprite == null)
            itemOrbitSprite = MakeSprite(96, (x, y, s) => FlowerPixel(x, y, s, OrbitPink, OrbitEdge), Vector4.zero);
        itemShotSprite = Resources.Load<Sprite>("BugsBugs/ItemShot");
        if (itemShotSprite == null)
            itemShotSprite = MakeSprite(96, (x, y, s) => FlowerPixel(x, y, s, ShotPurple, ShotEdge), Vector4.zero);
        petalSprite = Resources.Load<Sprite>("BugsBugs/Petal");
        if (petalSprite == null)
            petalSprite = MakeSprite(64, (x, y, s) => PetalPixel(x, y, s, OrbitPink, OrbitEdge), Vector4.zero);
    }

    // 꽃 아이템: 꽃잎 5장 + 노란 가운데
    static Color FlowerPixel(int x, int y, int size, Color petal, Color edge)
    {
        float c = (size - 1) * 0.5f;
        Vector2 p = new Vector2(x, y), ctr = new Vector2(c, c);
        float centerR = size * 0.15f;
        float dc = Vector2.Distance(p, ctr);
        if (dc < centerR) return dc > centerR - 2.5f ? edge : AccentYellow;

        float dist = float.MaxValue; // 가장 가까운 꽃잎 원까지의 거리 (<0 이면 안쪽)
        for (int k = 0; k < 5; k++)
        {
            float a = (90f + k * 72f) * Mathf.Deg2Rad;
            Vector2 pc = ctr + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * size * 0.25f;
            dist = Mathf.Min(dist, Vector2.Distance(p, pc) - size * 0.2f);
        }
        Color col = dist > -3f ? edge : petal;
        col.a *= Mathf.Clamp01(0.5f - dist);
        return col;
    }

    // 꽃잎: 위아래로 긴 타원 (스프라이트의 위쪽이 날아가는 방향)
    static Color PetalPixel(int x, int y, int size, Color fill, Color edge)
    {
        float c = (size - 1) * 0.5f;
        float rx = size * 0.28f, ry = size * 0.47f;
        float nx = (x - c) / rx, ny = (y - c) / ry;
        float dist = (Mathf.Sqrt(nx * nx + ny * ny) - 1f) * rx; // 대략적인 픽셀 거리
        Color col = dist > -3f ? edge : fill;
        col.a *= Mathf.Clamp01(0.5f - dist);
        return col;
    }

    delegate Color PixelFunc(int x, int y, int size);

    static Sprite MakeSprite(int size, PixelFunc f, Vector4 border)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = f(x, y, size);
        tex.SetPixels(px);
        tex.Apply();
        // pixelsPerUnit = size  ->  월드에서 1 x 1 유닛 크기
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect, border);
    }

    static Color CirclePixel(int x, int y, int size, Color fill, Color border, float borderPx)
    {
        float c = (size - 1) * 0.5f;
        float r = size * 0.5f - 1f;
        float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
        float a = Mathf.Clamp01(r - d + 0.5f);
        Color col = d > r - borderPx ? border : fill;
        col.a *= a;
        return col;
    }

    static Color RoundRectPixel(int x, int y, int size, float radius, Color fill, Color border, float borderPx)
    {
        float c = (size - 1) * 0.5f;
        float h = size * 0.5f - 1f;
        float qx = Mathf.Abs(x - c) - (h - radius);
        float qy = Mathf.Abs(y - c) - (h - radius);
        float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
        float dist = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius; // <0 이면 안쪽
        float a = Mathf.Clamp01(0.5f - dist);
        Color col = dist > -borderPx ? border : fill;
        col.a *= a;
        return col;
    }

    // 장애물: 테두리 있는 네모 + 벌레 눈 두 개
    static Color ObstaclePixel(int x, int y, int size)
    {
        Color baseCol = RoundRectPixel(x, y, size, 8f, ObstacleFill, ObstacleBorder, 5f);
        if (baseCol.a < 0.99f) return baseCol;
        Vector2 p = new Vector2(x, y);
        Vector2[] eyes = { new Vector2(size * 0.33f, size * 0.38f), new Vector2(size * 0.67f, size * 0.38f) };
        foreach (var e in eyes)
        {
            float d = Vector2.Distance(p, e);
            if (d < size * 0.065f) return new Color(0.1f, 0.08f, 0.08f, 1f);
            if (d < size * 0.13f) return Color.white;
        }
        return baseCol;
    }

    // ================================================================= 플레이어
    void CreatePlayer()
    {
        var go = new GameObject("Player");
        player = go.transform;
        playerSr = go.AddComponent<SpriteRenderer>();
        playerSr.sortingOrder = 10;

        // Assets/Resources/BugsBugs/Player.png 를 넣으면 그 그림으로 교체됨
        Sprite custom = Resources.Load<Sprite>("BugsBugs/Player");
        playerSr.sprite = custom != null ? custom
            : MakeSprite(128, (x, y, s) => CirclePixel(x, y, s, PlayerFill, PlayerBorder, 8f), Vector4.zero);
        float spriteSize = Mathf.Max(playerSr.sprite.bounds.size.x, playerSr.sprite.bounds.size.y);
        player.localScale = Vector3.one * (playerSize / spriteSize);

        // 실드 이펙트 (플레이어 자식)
        var sh = new GameObject("Shield");
        sh.transform.SetParent(player, false);
        shieldSr = sh.AddComponent<SpriteRenderer>();
        shieldSr.sprite = MakeSprite(128, (x, y, s) =>
            CirclePixel(x, y, s, new Color(0.6f, 0.85f, 1f, 0.35f), new Color(1f, 1f, 1f, 0.9f), 5f), Vector4.zero);
        shieldSr.sortingOrder = 11;
        sh.transform.localScale = Vector3.one * spriteSize * 1.8f; // 부모 스케일 기준
        sh.SetActive(false);
    }

    void CreateOrbitPetals()
    {
        orbitPetals = new SpriteRenderer[orbitCount];
        for (int i = 0; i < orbitCount; i++)
        {
            orbitPetals[i] = NewPetal("OrbitPetal");
            orbitPetals[i].gameObject.SetActive(false);
        }
    }

    SpriteRenderer NewPetal(string name)
    {
        var go = new GameObject(name);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = petalSprite;
        sr.sortingOrder = 12;
        float spriteSize = Mathf.Max(sr.sprite.bounds.size.x, sr.sprite.bounds.size.y);
        go.transform.localScale = Vector3.one * (petalSize / spriteSize);
        return sr;
    }

    Vector3 ClampToField(Vector3 p)
    {
        float r = playerSize * 0.5f;
        p.x = Mathf.Clamp(p.x, -halfW + r, halfW - r);
        p.y = Mathf.Clamp(p.y, -halfH + r, halfH - HudTop - r);
        p.z = 0f;
        return p;
    }

    // ================================================================= 게임 흐름
    void GoTitle()
    {
        state = State.Title;
        ClearField();
        ResetPlayer();
        hud.SetActive(false);
        resultPanel.SetActive(false);
        titlePanel.SetActive(true);
        titleBestText.text = "BEST  " + best;
    }

    void StartGame()
    {
        ClearField();
        ResetPlayer();
        elapsed = 0f;
        score = 0;
        bonusScore = 0;
        itemTimer = firstItemTime;
        spawnTimer = 0.6f;
        shieldTimer = 0f;
        shieldCdTimer = 0f;
        dashCdTimer = 0f;
        lastMoveDir = Vector2.up;
        scoreText.text = "SCORE 0";
        titlePanel.SetActive(false);
        resultPanel.SetActive(false);
        hud.SetActive(true);
        joystick.ResetStick();
        state = State.Playing;
    }

    void ResetPlayer()
    {
        UpdateBounds();
        dashing = false;
        player.gameObject.SetActive(true);
        player.position = ClampToField(new Vector3(0f, -halfH + 2.3f, 0f));
        playerSr.color = Color.white;
        playerSr.enabled = true; // Dying 중 깜빡이다가 홈으로 나가도 다시 보이게
        shakeTimer = 0f;
        shieldSr.gameObject.SetActive(false);
    }

    void Die()
    {
        state = State.Dying;
        dyingTimer = 0.7f;
        shakeTimer = 0.35f;
        dashing = false;
        playerSr.color = new Color(1f, 0.45f, 0.45f, 1f);
        joystick.ResetStick();
    }

    void ShowResult()
    {
        state = State.Result;
        bool isNewBest = score > best;
        if (isNewBest)
        {
            best = score;
            PlayerPrefs.SetInt("BugsBugs_Best", best);
            PlayerPrefs.Save();
        }
        hud.SetActive(false);
        resultPanel.SetActive(true);
        resultScoreText.text = "SCORE  " + score;
        resultBestText.text = "BEST  " + best;
        newBestText.gameObject.SetActive(isNewBest);
    }

    // ================================================================= Update
    void Update()
    {
        UpdateBounds();
        float dt = Time.deltaTime;

        switch (state)
        {
            case State.Title:
                var c = tapToStartText.color;
                c.a = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 4f);
                tapToStartText.color = c;
                break;
            case State.Playing:
                UpdatePlaying(dt);
                break;
            case State.Dying:
                dyingTimer -= dt;
                playerSr.enabled = Mathf.Repeat(dyingTimer, 0.16f) > 0.08f; // 깜빡임
                if (dyingTimer <= 0f) { playerSr.enabled = true; ShowResult(); }
                break;
        }

        // 화면 흔들림
        if (shakeTimer > 0f)
        {
            shakeTimer -= dt;
            float k = Mathf.Clamp01(shakeTimer / 0.35f) * 0.18f;
            cam.transform.position = camBasePos + (Vector3)(Random.insideUnitCircle * k);
        }
        else cam.transform.position = camBasePos;
    }

    void UpdatePlaying(float dt)
    {
        elapsed += dt;
        int newScore = Mathf.FloorToInt(elapsed * 10f) + bonusScore; // 1초 = 10점 + 꽃잎 처치 보너스
        if (newScore != score) { score = newScore; scoreText.text = "SCORE " + score; }

        // ---- 이동 (Stick) / 대시 (Tap)
        Vector2 input = joystick.Value;
        Vector2 kb = KeyboardAxis();
        if (kb.sqrMagnitude > 0.01f) input = Vector2.ClampMagnitude(kb, 1f);
        if (input.sqrMagnitude > 0.01f) lastMoveDir = input.normalized;
        if (KeyboardDashPressed()) TryDash();

        Vector3 p = player.position;
        if (dashing)
        {
            p = Vector3.MoveTowards(p, dashTarget, dashSpeed * dt);
            if ((p - dashTarget).sqrMagnitude < 0.0004f) dashing = false;
        }
        else
        {
            p += (Vector3)(input * moveSpeed * dt);
        }
        player.position = ClampToField(p);

        // ---- 실드 (Click)
        if (KeyboardShieldPressed()) TryShield();
        if (shieldTimer > 0f)
        {
            shieldTimer -= dt;
            bool on = shieldTimer > 0f;
            shieldSr.gameObject.SetActive(on);
            // 끝나기 직전 깜빡임
            if (on && shieldTimer < 0.8f) shieldSr.enabled = Mathf.Repeat(shieldTimer, 0.2f) > 0.1f;
            else shieldSr.enabled = true;
        }
        if (shieldCdTimer > 0f) shieldCdTimer -= dt;
        if (dashCdTimer > 0f) dashCdTimer -= dt;
        UpdateShieldButton();
        UpdateDashButton();

        // ---- 꽃 아이템 / 꽃잎
        UpdateItems(dt);
        UpdatePetals(dt);

        // ---- 장애물 생성 (시간이 지날수록 자주, 빠르게)
        float t01 = Mathf.Clamp01(elapsed / rampSeconds);
        float interval = Mathf.Lerp(startSpawnInterval, minSpawnInterval, t01);
        spawnTimer -= dt;
        if (spawnTimer <= 0f)
        {
            SpawnObstacle();
            spawnTimer = interval;
        }

        // ---- 장애물 이동 + 충돌
        Vector2 pc = player.position;
        float pr = playerSize * 0.5f * 0.85f; // 판정은 살짝 너그럽게
        for (int i = obstacles.Count - 1; i >= 0; i--)
        {
            var o = obstacles[i];
            Vector3 op = o.tr.position;
            op.y -= o.speed * dt;
            o.tr.position = op;

            if (op.y < -halfH - o.half - 0.5f) { Recycle(i); continue; }

            if (PetalHits(op, o.half))
            {
                Recycle(i);
                bonusScore += killBonus;
                continue;
            }

            if (shieldTimer <= 0f && CircleHitsBox(pc, pr, op, o.half * 0.9f))
            {
                Die();
                return;
            }
        }
    }

    static bool CircleHitsBox(Vector2 c, float r, Vector2 b, float half)
    {
        float dx = Mathf.Max(Mathf.Abs(c.x - b.x) - half, 0f);
        float dy = Mathf.Max(Mathf.Abs(c.y - b.y) - half, 0f);
        return dx * dx + dy * dy < r * r;
    }

    void SpawnObstacle()
    {
        Obstacle o = pool.Count > 0 ? pool.Pop() : CreateObstacle();
        float size = Random.Range(minObstacleSize, maxObstacleSize);
        o.half = size * 0.5f;
        float baseSpeed = Mathf.Min(maxFallSpeed, startFallSpeed + elapsed * fallSpeedGain);
        o.speed = baseSpeed * Random.Range(0.85f, 1.15f);

        float spriteSize = Mathf.Max(o.sr.sprite.bounds.size.x, o.sr.sprite.bounds.size.y);
        o.tr.localScale = Vector3.one * (size / spriteSize);
        float x = Random.Range(-halfW + o.half, halfW - o.half);
        o.tr.position = new Vector3(x, halfH + o.half + 0.1f, 0f);
        o.tr.gameObject.SetActive(true);
        obstacles.Add(o);
    }

    Obstacle CreateObstacle()
    {
        var go = new GameObject("Obstacle");
        var o = new Obstacle { tr = go.transform, sr = go.AddComponent<SpriteRenderer>() };
        o.sr.sprite = obstacleSprite;
        o.sr.sortingOrder = 0;
        return o;
    }

    void Recycle(int index)
    {
        var o = obstacles[index];
        obstacles.RemoveAt(index);
        o.tr.gameObject.SetActive(false);
        pool.Push(o);
    }

    void ClearObstacles()
    {
        for (int i = obstacles.Count - 1; i >= 0; i--) Recycle(i);
    }

    // 장애물 + 아이템 + 꽃잎 전부 정리
    void ClearField()
    {
        ClearObstacles();
        for (int i = items.Count - 1; i >= 0; i--) RecycleItem(i);
        for (int i = bullets.Count - 1; i >= 0; i--) RecycleBullet(i);
        orbitTimer = 0f;
        shotTimer = 0f;
        foreach (var p in orbitPetals) p.gameObject.SetActive(false);
    }

    // ================================================================= 꽃 아이템
    void UpdateItems(float dt)
    {
        itemTimer -= dt;
        if (itemTimer <= 0f)
        {
            SpawnItem();
            itemTimer = Random.Range(itemIntervalMin, itemIntervalMax);
        }

        Vector2 pc = player.position;
        float pickR = playerSize * 0.5f + itemSize * 0.4f;
        for (int i = items.Count - 1; i >= 0; i--)
        {
            var it = items[i];
            it.t += dt;
            Vector3 p = it.tr.position;
            p.y -= itemFallSpeed * dt;
            p.x = it.baseX + Mathf.Sin(it.t * 2f) * 0.4f; // 살랑살랑
            it.tr.position = p;
            it.tr.rotation = Quaternion.Euler(0f, 0f, it.t * 60f);

            if (((Vector2)p - pc).sqrMagnitude < pickR * pickR)
            {
                if (it.type == ItemType.Orbit) orbitTimer = orbitDuration;
                else { shotTimer = shotDuration; shotFireTimer = 0f; }
                RecycleItem(i);
                continue;
            }
            if (p.y < -halfH - itemSize) RecycleItem(i);
        }
    }

    void SpawnItem()
    {
        Item it;
        if (itemPool.Count > 0) it = itemPool.Pop();
        else
        {
            var go = new GameObject("Item");
            it = new Item { tr = go.transform, sr = go.AddComponent<SpriteRenderer>() };
            it.sr.sortingOrder = 5;
        }
        it.type = Random.value < 0.5f ? ItemType.Orbit : ItemType.Shot;
        it.sr.sprite = it.type == ItemType.Orbit ? itemOrbitSprite : itemShotSprite;
        float spriteSize = Mathf.Max(it.sr.sprite.bounds.size.x, it.sr.sprite.bounds.size.y);
        it.tr.localScale = Vector3.one * (itemSize / spriteSize);
        float margin = itemSize * 0.5f + 0.4f;
        it.baseX = Random.Range(-halfW + margin, halfW - margin);
        it.t = 0f;
        it.tr.position = new Vector3(it.baseX, halfH + itemSize, 0f);
        it.tr.gameObject.SetActive(true);
        items.Add(it);
    }

    void RecycleItem(int index)
    {
        var it = items[index];
        items.RemoveAt(index);
        it.tr.gameObject.SetActive(false);
        itemPool.Push(it);
    }

    // ================================================================= 꽃잎
    void UpdatePetals(float dt)
    {
        // 분홍 꽃 : 플레이어 주위를 도는 꽃잎
        if (orbitTimer > 0f)
        {
            orbitTimer -= dt;
            orbitAngle += orbitSpeed * dt;
        }
        bool orbitOn = orbitTimer > 0f;
        bool blink = orbitOn && orbitTimer < 1f && Mathf.Repeat(orbitTimer, 0.2f) < 0.1f; // 끝나기 직전 깜빡임
        for (int i = 0; i < orbitPetals.Length; i++)
        {
            var sr = orbitPetals[i];
            sr.gameObject.SetActive(orbitOn);
            if (!orbitOn) continue;
            float a = orbitAngle + 360f / orbitPetals.Length * i;
            float rad = a * Mathf.Deg2Rad;
            sr.transform.position = player.position + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * orbitRadius;
            sr.transform.rotation = Quaternion.Euler(0f, 0f, a - 90f); // 꽃잎 끝이 바깥을 향하게
            sr.enabled = !blink;
        }

        // 보라 꽃 : 3갈래로 날아가는 꽃잎
        if (shotTimer > 0f)
        {
            shotTimer -= dt;
            shotFireTimer -= dt;
            if (shotFireTimer <= 0f)
            {
                for (int k = -1; k <= 1; k++) FireBullet(90f + k * shotSpreadAngle);
                shotFireTimer = shotInterval;
            }
        }

        for (int i = bullets.Count - 1; i >= 0; i--)
        {
            var b = bullets[i];
            Vector3 p = b.tr.position + (Vector3)(b.vel * dt);
            b.tr.position = p;
            if (p.y > halfH + 1f || p.y < -halfH - 1f || Mathf.Abs(p.x) > halfW + 1f) RecycleBullet(i);
        }
    }

    void FireBullet(float angleDeg)
    {
        Bullet b;
        if (bulletPool.Count > 0) b = bulletPool.Pop();
        else
        {
            var sr = NewPetal("ShotPetal");
            sr.color = new Color(0.85f, 0.8f, 1f, 1f); // 보라빛 꽃잎
            b = new Bullet { tr = sr.transform };
        }
        float rad = angleDeg * Mathf.Deg2Rad;
        b.vel = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * shotSpeed;
        b.tr.position = player.position;
        b.tr.rotation = Quaternion.Euler(0f, 0f, angleDeg - 90f);
        b.tr.gameObject.SetActive(true);
        bullets.Add(b);
    }

    void RecycleBullet(int index)
    {
        var b = bullets[index];
        bullets.RemoveAt(index);
        b.tr.gameObject.SetActive(false);
        bulletPool.Push(b);
    }

    // 꽃잎이 장애물에 닿았는지 (날아가는 꽃잎은 맞으면 사라짐)
    bool PetalHits(Vector2 boxPos, float half)
    {
        float r = petalSize * 0.45f;
        if (orbitTimer > 0f)
            foreach (var p in orbitPetals)
                if (CircleHitsBox(p.transform.position, r, boxPos, half)) return true;

        for (int j = bullets.Count - 1; j >= 0; j--)
        {
            if (CircleHitsBox(bullets[j].tr.position, r, boxPos, half))
            {
                RecycleBullet(j);
                return true;
            }
        }
        return false;
    }

    // ================================================================= 입력 콜백
    // 대시 : 이동 중인 방향(멈춰 있으면 마지막 방향)으로 짧게 돌진
    void TryDash()
    {
        if (state != State.Playing) return;
        if (dashCdTimer > 0f || dashing) return;
        dashTarget = ClampToField(player.position + (Vector3)(lastMoveDir * dashDistance));
        dashing = true;
        dashCdTimer = dashCooldown;
    }

    void TryShield()
    {
        if (state != State.Playing) return;
        if (shieldCdTimer > 0f || shieldTimer > 0f) return;
        shieldTimer = shieldDuration;
        shieldCdTimer = shieldCooldown;
        shieldSr.gameObject.SetActive(true);
        shieldSr.enabled = true;
    }

    void UpdateShieldButton()
    {
        if (shieldTimer > 0f)
        {
            shieldButtonImg.color = new Color(0.55f, 0.85f, 1f, 1f);
            shieldCdFill.fillAmount = 0f;
            shieldLabel.text = "ON";
        }
        else if (shieldCdTimer > 0f)
        {
            shieldButtonImg.color = ShieldBlue;
            shieldCdFill.fillAmount = shieldCdTimer / shieldCooldown;
            shieldLabel.text = Mathf.CeilToInt(shieldCdTimer).ToString();
        }
        else
        {
            shieldButtonImg.color = ShieldBlue;
            shieldCdFill.fillAmount = 0f;
            shieldLabel.text = "SHIELD";
        }
    }

    void UpdateDashButton()
    {
        dashButtonImg.color = DashOrange;
        dashCdFill.fillAmount = dashCdTimer > 0f ? dashCdTimer / dashCooldown : 0f;
        dashLabel.text = dashCdTimer > 0f ? Mathf.CeilToInt(dashCdTimer).ToString() : "DASH";
    }

    // PC 테스트용 키보드 입력 (구/신 Input System 모두 지원)
    static Vector2 KeyboardAxis()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        var k = Keyboard.current;
        if (k == null) return Vector2.zero;
        float x = ((k.dKey.isPressed || k.rightArrowKey.isPressed) ? 1f : 0f) - ((k.aKey.isPressed || k.leftArrowKey.isPressed) ? 1f : 0f);
        float y = ((k.wKey.isPressed || k.upArrowKey.isPressed) ? 1f : 0f) - ((k.sKey.isPressed || k.downArrowKey.isPressed) ? 1f : 0f);
        return new Vector2(x, y);
#else
        return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
    }

    static bool KeyboardShieldPressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Space);
#endif
    }

    static bool KeyboardDashPressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Keyboard.current != null && Keyboard.current.shiftKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift);
#endif
    }

    // ================================================================= UI 생성
    void CreateUI()
    {
#if UNITY_2022_2_OR_NEWER
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
        font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif

        // EventSystem (없으면 생성)
        if (EventSystem.current == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            es.AddComponent<InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }

        // Canvas : 기획서 기준 480 x 854 세로 화면
        var canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(480, 854);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        Transform root = canvasGo.transform;

        BuildHud(root);
        BuildTitle(root);
        BuildResult(root);
    }

    void BuildHud(Transform root)
    {
        hud = Stretch(NewRect("HUD", root)).gameObject;

        // 점수 (좌측 상단)
        scoreText = MakeText(hud.transform, "SCORE 0", 30, TextDark, TextAnchor.MiddleLeft);
        Place(scoreText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -14), new Vector2(300, 50));
        scoreText.fontStyle = FontStyle.Bold;

        // 홈 버튼 (우측 상단)
        var home = MakeButton(hud.transform, "HOME", 22, new Vector2(96, 50), Color.white, GoTitle);
        Place(home, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -14), new Vector2(96, 50));

        // 조이스틱 (좌측 하단)
        var stickBase = NewRect("Joystick", hud.transform);
        Place(stickBase, new Vector2(0, 0), new Vector2(0.5f, 0.5f), new Vector2(115, 135), new Vector2(170, 170));
        var baseImg = stickBase.gameObject.AddComponent<Image>();
        baseImg.sprite = uiCircle;
        baseImg.color = new Color(1f, 1f, 1f, 0f); // 안 보이게 (알파 0이어도 터치는 받음)
        var knob = NewRect("Knob", stickBase);
        Place(knob, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(74, 74));
        var knobImg = knob.gameObject.AddComponent<Image>();
        knobImg.sprite = uiCircle;
        knobImg.color = new Color(0.75f, 0.75f, 0.8f, 0f);
        knobImg.raycastTarget = false;
        joystick = stickBase.gameObject.AddComponent<VirtualJoystick>();
        joystick.knob = knob;
        joystick.radius = 55f;

        // 대시 버튼 (우측 하단, Tap)
        BuildCooldownButton(hud.transform, "DashButton", new Vector2(-85, 120), 116, "DASH", 26, DashOrange,
            TryDash, out dashButtonImg, out dashCdFill, out dashLabel);

        // 실드 버튼 (대시 버튼 위, Click) - 작게
        BuildCooldownButton(hud.transform, "ShieldButton", new Vector2(-75, 250), 86, "SHIELD", 17, ShieldBlue,
            TryShield, out shieldButtonImg, out shieldCdFill, out shieldLabel);
    }

    // 원형 버튼 + 쿨타임 표시(어두운 원이 줄어듦) + 라벨
    void BuildCooldownButton(Transform parent, string name, Vector2 pos, float size, string label, int fontSize, Color color,
        System.Action onPress, out Image img, out Image cdFill, out Text text)
    {
        var rt = NewRect(name, parent);
        Place(rt, new Vector2(1, 0), new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
        img = rt.gameObject.AddComponent<Image>();
        img.sprite = uiCircle;
        img.color = color;
        rt.gameObject.AddComponent<PointerDownRelay>().onDown = _ => onPress();

        var cd = Stretch(NewRect("Cooldown", rt));
        cdFill = cd.gameObject.AddComponent<Image>();
        cdFill.sprite = uiCircle;
        cdFill.color = new Color(0f, 0f, 0f, 0.4f);
        cdFill.type = Image.Type.Filled;
        cdFill.fillMethod = Image.FillMethod.Radial360;
        cdFill.fillOrigin = (int)Image.Origin360.Top;
        cdFill.fillClockwise = false;
        cdFill.fillAmount = 0f;
        cdFill.raycastTarget = false;

        text = MakeText(rt, label, fontSize, TextDark, TextAnchor.MiddleCenter);
        Stretch(text.rectTransform);
        text.fontStyle = FontStyle.Bold;
    }

    void BuildTitle(Transform root)
    {
        var panel = Stretch(NewRect("TitlePanel", root));
        titlePanel = panel.gameObject;
        var bg = panel.gameObject.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.15f, 0.08f, 0.55f);
        panel.gameObject.AddComponent<PointerDownRelay>().onDown = _ => StartGame(); // 화면 터치 = 시작

        var title = MakeText(panel, "BugsBugs", 84, AccentYellow, TextAnchor.MiddleCenter);
        Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 170), new Vector2(460, 120));
        title.fontStyle = FontStyle.Bold;
        AddOutline(title, new Color(0.25f, 0.15f, 0f, 1f), 3f);

        tapToStartText = MakeText(panel, "TAP TO START", 38, Color.white, TextAnchor.MiddleCenter);
        Place(tapToStartText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(460, 60));
        tapToStartText.fontStyle = FontStyle.Bold;
        AddOutline(tapToStartText, new Color(0, 0, 0, 0.6f), 2f);

        titleBestText = MakeText(panel, "BEST  0", 26, Color.white, TextAnchor.MiddleCenter);
        Place(titleBestText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -80), new Vector2(460, 40));
    }

    void BuildResult(Transform root)
    {
        var panel = Stretch(NewRect("ResultPanel", root));
        resultPanel = panel.gameObject;
        var bg = panel.gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.6f);

        var card = NewRect("Card", panel);
        Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(380, 470));
        var cardImg = card.gameObject.AddComponent<Image>();
        cardImg.sprite = uiRound;
        cardImg.type = Image.Type.Sliced;
        cardImg.color = new Color(1f, 0.98f, 0.92f, 1f);

        var over = MakeText(card, "GAME OVER", 54, new Color32(0xC0, 0x3A, 0x2B, 0xFF), TextAnchor.MiddleCenter);
        Place(over.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(360, 70));
        over.fontStyle = FontStyle.Bold;

        resultScoreText = MakeText(card, "SCORE  0", 40, TextDark, TextAnchor.MiddleCenter);
        Place(resultScoreText.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -115), new Vector2(360, 56));
        resultScoreText.fontStyle = FontStyle.Bold;

        resultBestText = MakeText(card, "BEST  0", 26, new Color(0.35f, 0.4f, 0.35f, 1f), TextAnchor.MiddleCenter);
        Place(resultBestText.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(360, 40));

        newBestText = MakeText(card, "NEW BEST!", 26, new Color32(0xE0, 0x8A, 0x00, 0xFF), TextAnchor.MiddleCenter);
        Place(newBestText.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -210), new Vector2(360, 40));
        newBestText.fontStyle = FontStyle.Bold;

        var retry = MakeButton(card, "RETRY", 30, new Vector2(260, 72), new Color32(0x9F, 0xE2, 0xC4, 0xFF), StartGame);
        Place(retry, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 120), new Vector2(260, 72));

        var home = MakeButton(card, "HOME", 30, new Vector2(260, 72), Color.white, GoTitle);
        Place(home, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 32), new Vector2(260, 72));
    }

    // ----------------------------------------------------------------- UI 도우미
    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static RectTransform Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    Text MakeText(Transform parent, string s, int size, Color color, TextAnchor align)
    {
        var rt = NewRect("Text", parent);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = font;
        t.text = s;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static void AddOutline(Text t, Color c, float dist)
    {
        var o = t.gameObject.AddComponent<Outline>();
        o.effectColor = c;
        o.effectDistance = new Vector2(dist, -dist);
    }

    RectTransform MakeButton(Transform parent, string label, int fontSize, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick)
    {
        var rt = NewRect(label + "Button", parent);
        rt.sizeDelta = size;
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = uiRound;
        img.type = Image.Type.Sliced;
        img.color = color;
        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var t = MakeText(rt, label, fontSize, TextDark, TextAnchor.MiddleCenter);
        Stretch(t.rectTransform);
        t.fontStyle = FontStyle.Bold;
        return rt;
    }
}
