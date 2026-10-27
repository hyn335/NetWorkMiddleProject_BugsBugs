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
    public float moveSpeed = 3.0f;      // 조이스틱 이동 속도 (유닛/초). 화면 폭 약 5.6
    public float playerSize = 0.7f;     // 캐릭터 지름

    [Header("Obstacles")]
    public float startFallSpeed = 2f;   // 처음 낙하 속도
    public float fallSpeedGain = 0.04f; // 1초마다 늘어나는 낙하 속도
    public float maxFallSpeed = 6f;   // 후반 낙하 속도 상한 (플레이어 이동 3.0 에 맞춰 낮춤)
    public float startSpawnInterval = 0.9f;
    public float minSpawnInterval = 0.3f;
    public float rampSeconds = 120f;    // 이 시간 동안 생성 간격이 점점 짧아짐
    public float minObstacleSize = 0.6f; // 그림 크기 (몸통 길이는 약 80%, 폭은 약 50%)
    public float maxObstacleSize = 1.0f;

    [Header("Special Bugs")]
    public float zigzagAmplitude = 1.1f; // 지그재그 좌우 폭
    public float zigzagFrequency = 3f;   // 지그재그 속도
    public float chaserTurnSpeed = 1.1f; // 추적 벌레가 좌우로 따라오는 속도 (유닛/초)
    public float chargerWarnTime = 1f;   // 돌진 벌레 경고선 시간
    public float chargerSpeed = 7f;      // 돌진 벌레 가로 속도
    public float chargerSize = 0.9f;     // 거미 크기 (경고 구역 폭)
    public float wormSize = 1.7f;        // 지렁이 그림 크기 (몸 길이는 약 80%)

    // ------------------------------------------------------------ 레벨 디자인
    //  점수가 기준을 넘으면 다음 레벨. (점수 = 1초에 10점 + 꽃잎 처치 보너스)
    //  zigzag / chaser : 떨어지는 벌레 중 해당 종류가 나올 확률
    //  chargerEvery    : 돌진 벌레가 나오는 간격(초), 0 이면 안 나옴
    //  vertical        : 돌진 벌레 중 세로(위에서 아래로) 돌진 확률
    struct Level
    {
        public int score;
        public string title;
        public float zigzag, chaser, chargerEvery, vertical;
        public Level(int score, string title, float zigzag, float chaser, float chargerEvery, float vertical)
        {
            this.score = score; this.title = title;
            this.zigzag = zigzag; this.chaser = chaser; this.chargerEvery = chargerEvery; this.vertical = vertical;
        }
    }

    static readonly Level[] Levels =
    {
        new Level(  0, "",              0f,    0f,    0f,   0f),    // LV1 (0~15초)  : 기본 벌레만. 조작 익히기
        new Level(150, "ZIGZAG BUGS!",  0.30f, 0f,    0f,   0f),    // LV2 (~15초)   : 지그재그 등장
        new Level(350, "EARTHWORMS!",   0.30f, 0f,    8f,   0f),    // LV3 (~35초)   : 경고 후 지렁이가 꿈틀거리며 가로로 지나감
        new Level(600, "CHASER BUGS!",  0.35f, 0.15f, 6.5f, 0f),    // LV4 (~60초)   : 플레이어를 따라오는 벌레
        new Level(900, "BUG STORM!",    0.40f, 0.20f, 5f,   0.4f),  // LV5 (~90초)   : 거미가 줄 타고 내려왔다 올라감 추가, 전부 자주
    };

    [Header("Shield")]
    public float shieldMaxEnergy = 2.5f;   // 게이지가 꽉 찼을 때 버틸 수 있는 무적 시간 (초)
    public float shieldCooldown = 15f;     // 한 번 쓰고 나면 못 쓰는 시간 (초)
    public float shieldTapTime = 0.25f;    // 이 시간 안에 떼면 "톡" = 방패 쾅!
    public float slamRadius = 1.3f;        // 방패 쾅이 벌레를 날려버리는 범위

    [Header("Items (Flower)")]
    public float itemIntervalMin = 3.5f; // 아이템 간격 3.5~7초 중 랜덤 (첫 아이템도)
    public float itemIntervalMax = 7f;
    public float itemShrinkPer100 = 0.04f; // 점수 100점마다 간격 4% 줄어듦
    public float itemMinScale = 0.6f;      // 최대 60%(약 2~4초)까지만 줄어듦
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
    // 콩벌레 : 몸을 말고 X자 대각선 4방향으로 굴러가며 벽에 튕김
    public int rollCount = 4;           // 4 = X자 대각선, 2 = 위쪽 대각선만
    public float rollDuration = 2.5f;
    public float rollSpeed = 6.5f;
    public float rollSize = 0.6f;

    // ------------------------------------------------------------ 색상
    static readonly Color BgColor       = new Color32(0xD8, 0xEE, 0xC0, 0xFF);
    static readonly Color ObstacleFill  = new Color32(0xEE, 0x5A, 0x4C, 0xFF); // 기본 : 빨강
    static readonly Color ObstacleBorder= new Color32(0x7A, 0x24, 0x20, 0xFF);
    static readonly Color ZigzagFill    = new Color32(0x72, 0xC9, 0x6B, 0xFF); // 지그재그 : 초록 + 지그재그 무늬
    static readonly Color ZigzagBorder  = new Color32(0x2E, 0x6B, 0x33, 0xFF);


    static readonly Color ChaserFill    = new Color32(0xAE, 0x82, 0xE0, 0xFF); // 추적 : 보라 날벌레 (날개)
    static readonly Color ChaserBorder  = new Color32(0x4E, 0x2E, 0x7A, 0xFF);
    static readonly Color TextDark      = new Color32(0x22, 0x2B, 0x22, 0xFF);
    static readonly Color ShieldBlue    = new Color32(0x9C, 0xC8, 0xF0, 0xFF);
    static readonly Color OrbitPink     = new Color32(0xFF, 0xB3, 0xD1, 0xFF);
    static readonly Color OrbitEdge     = new Color32(0xC2, 0x4E, 0x84, 0xFF);
    static readonly Color ShotPurple    = new Color32(0xC9, 0xB6, 0xFF, 0xFF);
    static readonly Color ShotEdge      = new Color32(0x6A, 0x4F, 0xC0, 0xFF);
    static readonly Color AccentYellow  = new Color32(0xFF, 0xD8, 0x4A, 0xFF);

    // ------------------------------------------------------------ 상태
    enum State { Title, Playing, Paused, Dying, Result }
    State state;

    enum BugKind { Basic, Zigzag, Chaser, Charger, ChargerDown }

    class Obstacle
    {
        public Transform tr;
        public SpriteRenderer sr;
        public BugKind kind;
        public float half;
        public float speed;   // 낙하 속도 (돌진 벌레는 가로 속도, 부호 = 방향)
        public float baseX, t; // 지그재그용
        public SpriteRenderer thread; // 거미줄 (거미만 사용)
        public Vector2 origin;        // 거미줄이 시작되는 곳 (화면 밖)
        public float walkOffset;      // 걸음 박자 어긋나게
        public int phase;             // 거미 : 0 내려옴, 1 매달림, 2 올라감
        public float targetY, hangT;  // 거미가 멈출 높이, 매달린 시간
    }

    // 돌진 벌레 경고선
    class Warning
    {
        public SpriteRenderer sr;
        public float y, x, t;
        public int dir; // +1 : 왼쪽에서 오른쪽으로, -1 : 반대
        public bool vertical; // true : 위에서 아래로 돌진 (x 사용)
        public SpriteRenderer thread; // 미리 쳐지는 거미줄 한 가닥
    }

    readonly List<Obstacle> obstacles = new List<Obstacle>();
    readonly Stack<Obstacle> pool = new Stack<Obstacle>();
    readonly List<Warning> warnings = new List<Warning>();
    readonly Stack<Warning> warningPool = new Stack<Warning>();
    Sprite[][] bugFrames; // BugKind 순서, 종류마다 걸음 그림 3장
    Sprite whiteSprite;
    int level;           // Levels 배열 인덱스
    float chargerTimer, levelBannerTimer;

    enum ItemType { Orbit, Shot, Roll }

    // 굴러가는 콩벌레 (적에 닿아도 안 사라지고 관통)
    class Roller
    {
        public Transform tr;
        public SpriteRenderer sr;
        public Vector2 vel;
        public float t;
    }

    readonly List<Roller> rollers = new List<Roller>();
    readonly Stack<Roller> rollerPool = new Stack<Roller>();
    Sprite pillbugSprite;
    static readonly Color PillbugFill   = new Color32(0x9A, 0xA8, 0xB8, 0xFF);
    static readonly Color PillbugBorder = new Color32(0x3E, 0x4A, 0x5A, 0xFF);

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
        public float t; // 날아간 시간 (흔들림 애니메이션용)
    }

    readonly List<Item> items = new List<Item>();
    readonly Stack<Item> itemPool = new Stack<Item>();
    readonly List<ItemType> itemBag = new List<ItemType>(); // 아이템 순서 주머니 (3종을 한 번씩 섞어서 꺼냄)
    readonly List<Bullet> bullets = new List<Bullet>();
    readonly Stack<Bullet> bulletPool = new Stack<Bullet>();
    SpriteRenderer[] orbitPetals;
    Sprite itemOrbitSprite, itemShotSprite, petalSprite, shotPetalSprite;
    float itemTimer, orbitTimer, orbitAngle, shotTimer, shotFireTimer;
    int bonusScore;

    Camera cam;
    Vector3 camBasePos = new Vector3(0f, 0f, -10f);
    float halfW, halfH;
    const float HudTop = 0.9f; // 상단 점수/홈 영역만큼 캐릭터가 못 올라가게

    Transform player;

    // 숲 바닥 배경 (두 장을 이어 붙여 아래로 계속 흐름)
    public float groundScrollSpeed = 0.6f;
    readonly Transform[] groundTiles = new Transform[2];
    float groundTileH;
    SpriteRenderer playerSr;
    SpriteRenderer shieldSr;
    Sprite[] playerFrames;  // 무당벌레 걸음 그림 3장
    float playerWalkT;

    float elapsed, spawnTimer, shieldEnergy, shieldCdTimer, dyingTimer, shakeTimer;
    bool shieldOn, shieldNeedRelease;
    float shieldPressT; // 실드 버튼을 누른 뒤 지난 시간 (톡 / 꾹 구분)
    int score, best;
    Vector2 lastMoveDir = Vector2.up; // 멈춰 있을 때 대시 방향

    // UI
    Font font;
    bool customFont; // Resources 의 폰트 파일을 쓰는 중인지
    Sprite uiCircle;
    GameObject hud, titlePanel, resultPanel, pausePanel, howToPanel;
    Text howToButtonLabel;
    bool howToStartsGame; // 조작 안내의 버튼이 게임을 시작하는지 (첫 판) / 그냥 닫는지 (로비에서 열었을 때)
    Text hintText;      // 조작 안내 (게임 시작 때)
    Text resultScoreLabel;
    float hintTimer;
    Image titleBug;              // 타이틀의 걷는 무당벌레
    readonly List<Image> soundIcons = new List<Image>(); // 타이틀·게임 화면 사운드 아이콘 (같이 바뀜)
    Text levelText;
    Text scoreText, tapToStartText, titleBestText, resultScoreText, resultBestText, newBestText;
    // true : 레이디버그식 드래그 조작 (화면 아무 데나 끌면 그만큼 이동) / false : 가상 조이스틱
    public bool useDragControl = false;
    VirtualJoystick joystick; // useDragControl = false 일 때만 생성
    DragPad dragPad;          // useDragControl = true 일 때만 생성
    BugsSound sound;
    Image shieldButtonImg, shieldGauge, shieldIconImg;
    Text shieldCdText; // 쿨타임 남은 초
    HoldButton shieldHold;
    Sprite chunkySprite, chunkyCircleSprite, ringSprite, leafSprite, shieldIcon, iconHome, iconRetry, iconSoundOn, iconSoundOff, iconPause, iconPlay;
    static readonly Color Wood       = new Color32(0xD0, 0x95, 0x5C, 0xFF);
    static readonly Color LeafGreen  = new Color32(0x84, 0xCF, 0x5E, 0xFF);
    static readonly Color Cream      = new Color32(0xFF, 0xF6, 0xDC, 0xFF);
    static readonly Color IconBrown  = new Color32(0x4A, 0x2E, 0x1A, 0xFF);
    static readonly Color DeepGreen  = new Color32(0x24, 0x4A, 0x1A, 0xFF);

    // =================================================================
    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL 은 -1 이어야 브라우저 화면 갱신(requestAnimationFrame)에 맞춰 돌아감.
        // 60 으로 고정하면 타이머 방식으로 바뀌어 프레임이 들쭉날쭉해지고 입력이 늦게 느껴짐
        Application.targetFrameRate = -1;
#else
        Application.targetFrameRate = 60;
#endif
        best = PlayerPrefs.GetInt("BugsBugs_Best", 0);

        SetupCamera();
        CreateSprites();
        CreateGround();
        CreatePlayer();
        CreateOrbitPetals();
        sound = gameObject.AddComponent<BugsSound>();
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
        // 숲 느낌 UI : 도톰한 버튼(9-slice, 색은 Image.color 로), 나뭇잎 카드, 아이콘들
        chunkySprite = BugsArt.BakeSliced(96, 96, BugsArt.ChunkyButton, new Vector4(30, 30, 30, 30));
        chunkyCircleSprite = BugsArt.Bake(128, 128, BugsArt.ChunkyCircle, 100f);
        ringSprite = BugsArt.Bake(128, 128, BugsArt.Ring, 100f);
        leafSprite = BugsArt.Bake(304, 376, BugsArt.Leaf, 100f);
        shieldIcon = BugsArt.Bake(128, 128, BugsArt.Shield, 100f);
        iconHome = BugsArt.Bake(64, 64, BugsArt.IconHome, 100f);
        iconRetry = BugsArt.Bake(64, 64, BugsArt.IconRetry, 100f);
        iconSoundOn = BugsArt.Bake(64, 64, (x, y, w, h) => BugsArt.IconSound(x, y, w, h, true), 100f);
        iconSoundOff = BugsArt.Bake(64, 64, (x, y, w, h) => BugsArt.IconSound(x, y, w, h, false), 100f);
        iconPause = BugsArt.Bake(64, 64, BugsArt.IconPause, 100f);
        iconPlay = BugsArt.Bake(64, 64, BugsArt.IconPlay, 100f);

        // Assets/Resources/BugsBugs/Obstacle.png 를 넣으면 그 그림으로 교체됨 (그림 파일은 다리 움직임 없이 한 장)
        // ObstacleZigzag.png / ObstacleChaser.png / ObstacleCharger.png(지렁이) / ObstacleSpider.png 도 같은 방식
        bugFrames = new[]
        {
            LoadOrMake("BugsBugs/Obstacle",        ObstacleFill, ObstacleBorder, BugsArt.BeetleStyle.Plain),
            LoadOrMake("BugsBugs/ObstacleZigzag",  ZigzagFill,   ZigzagBorder,   BugsArt.BeetleStyle.Zigzag),
            LoadOrMake("BugsBugs/ObstacleChaser",  ChaserFill,   ChaserBorder,   BugsArt.BeetleStyle.Wings),
            LoadOrBakeFrames("BugsBugs/ObstacleCharger", BugsArt.BakeWiggle(96, BugsArt.Worm)), // 가로 돌진 = 지렁이 (꿈틀 4장)
            LoadOrBake("BugsBugs/ObstacleSpider", BugsArt.Spider),                             // 세로 돌진 = 거미
        };
        whiteSprite = MakeSprite(4, (x, y, s) => Color.white, Vector4.zero);

        // ItemOrbit.png / ItemShot.png / Petal.png 도 같은 폴더에 넣으면 교체됨
        itemOrbitSprite = Resources.Load<Sprite>("BugsBugs/ItemOrbit");
        if (itemOrbitSprite == null)
            itemOrbitSprite = MakeSprite(96, (x, y, s) => FlowerPixel(x, y, s, OrbitPink, OrbitEdge), Vector4.zero);
        itemShotSprite = Resources.Load<Sprite>("BugsBugs/ItemShot");
        if (itemShotSprite == null)
            itemShotSprite = MakeSprite(96, (x, y, s) => FlowerPixel(x, y, s, ShotPurple, ShotEdge), Vector4.zero);
        petalSprite = Resources.Load<Sprite>("BugsBugs/Petal");
        if (petalSprite == null)
            petalSprite = BugsArt.Bake(64, 64, (x, y, w, h) => BugsArt.Petal(x, y, w, h, new Color(1f, 0.84f, 0.91f), new Color32(0xFF, 0x7E, 0xB6, 0xFF), OrbitEdge), 64f);
        shotPetalSprite = Resources.Load<Sprite>("BugsBugs/ShotPetal");
        if (shotPetalSprite == null)
            shotPetalSprite = BugsArt.Bake(64, 64, (x, y, w, h) => BugsArt.Petal(x, y, w, h, new Color(0.9f, 0.86f, 1f), new Color32(0xA0, 0x7E, 0xFF, 0xFF), ShotEdge), 64f);
        // Pillbug.png : 콩벌레 아이템 + 굴러가는 콩벌레 그림
        pillbugSprite = Resources.Load<Sprite>("BugsBugs/Pillbug");
        if (pillbugSprite == null)
            pillbugSprite = MakeSprite(96, PillbugPixel, Vector4.zero);
    }

    // 몸을 만 콩벌레: 회색 공 + 등껍질 마디 줄무늬 + 하이라이트
    static Color PillbugPixel(int x, int y, int size)
    {
        Color col = CirclePixel(x, y, size, PillbugFill, PillbugBorder, 5f);
        if (col.a < 0.99f) return col;
        float c = (size - 1) * 0.5f;
        float r = size * 0.5f - 6f;
        float dx = x - c, dy = y - c;
        // 마디 : 가운데가 볼록한 곡선 줄 4개
        for (int k = -2; k <= 1; k++)
        {
            float lineY = (k + 0.5f) * r * 0.42f - dx * dx / (r * 2.2f);
            if (Mathf.Abs(dy - lineY) < 2.2f) return PillbugBorder;
        }
        // 왼쪽 위 하이라이트
        if (Vector2.Distance(new Vector2(dx, dy), new Vector2(-r * 0.4f, r * 0.45f)) < r * 0.18f)
            return new Color(1f, 1f, 1f, 1f);
        return col;
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

    // 걸음 그림 3장. Resources 에 그림 파일이 있으면 그 한 장을 3번 씀 (다리 움직임 없음)
    // 이미 구운 그림 여러 장. Resources 에 그림 파일이 있으면 그 한 장으로 대신함
    static Sprite[] LoadOrBakeFrames(string path, Sprite[] baked)
    {
        Sprite s = Resources.Load<Sprite>(path);
        return s != null ? new[] { s, s, s, s } : baked;
    }

    static Sprite[] LoadOrBake(string path, BugsArt.PhasePixelFunc f)
    {
        Sprite s = Resources.Load<Sprite>(path);
        return s != null ? new[] { s, s, s } : BugsArt.BakeWalk(96, f);
    }

    // 적 벌레 그림 : Resources 에 그림이 있으면 그걸, 없으면 BugsArt 로 그림
    static Sprite[] LoadOrMake(string path, Color fill, Color border, BugsArt.BeetleStyle style)
    {
        return LoadOrBake(path, (x, y, w, h, ph) => BugsArt.Beetle(x, y, w, h, fill, border, style, ph));
    }

    // ================================================================= 플레이어
    void CreatePlayer()
    {
        var go = new GameObject("Player");
        player = go.transform;
        playerSr = go.AddComponent<SpriteRenderer>();
        playerSr.sortingOrder = 10;

        // Assets/Resources/BugsBugs/Player.png 를 넣으면 그 그림으로 교체됨 (다리 움직임 없이 한 장)
        Sprite custom = Resources.Load<Sprite>("BugsBugs/Player");
        playerFrames = custom != null ? new[] { custom, custom, custom } : BugsArt.BakeWalk(128, BugsArt.Ladybug);
        playerSr.sprite = playerFrames[0];
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
            orbitPetals[i] = NewPetal("OrbitPetal", petalSprite);
            orbitPetals[i].gameObject.SetActive(false);
        }
    }

    SpriteRenderer NewPetal(string name, Sprite sprite)
    {
        var go = new GameObject(name);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 12;
        float spriteSize = Mathf.Max(sr.sprite.bounds.size.x, sr.sprite.bounds.size.y);
        go.transform.localScale = Vector3.one * (petalSize / spriteSize);
        return sr;
    }

    // ================================================================= 배경
    void CreateGround()
    {
        // Ground.png 를 넣으면 그 그림으로 (위아래가 이어지는 그림이어야 자연스러움)
        Sprite sprite = Resources.Load<Sprite>("BugsBugs/Ground");
        if (sprite == null) sprite = BugsArt.Bake(192, 384, BugsArt.Ground, 192f); // 가로 1유닛짜리
        Vector2 size = sprite.bounds.size;
        float scale = halfW * 2f / size.x * 1.02f;   // 화면 폭에 맞춤
        groundTileH = size.y * scale;
        for (int i = 0; i < groundTiles.Length; i++)
        {
            var go = new GameObject("Ground" + i);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = -10;
            go.transform.localScale = Vector3.one * scale;
            go.transform.position = new Vector3(0f, i * groundTileH, 0f);
            groundTiles[i] = go.transform;
        }
    }

    void UpdateGround(float dt)
    {
        if (state != State.Title && state != State.Playing) return;
        foreach (var t in groundTiles)
        {
            if (t == null) return; // Play 중에 스크립트가 다시 컴파일되면 비어 있을 수 있음
            Vector3 p = t.position;
            p.y -= groundScrollSpeed * dt;
            if (p.y < -groundTileH) p.y += groundTileH * groundTiles.Length; // 아래로 빠지면 위로 올림
            t.position = p;
        }
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
        pausePanel.SetActive(false);
        howToPanel.SetActive(false);
        sound.SetBgmDucked(false);
        titlePanel.SetActive(true);
        titleBestText.text = "BEST " + best;
    }

    // 일시정지 (HUD 의 일시정지 버튼 / ESC 키). HOME 은 일시정지 화면 안에서만 → 실수로 판이 끝나지 않음
    void PauseGame()
    {
        if (state != State.Playing) return;
        state = State.Paused;
        ResetMoveInput();
        if (shieldHold != null) shieldHold.Release();
        pausePanel.SetActive(true);
        sound.SetBgmDucked(true);
    }

    void ResumeGame()
    {
        if (state != State.Paused) return;
        pausePanel.SetActive(false);
        sound.SetBgmDucked(false);
        ResetMoveInput();
        state = State.Playing;
    }

    void StartGame()
    {
        ClearField();
        ResetPlayer();
        elapsed = 0f;
        score = 0;
        bonusScore = 0;
        level = 0;
        chargerTimer = 0f;
        levelBannerTimer = 0f;
        levelText.gameObject.SetActive(false);
        itemBag.Clear();
        itemTimer = NextItemInterval();
        spawnTimer = 0.6f;
        shieldEnergy = shieldMaxEnergy;
        shieldCdTimer = 0f;
        shieldOn = false;
        shieldNeedRelease = false;
        lastMoveDir = Vector2.up;
        scoreText.text = "SCORE 0";
        titlePanel.SetActive(false);
        resultPanel.SetActive(false);
        pausePanel.SetActive(false);
        hud.SetActive(true);
        // 조작 안내 : 처음 움직일 때까지 (최대 5초) 보여줌
        hintTimer = 5f;
        hintText.gameObject.SetActive(true);
        ResetMoveInput();
        state = State.Playing;
        sound.Play(BugsSound.Sfx.Start);
    }

    void ResetPlayer()
    {
        UpdateBounds();
        player.gameObject.SetActive(true);
        player.position = ClampToField(new Vector3(0f, -halfH + 2.3f, 0f));
        player.rotation = Quaternion.identity;
        playerSr.color = Color.white;
        playerSr.enabled = true; // Dying 중 깜빡이다가 홈으로 나가도 다시 보이게
        shakeTimer = 0f;
        shieldSr.gameObject.SetActive(false);
    }

    void Die()
    {
        state = State.Dying;
        sound.Play(BugsSound.Sfx.Die);
        dyingTimer = 0.7f;
        shakeTimer = 0.35f;
        playerSr.color = new Color(1f, 0.45f, 0.45f, 1f);
        ResetMoveInput();
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
        resultScoreText.text = score.ToString();
        resultBestText.text = "BEST " + best;
        newBestText.gameObject.SetActive(isNewBest);
        resultScoreLabel.gameObject.SetActive(!isNewBest);
        if (isNewBest) sound.Play(BugsSound.Sfx.NewBest);
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
                // 타이틀 무당벌레 : 제자리에서 걷듯 다리를 움직이며 살랑
                titleBug.sprite = playerFrames[BugsArt.WalkCycle[(int)(Time.unscaledTime * 8f) % 4]];
                titleBug.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.unscaledTime * 2f) * 12f);
                break;
            case State.Playing:
                if (KeyboardPausePressed()) { PauseGame(); break; }
                UpdatePlaying(dt);
                UpdateSquash(dt);
                break;
            case State.Paused:
                if (KeyboardPausePressed()) ResumeGame();
                break;
            case State.Dying:
                dyingTimer -= dt;
                playerSr.enabled = Mathf.Repeat(dyingTimer, 0.16f) > 0.08f; // 깜빡임
                UpdateSquash(dt);
                if (dyingTimer <= 0f) { playerSr.enabled = true; ShowResult(); }
                break;
        }

        UpdateGround(dt);

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
        UpdateLevel(dt);

        // ---- 이동 (Stick, PC 는 WASD/방향키)
        Vector2 input = joystick != null ? joystick.Value : Vector2.zero;
        Vector2 kb = KeyboardAxis();
        if (kb.sqrMagnitude > 0.01f) input = Vector2.ClampMagnitude(kb, 1f);
        Vector2 drag = dragPad != null ? dragPad.ConsumeWorldDelta(cam) : Vector2.zero;
        if (input.sqrMagnitude > 0.01f) lastMoveDir = input.normalized;
        else if (drag.sqrMagnitude > 0.0001f) lastMoveDir = drag.normalized;

        Vector3 p = player.position;
        p += (Vector3)(input * moveSpeed * dt + drag); // 드래그는 손가락이 움직인 만큼 그대로
        player.position = ClampToField(p);

        // ---- 조작 안내 : 한 번 움직이면 곧 사라짐
        if (hintTimer > 0f)
        {
            if (input.sqrMagnitude > 0.01f || drag.sqrMagnitude > 0.0001f) hintTimer = Mathf.Min(hintTimer, 0.4f);
            hintTimer -= dt;
            var hc = hintText.color;
            hc.a = Mathf.Clamp01(hintTimer / 0.4f) * (0.75f + 0.25f * Mathf.Sin(Time.time * 5f));
            hintText.color = hc;
            if (hintTimer <= 0f) hintText.gameObject.SetActive(false);
        }

        // 무당벌레가 움직이는 방향을 바라보게 (부드럽게 회전)
        if (input.sqrMagnitude > 0.01f || drag.sqrMagnitude > 0.0001f)
        {
            float target = Mathf.Atan2(lastMoveDir.y, lastMoveDir.x) * Mathf.Rad2Deg - 90f;
            float z = Mathf.MoveTowardsAngle(player.eulerAngles.z, target, 720f * dt);
            player.rotation = Quaternion.Euler(0f, 0f, z);
            playerWalkT += dt * 14f; // 다리 걸음
        }
        else playerWalkT = 0f; // 멈추면 다리도 멈춤
        playerSr.sprite = playerFrames[BugsArt.WalkCycle[(int)playerWalkT % 4]];

        // ---- 실드 (Hold) : 누르고 있는 동안만 무적, 게이지가 닳고 떼면 다시 참
        UpdateShield(dt);
        UpdateShieldButton();

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

        // ---- 돌진 벌레 (경고선 -> 가로 돌진)
        float chargerEvery = Levels[level].chargerEvery;
        if (chargerEvery > 0f)
        {
            chargerTimer -= dt;
            if (chargerTimer <= 0f)
            {
                StartChargerWarning(Random.value < Levels[level].vertical);
                chargerTimer = chargerEvery * Random.Range(0.8f, 1.2f);
            }
        }
        UpdateWarnings(dt);

        // ---- 장애물 이동 + 충돌
        Vector2 pc = player.position;
        float pr = playerSize * 0.5f * 0.85f; // 판정은 살짝 너그럽게
        for (int i = obstacles.Count - 1; i >= 0; i--)
        {
            var o = obstacles[i];
            Vector3 op = o.tr.position;
            o.t += dt;
            switch (o.kind)
            {
                case BugKind.Basic:
                    op.y -= o.speed * dt;
                    break;
                case BugKind.Zigzag:
                    op.y -= o.speed * dt;
                    op.x = ZigzagX(o);
                    break;
                case BugKind.Chaser:
                    op.y -= o.speed * dt;
                    if (op.y > pc.y) // 플레이어를 지나치면 더 이상 안 따라감
                        op.x = Mathf.MoveTowards(op.x, pc.x, chaserTurnSpeed * dt);
                    break;
                case BugKind.Charger: // 지렁이 : 가로로 지나감
                    op.x += o.speed * dt;
                    break;
                case BugKind.ChargerDown: // 거미 : 줄 타고 내려옴 -> 대롱대롱 -> 다시 올라감
                    if (o.phase == 0)
                    {
                        op.y -= o.speed * dt;
                        if (op.y <= o.targetY) { op.y = o.targetY; o.phase = 1; o.hangT = 0.6f; }
                    }
                    else if (o.phase == 1)
                    {
                        o.hangT -= dt;
                        op.y = o.targetY + Mathf.Sin(o.hangT * 18f) * 0.06f; // 줄 끝에서 살짝 출렁
                        if (o.hangT <= 0f) o.phase = 2;
                    }
                    else op.y += o.speed * 0.75f * dt;
                    break;
            }
            // 벌레 머리(그림의 아래쪽)가 움직이는 방향을 보게 회전
            Vector2 move = op - o.tr.position;
            bool hanging = o.kind == BugKind.ChargerDown && o.phase == 1; // 매달려 출렁일 땐 방향 유지
            if (move.sqrMagnitude > 1e-8f && !hanging)
                o.tr.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(move.y, move.x) * Mathf.Rad2Deg + 90f);
            o.tr.position = op;
            // 다리 걸음 : 빨리 움직일수록 다리도 빨리 (지렁이는 그림 4장을 차례로 = 꿈틀)
            var frames = bugFrames[(int)o.kind];
            if (o.kind == BugKind.Charger)
                o.sr.sprite = frames[(int)(o.t * 10f + o.walkOffset) % frames.Length];
            else
            {
                float legFps = Mathf.Min(20f, 7f + Mathf.Abs(o.speed) * 1.6f);
                if (o.kind == BugKind.ChargerDown && o.phase == 1) legFps = 4f; // 매달려 있을 땐 다리를 천천히
                o.sr.sprite = frames[BugsArt.WalkCycle[(int)(o.t * legFps + o.walkOffset) % 4]];
            }
            // 거미는 위에서부터 거미줄에 매달려 있음
            if (o.thread != null && o.thread.gameObject.activeSelf) PlaceThread(o.thread, o.origin, op);

            bool gone;
            if (o.kind == BugKind.Charger) gone = Mathf.Abs(op.x) > halfW + o.half + 0.5f;
            else if (o.kind == BugKind.ChargerDown) gone = o.phase == 2 && op.y > halfH + o.half + 0.3f;
            else gone = op.y < -halfH - o.half - 0.5f;
            if (gone) { Recycle(i); continue; }

            if (PetalHits(op, o.half * 0.6f))
            {
                SpawnSquash(o);
                Recycle(i);
                bonusScore += killBonus;
                sound.Play(BugsSound.Sfx.Kill);
                continue;
            }

            if (!shieldOn && BugHitsPlayer(o, op, pc, pr))
            {
                Die();
                return;
            }
        }
    }

    void ResetMoveInput()
    {
        if (joystick != null) joystick.ResetStick();
        if (dragPad != null) dragPad.ResetPad();
    }

    // 벌레와 플레이어 충돌. 보통은 몸통 원 하나(반지름 half * 0.5),
    // 지렁이는 몸이 길어서 머리-가운데-꼬리 원 3개로 판정
    bool BugHitsPlayer(Obstacle o, Vector2 op, Vector2 pc, float pr)
    {
        if (o.kind == BugKind.Charger)
        {
            float r = pr + o.half * 0.17f;
            Vector2 along = o.tr.up * (o.half * 0.6f); // 그림의 위아래 = 지렁이 몸 방향
            return ((op - pc).sqrMagnitude < r * r)
                || ((op + along - pc).sqrMagnitude < r * r)
                || ((op - along - pc).sqrMagnitude < r * r);
        }
        float hitR = pr + o.half * 0.5f;
        return (op - pc).sqrMagnitude < hitR * hitR;
    }

    static bool CircleHitsBox(Vector2 c, float r, Vector2 b, float half)
    {
        float dx = Mathf.Max(Mathf.Abs(c.x - b.x) - half, 0f);
        float dy = Mathf.Max(Mathf.Abs(c.y - b.y) - half, 0f);
        return dx * dx + dy * dy < r * r;
    }

    // 위에서 떨어지는 벌레 (현재 레벨 확률에 따라 기본/지그재그/추적)
    void SpawnObstacle()
    {
        var lv = Levels[level];
        float r = Random.value;
        BugKind kind = r < lv.zigzag ? BugKind.Zigzag
                     : r < lv.zigzag + lv.chaser ? BugKind.Chaser
                     : BugKind.Basic;

        float size = Random.Range(minObstacleSize, maxObstacleSize);
        float baseSpeed = Mathf.Min(maxFallSpeed, startFallSpeed + elapsed * fallSpeedGain);
        float speed = baseSpeed * Random.Range(0.85f, 1.15f);
        if (kind == BugKind.Zigzag) speed *= 0.85f; // 옆으로 움직이는 만큼 조금 느리게
        if (kind == BugKind.Chaser) speed *= 0.7f;

        var o = NewObstacle(kind, size);
        o.speed = speed;
        o.baseX = Random.Range(-halfW + o.half, halfW - o.half); // 상단 어디서나
        float x = o.baseX;
        if (kind == BugKind.Zigzag)
        {
            o.t = Random.Range(0f, Mathf.PI * 2f / zigzagFrequency); // 흔들리는 시작 방향도 랜덤
            x = ZigzagX(o);
        }
        o.tr.position = new Vector3(x, halfH + o.half + 0.1f, 0f);
    }

    // 지그재그 x 좌표. 화면 벽에 닿으면 튕겨서 반대로 (PingPong)
    float ZigzagX(Obstacle o)
    {
        float limit = Mathf.Max(0.01f, halfW - o.half);
        float x = o.baseX + Mathf.Sin(o.t * zigzagFrequency) * zigzagAmplitude;
        return Mathf.PingPong(x + limit, limit * 2f) - limit;
    }

    Obstacle NewObstacle(BugKind kind, float size)
    {
        Obstacle o;
        if (pool.Count > 0) o = pool.Pop();
        else
        {
            var go = new GameObject("Obstacle");
            o = new Obstacle { tr = go.transform, sr = go.AddComponent<SpriteRenderer>() };
            o.sr.sortingOrder = 0;
        }
        o.kind = kind;
        o.half = size * 0.5f;
        o.t = 0f;
        o.sr.sprite = bugFrames[(int)kind][0];
        o.walkOffset = Random.Range(0f, 4f); // 벌레마다 걸음 박자가 다르게
        o.tr.rotation = Quaternion.identity;
        if (o.thread != null) o.thread.gameObject.SetActive(false); // 풀에서 재사용할 때 거미줄 끄기
        float spriteSize = Mathf.Max(o.sr.sprite.bounds.size.x, o.sr.sprite.bounds.size.y);
        o.tr.localScale = Vector3.one * (size / spriteSize);
        o.tr.gameObject.SetActive(true);
        obstacles.Add(o);
        return o;
    }

    // ================================================================= 레벨
    void UpdateLevel(float dt)
    {
        while (level + 1 < Levels.Length && score >= Levels[level + 1].score)
        {
            level++;
            levelText.text = "LEVEL " + (level + 1); // 레벨 숫자만 (벌레 이름 문구는 뺌)
            levelText.gameObject.SetActive(true);
            levelBannerTimer = 2f;
            sound.Play(BugsSound.Sfx.LevelUp);
            if (Levels[level].chargerEvery > 0f && chargerTimer <= 0f) chargerTimer = 1.5f; // 첫 돌진은 바로 보여주기
        }

        if (levelBannerTimer > 0f)
        {
            levelBannerTimer -= dt;
            var c = levelText.color;
            c.a = Mathf.Clamp01(levelBannerTimer / 0.5f); // 마지막 0.5초 동안 사라짐
            levelText.color = c;
            if (levelBannerTimer <= 0f) levelText.gameObject.SetActive(false);
        }
    }

    // ================================================================= 돌진 벌레
    void StartChargerWarning(bool vertical)
    {
        Warning w;
        if (warningPool.Count > 0) w = warningPool.Pop();
        else
        {
            var go = new GameObject("SpiderWarning");
            w = new Warning { sr = go.AddComponent<SpriteRenderer>() };
            w.sr.sprite = whiteSprite;
            w.sr.sortingOrder = -2; // 장애물 뒤 (흐린 빨간 위험 구역)
            w.thread = NewThread();
        }
        float r = chargerSize * 0.5f;
        float spriteSize = w.sr.sprite.bounds.size.x;
        w.vertical = vertical;
        w.t = chargerWarnTime;
        if (vertical)
        {
            // 거미 : 플레이어 x 근처로, 플레이어보다 조금 아래까지 내려옴. 위험 구역 = 위에서 그 높이까지
            w.x = Mathf.Clamp(player.position.x + Random.Range(-0.8f, 0.8f), -halfW + r, halfW - r);
            w.y = Mathf.Clamp(player.position.y - 0.8f, -halfH + 1f, halfH - HudTop - 1.5f); // 멈출 높이
            float top = halfH + 0.5f, bottom = w.y - r;
            w.sr.transform.position = new Vector3(w.x, (top + bottom) * 0.5f, 0f);
            w.sr.transform.localScale = new Vector3(chargerSize / spriteSize, (top - bottom) / spriteSize, 1f);
            PlaceThread(w.thread, new Vector2(w.x, top), new Vector2(w.x, w.y));
            w.thread.gameObject.SetActive(true);
            sound.Play(BugsSound.Sfx.Warn); // 거미줄 소리
        }
        else
        {
            // 지렁이 : 플레이어 높이 근처를 노림 (너무 정확하진 않게)
            w.y = Mathf.Clamp(player.position.y + Random.Range(-1.2f, 1.2f), -halfH + r, halfH - HudTop - r);
            w.dir = Random.value < 0.5f ? 1 : -1;
            w.sr.transform.position = new Vector3(0f, w.y, 0f);
            w.sr.transform.localScale = new Vector3(halfW * 2f / spriteSize, chargerSize / spriteSize, 1f);
            w.thread.gameObject.SetActive(false); // 지렁이는 거미줄 없음
            sound.Play(BugsSound.Sfx.Worm); // 흙 속에서 꿈틀대는 소리
        }
        w.sr.gameObject.SetActive(true);
        warnings.Add(w);
    }

    void UpdateWarnings(float dt)
    {
        for (int i = warnings.Count - 1; i >= 0; i--)
        {
            var w = warnings[i];
            w.t -= dt;
            bool on = Mathf.Repeat(w.t, 0.2f) > 0.1f;
            w.sr.color = new Color(1f, 0.25f, 0.2f, on ? 0.22f : 0.1f);          // 흐린 위험 구역
            w.thread.color = new Color(1f, 1f, 1f, on ? 0.95f : 0.55f);        // 반짝이는 거미줄
            if (w.t > 0f) continue;

            if (w.vertical)
            {
                // 거미가 줄을 타고 위에서 쭉 내려옴 (UpdatePlaying 에서 내려옴 -> 매달림 -> 올라감)
                var o = NewObstacle(BugKind.ChargerDown, chargerSize);
                o.speed = chargerSpeed;
                o.phase = 0;
                o.targetY = w.y;
                o.tr.position = new Vector3(w.x, halfH + o.half + 0.2f, 0f);
                o.origin = new Vector2(w.x, halfH + 1f);
                if (o.thread == null) o.thread = NewThread();
                o.thread.color = new Color(1f, 1f, 1f, 0.85f);
                o.thread.gameObject.SetActive(true);
                PlaceThread(o.thread, o.origin, o.tr.position);
            }
            else
            {
                // 지렁이가 옆에서 꿈틀거리며 지나감
                var o = NewObstacle(BugKind.Charger, wormSize);
                o.speed = chargerSpeed * w.dir;
                o.tr.position = new Vector3(-w.dir * (halfW + o.half + 0.2f), w.y, 0f);
            }
            RecycleWarning(i);
        }
    }

    void RecycleWarning(int index)
    {
        var w = warnings[index];
        warnings.RemoveAt(index);
        w.sr.gameObject.SetActive(false);
        w.thread.gameObject.SetActive(false);
        warningPool.Push(w);
    }

    // 거미줄 한 가닥 (가늘고 하얀 선)
    SpriteRenderer NewThread()
    {
        var go = new GameObject("SpiderThread");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = whiteSprite;
        sr.sortingOrder = -1;
        go.SetActive(false);
        return sr;
    }

    // a 에서 b 까지 이어지는 선으로 배치
    void PlaceThread(SpriteRenderer sr, Vector2 a, Vector2 b)
    {
        Vector2 d = b - a;
        float spriteSize = sr.sprite.bounds.size.x;
        sr.transform.position = (Vector3)((a + b) * 0.5f);
        sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        sr.transform.localScale = new Vector3(d.magnitude / spriteSize, 0.045f / spriteSize, 1f);
    }

    void Recycle(int index)
    {
        var o = obstacles[index];
        obstacles.RemoveAt(index);
        o.tr.gameObject.SetActive(false);
        if (o.thread != null) o.thread.gameObject.SetActive(false);
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
        for (int i = warnings.Count - 1; i >= 0; i--) RecycleWarning(i);
        for (int i = items.Count - 1; i >= 0; i--) RecycleItem(i);
        for (int i = bullets.Count - 1; i >= 0; i--) RecycleBullet(i);
        for (int i = rollers.Count - 1; i >= 0; i--) RecycleRoller(i);
        orbitTimer = 0f;
        shotTimer = 0f;
        foreach (var p in orbitPetals) p.gameObject.SetActive(false);
        ClearSquash();
    }

    // ================================================================= 꽃 아이템
    void UpdateItems(float dt)
    {
        itemTimer -= dt;
        if (itemTimer <= 0f)
        {
            SpawnItem();
            itemTimer = NextItemInterval();
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
                sound.Play(BugsSound.Sfx.Item);
                if (it.type == ItemType.Orbit) orbitTimer = orbitDuration;
                else if (it.type == ItemType.Shot) { shotTimer = shotDuration; shotFireTimer = 0f; }
                else LaunchRollers();
                RecycleItem(i);
                continue;
            }
            if (p.y < -halfH - itemSize) RecycleItem(i);
        }
    }

    // 5~10초 중 랜덤. 점수가 오를수록 조금씩 짧아짐
    float NextItemInterval()
    {
        float scale = Mathf.Max(itemMinScale, 1f - score / 100f * itemShrinkPer100);
        return Random.Range(itemIntervalMin, itemIntervalMax) * scale;
    }

    // 완전 랜덤이면 같은 아이템이 연달아 나올 수 있어서,
    // 3종을 한 번씩 넣은 주머니를 섞어서 하나씩 꺼냄 (3개마다 3종이 꼭 한 번씩 나옴)
    ItemType NextItemType()
    {
        if (itemBag.Count == 0)
        {
            itemBag.Add(ItemType.Orbit);
            itemBag.Add(ItemType.Shot);
            itemBag.Add(ItemType.Roll);
            for (int i = itemBag.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var tmp = itemBag[i]; itemBag[i] = itemBag[j]; itemBag[j] = tmp;
            }
        }
        var t = itemBag[itemBag.Count - 1];
        itemBag.RemoveAt(itemBag.Count - 1);
        return t;
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
        it.type = NextItemType();
        it.sr.sprite = it.type == ItemType.Orbit ? itemOrbitSprite
                     : it.type == ItemType.Shot ? itemShotSprite
                     : pillbugSprite;
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
        // 등장 : 톡 튀어나오듯 커짐 / 퇴장 : 마지막 0.5초 동안 작아지며 투명해짐
        float since = orbitDuration - orbitTimer;
        float appear = EaseOutBack(Mathf.Clamp01(since / 0.3f));
        float fade = Mathf.Clamp01(orbitTimer / 0.5f);
        float k01 = appear * Mathf.Lerp(0.5f, 1f, fade);
        float petalScale = petalSize * 1.45f / petalSprite.bounds.size.y; // 보이는 꽃잎은 판정보다 살짝 크게
        for (int i = 0; i < orbitPetals.Length; i++)
        {
            var sr = orbitPetals[i];
            sr.gameObject.SetActive(orbitOn);
            if (!orbitOn) continue;
            float a = orbitAngle + 360f / orbitPetals.Length * i;
            float rad = a * Mathf.Deg2Rad;
            Vector3 pos = player.position + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * orbitRadius * Mathf.Lerp(0.4f, 1f, appear);
            sr.transform.position = pos;
            // 꽃잎 끝이 바깥을 향하고, 살랑살랑 흔들림
            sr.transform.rotation = Quaternion.Euler(0f, 0f, a - 90f + Mathf.Sin(Time.time * 10f + i) * 12f);
            sr.transform.localScale = Vector3.one * petalScale * k01;
            sr.color = new Color(1f, 1f, 1f, fade);
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
            b.t += dt;
            Vector3 p = b.tr.position + (Vector3)(b.vel * dt);
            b.tr.position = p;
            // 날아가면서 살짝 비틀거림
            float ang = Mathf.Atan2(b.vel.y, b.vel.x) * Mathf.Rad2Deg - 90f + Mathf.Sin(b.t * 25f) * 10f;
            b.tr.rotation = Quaternion.Euler(0f, 0f, ang);
            if (p.y > halfH + 1f || p.y < -halfH - 1f || Mathf.Abs(p.x) > halfW + 1f) RecycleBullet(i);
        }

        UpdateRollers(dt);
    }

    // ================================================================= 콩벌레
    void LaunchRollers()
    {
        sound.Play(BugsSound.Sfx.Roll);
        int n = rollCount >= 4 ? 4 : 2;
        for (int i = 0; i < n; i++)
        {
            float angle = 45f + 90f * i; // 45, 135, (225, 315)
            Roller r;
            if (rollerPool.Count > 0) r = rollerPool.Pop();
            else
            {
                var go = new GameObject("Pillbug");
                r = new Roller { tr = go.transform, sr = go.AddComponent<SpriteRenderer>() };
                r.sr.sprite = pillbugSprite;
                r.sr.sortingOrder = 12;
                float spriteSize = Mathf.Max(r.sr.sprite.bounds.size.x, r.sr.sprite.bounds.size.y);
                go.transform.localScale = Vector3.one * (rollSize / spriteSize);
            }
            float rad = angle * Mathf.Deg2Rad;
            r.vel = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * rollSpeed;
            r.t = rollDuration;
            r.sr.enabled = true;
            r.tr.position = player.position;
            r.tr.gameObject.SetActive(true);
            rollers.Add(r);
        }
    }

    void UpdateRollers(float dt)
    {
        float rad = rollSize * 0.5f;
        float minX = -halfW + rad, maxX = halfW - rad;
        float minY = -halfH + rad, maxY = halfH - HudTop - rad;
        for (int i = rollers.Count - 1; i >= 0; i--)
        {
            var r = rollers[i];
            r.t -= dt;
            if (r.t <= 0f) { RecycleRoller(i); continue; }

            Vector3 p = r.tr.position + (Vector3)(r.vel * dt);
            // 벽에 닿으면 튕김
            if (p.x < minX) { p.x = minX; r.vel.x = Mathf.Abs(r.vel.x); }
            if (p.x > maxX) { p.x = maxX; r.vel.x = -Mathf.Abs(r.vel.x); }
            if (p.y < minY) { p.y = minY; r.vel.y = Mathf.Abs(r.vel.y); }
            if (p.y > maxY) { p.y = maxY; r.vel.y = -Mathf.Abs(r.vel.y); }
            r.tr.position = p;
            // 굴러가는 방향으로 회전
            r.tr.Rotate(0f, 0f, -Mathf.Sign(r.vel.x) * rollSpeed / rad * Mathf.Rad2Deg * dt);
            r.sr.enabled = r.t > 0.6f || Mathf.Repeat(r.t, 0.2f) > 0.1f; // 끝나기 직전 깜빡임
        }
    }

    void RecycleRoller(int index)
    {
        var r = rollers[index];
        rollers.RemoveAt(index);
        r.tr.gameObject.SetActive(false);
        rollerPool.Push(r);
    }

    void FireBullet(float angleDeg)
    {
        Bullet b;
        if (bulletPool.Count > 0) b = bulletPool.Pop();
        else
        {
            var sr = NewPetal("ShotPetal", shotPetalSprite);
            sr.transform.localScale *= 1.15f;
            b = new Bullet { tr = sr.transform };
        }
        float rad = angleDeg * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        b.vel = dir * shotSpeed;
        b.t = Random.value; // 비틀거림 박자가 서로 다르게
        b.tr.position = player.position + (Vector3)(dir * 0.3f);
        b.tr.rotation = Quaternion.Euler(0f, 0f, angleDeg - 90f);
        b.tr.gameObject.SetActive(true);
        bullets.Add(b);
    }

    static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
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
                if (CircleHitsBox(p.transform.position, r * 1.3f, boxPos, half)) return true; // 회전 꽃잎은 크게 그려지므로 판정도 맞춤

        float rr = rollSize * 0.45f;
        foreach (var roller in rollers)
            if (CircleHitsBox(roller.tr.position, rr, boxPos, half)) return true;

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

    // ================================================================= 실드 (Click = 쾅 / Hold = 무적)
    // 버튼(또는 스페이스바)을 누르는 순간 무적이 켜짐.
    //  - 짧게 톡 (shieldTapTime 초 안에 뗌) = 방패 쾅! 주변 벌레를 한 번에 날려버림 (Click)
    //  - 꾹 누르기 = 누르고 있는 동안 무적, 최대 shieldMaxEnergy 초 (Hold)
    // 어느 쪽이든 끝나면 shieldCooldown 초 동안 못 씀 -> 쿨타임이 끝나면 게이지가 다시 꽉 참.
    // 쿨타임이 끝났을 때 아직 누르고 있으면 한 번 뗐다가 다시 눌러야 켜짐 (실수로 바로 켜지는 것 방지)
    void UpdateShield(float dt)
    {
        bool want = (shieldHold != null && shieldHold.IsHeld) || KeyboardShieldHeld();
        if (!want) shieldNeedRelease = false;

        if (shieldCdTimer > 0f)
        {
            shieldCdTimer -= dt;
            if (shieldCdTimer <= 0f) { shieldCdTimer = 0f; shieldEnergy = shieldMaxEnergy; }
        }

        if (shieldOn)
        {
            shieldPressT += dt;
            shieldEnergy -= dt;
            if (!want)
            {
                if (shieldPressT < shieldTapTime) Slam(); // 짧게 톡 = 쾅
                EndShield();
            }
            else if (shieldEnergy <= 0f) EndShield(); // 꾹 누르다가 다 씀
        }
        else if (want && !shieldNeedRelease && shieldCdTimer <= 0f)
        {
            shieldOn = true;
            shieldPressT = 0f;
            sound.Play(BugsSound.Sfx.Shield);
        }

        shieldSr.gameObject.SetActive(shieldOn);
        // 게이지가 거의 바닥나면 깜빡임
        shieldSr.enabled = !shieldOn || shieldEnergy > 0.5f || Mathf.Repeat(shieldEnergy, 0.16f) > 0.08f;
    }

    void EndShield()
    {
        shieldOn = false;
        shieldEnergy = 0f;
        shieldCdTimer = shieldCooldown;
        shieldNeedRelease = true;
    }

    // 방패 쾅! : 플레이어 주변 slamRadius 안의 벌레를 전부 날려버림 + 퍼지는 충격파
    void Slam()
    {
        Vector2 pc = player.position;
        int kills = 0;
        for (int i = obstacles.Count - 1; i >= 0; i--)
        {
            var o = obstacles[i];
            float r = slamRadius + o.half * 0.4f;
            if (((Vector2)o.tr.position - pc).sqrMagnitude < r * r)
            {
                SpawnSquash(o);
                Recycle(i);
                kills++;
            }
        }
        bonusScore += kills * killBonus;
        sound.Play(BugsSound.Sfx.Slam);
        shakeTimer = Mathf.Max(shakeTimer, 0.2f);
        StartShockwave(pc);
    }

    // ---- 충격파 고리 (흙빛 고리가 퍼지며 사라짐)
    SpriteRenderer shockwave;
    float shockT;

    void StartShockwave(Vector2 pos)
    {
        if (shockwave == null)
        {
            var go = new GameObject("Shockwave");
            shockwave = go.AddComponent<SpriteRenderer>();
            shockwave.sprite = BugsArt.Bake(128, 128, BugsArt.Ring, 128f);
            shockwave.sortingOrder = 9;
        }
        shockwave.transform.position = pos;
        shockwave.gameObject.SetActive(true);
        shockT = 0f;
        UpdateShockwave(0f);
    }

    void UpdateShockwave(float dt)
    {
        if (shockwave == null || !shockwave.gameObject.activeSelf) return;
        shockT += dt;
        float k = Mathf.Clamp01(shockT / 0.35f);
        shockwave.transform.position = player.position;
        shockwave.transform.localScale = Vector3.one * Mathf.Lerp(0.4f, slamRadius * 2.2f, 1f - (1f - k) * (1f - k));
        shockwave.color = new Color(0.98f, 0.92f, 0.75f, 0.9f * (1f - k));
        if (k >= 1f) shockwave.gameObject.SetActive(false);
    }

    // ================================================================= 처치 연출 : 납작하게 찌그러지며 사라짐
    class Squash { public SpriteRenderer sr; public Vector3 baseScale; public float t; }
    readonly List<Squash> squashes = new List<Squash>();
    readonly Stack<Squash> squashPool = new Stack<Squash>();

    void SpawnSquash(Obstacle o)
    {
        Squash s;
        if (squashPool.Count > 0) s = squashPool.Pop();
        else
        {
            var go = new GameObject("Squash");
            s = new Squash { sr = go.AddComponent<SpriteRenderer>() };
            s.sr.sortingOrder = 1;
        }
        s.sr.sprite = o.sr.sprite;
        s.sr.transform.position = o.tr.position;
        s.sr.transform.rotation = o.tr.rotation;
        s.baseScale = o.tr.localScale;
        s.t = 0f;
        s.sr.gameObject.SetActive(true);
        squashes.Add(s);
    }

    void UpdateSquash(float dt)
    {
        UpdateShockwave(dt);
        for (int i = squashes.Count - 1; i >= 0; i--)
        {
            var s = squashes[i];
            s.t += dt;
            float k = Mathf.Clamp01(s.t / 0.3f);
            // 옆으로 퍼지고 위아래로 납작해짐 (그림 기준), 색은 어두워지며 투명해짐
            s.sr.transform.localScale = new Vector3(s.baseScale.x * (1f + 0.45f * k), s.baseScale.y * (1f - 0.75f * k), 1f);
            s.sr.color = new Color(1f - 0.4f * k, 1f - 0.4f * k, 1f - 0.4f * k, 1f - k);
            if (k >= 1f)
            {
                squashes.RemoveAt(i);
                s.sr.gameObject.SetActive(false);
                squashPool.Push(s);
            }
        }
    }

    void ClearSquash()
    {
        foreach (var s in squashes) { s.sr.gameObject.SetActive(false); squashPool.Push(s); }
        squashes.Clear();
        if (shockwave != null) shockwave.gameObject.SetActive(false);
    }

    void UpdateShieldButton()
    {
        bool ready = shieldOn || shieldCdTimer <= 0f;
        if (ready)
        {
            // 파란 링 = 남은 무적 시간
            shieldGauge.fillAmount = shieldEnergy / shieldMaxEnergy;
            shieldGauge.color = new Color32(0x6F, 0xC3, 0xFF, 0xFF);
            shieldCdText.text = "";
        }
        else
        {
            // 쿨타임 중 : 회색 링이 다시 차오르고 가운데에 남은 초
            shieldGauge.fillAmount = 1f - shieldCdTimer / shieldCooldown;
            shieldGauge.color = new Color32(0xD8, 0xDE, 0xE2, 0xFF);
            shieldCdText.text = Mathf.CeilToInt(shieldCdTimer).ToString();
        }

        bool pressed = shieldHold != null && shieldHold.IsHeld;
        // 누르고 있으면 버튼이 살짝 눌려 들어감
        shieldButtonImg.rectTransform.localScale = Vector3.one * (pressed ? 0.93f : 1f);
        if (shieldOn)
        {
            // 켜져 있으면 방패가 숨 쉬듯 반짝이고 버튼이 하늘빛
            shieldButtonImg.color = Color.Lerp(Wood, new Color(0.62f, 0.86f, 1f, 1f), 0.6f);
            shieldIconImg.color = Color.white;
            shieldIconImg.rectTransform.localScale = Vector3.one * (1.06f + Mathf.Sin(Time.time * 14f) * 0.05f);
        }
        else
        {
            shieldButtonImg.color = Wood;
            // 쿨타임 중이면 방패를 흐리게
            shieldIconImg.color = ready ? Color.white : new Color(0.55f, 0.55f, 0.55f, 0.75f);
            shieldIconImg.rectTransform.localScale = Vector3.one;
        }
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

    // ESC 또는 P : 일시정지 / 계속하기
    static bool KeyboardPausePressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        var k = Keyboard.current;
        return k != null && (k.escapeKey.wasPressedThisFrame || k.pKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P);
#endif
    }

    // 스페이스바를 누르고 있는지 (실드 홀드)
    static bool KeyboardShieldHeld()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
#else
        return Input.GetKey(KeyCode.Space);
#endif
    }

    // ================================================================= UI 생성
    void CreateUI()
    {
        // Assets/Resources/BugsBugs/Font.ttf (또는 .otf) 를 넣으면 그 폰트를 씀. 없으면 유니티 기본 폰트
        font = Resources.Load<Font>("BugsBugs/Font");
        customFont = font != null;
        if (font == null)
        {
#if UNITY_2022_2_OR_NEWER
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
        }

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
        BuildPause(root);
        BuildHowTo(root);

        // 받아 온 폰트는 이미 굵은 글꼴이라, 유니티가 억지로 한 번 더 굵게 하면 글자가 뭉개짐 -> 굵게 끄기
        if (customFont)
            foreach (var t in canvasGo.GetComponentsInChildren<Text>(true)) t.fontStyle = FontStyle.Normal;
    }

    void BuildHud(Transform root)
    {
        hud = Stretch(NewRect("HUD", root)).gameObject;

        // 점수 (좌측 상단)
        scoreText = MakeText(hud.transform, "SCORE 0", 30, TextDark, TextAnchor.MiddleLeft);
        Place(scoreText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -14), new Vector2(300, 50));
        scoreText.fontStyle = FontStyle.Bold;

        // 레벨 업 알림 (화면 위쪽 가운데, 2초 뒤 사라짐)
        levelText = MakeText(hud.transform, "", 38, AccentYellow, TextAnchor.MiddleCenter);
        Place(levelText.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -190), new Vector2(460, 110));
        levelText.fontStyle = FontStyle.Bold;
        AddOutline(levelText, new Color(0.25f, 0.15f, 0f, 1f), 2.5f);
        levelText.gameObject.SetActive(false);

        // 일시정지 버튼 (우측 상단). HOME 은 일시정지 화면 안에 있어서 실수로 판이 끝나지 않음
        Image unused;
        var pause = MakeChunkyButton(hud.transform, "Pause", null, iconPause, 0, new Vector2(58, 58), Wood, PauseGame, out unused);
        Place(pause, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -12), new Vector2(58, 58));

        // 사운드 켜기/끄기 (일시정지 버튼 왼쪽, 간격을 넉넉히)
        MakeSoundButton(hud.transform, new Vector2(-84, -12));

        // 조작 안내 (게임 시작 때 화면 아래쪽, 한 번 움직이면 사라짐)
        hintText = MakeText(hud.transform, "DRAG ANYWHERE TO MOVE", 28, Cream, TextAnchor.MiddleCenter);
        Place(hintText.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 250), new Vector2(460, 40));
        AddOutline(hintText, IconBrown, 2.5f);
        hintText.gameObject.SetActive(false);

        // 레이디버그식 드래그 조작 : 화면 전체가 터치 영역 (맨 뒤에 깔아서 버튼이 먼저 눌리게)
        if (useDragControl)
        {
            var pad = Stretch(NewRect("DragPad", hud.transform));
            pad.SetAsFirstSibling();
            pad.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            dragPad = pad.gameObject.AddComponent<DragPad>();
        }
        else
            BuildJoystick();

        // 실드 버튼 (우측 하단, Hold) : 누르고 있는 동안만 무적
        //  도톰한 나무 원형 버튼 + 큰 방패 그림 + 둘레의 파란 게이지 링(남은 양)
        var shieldRt = NewRect("ShieldButton", hud.transform);
        Place(shieldRt, new Vector2(1, 0), new Vector2(0.5f, 0.5f), new Vector2(-72, 104), new Vector2(118, 118));

        var track = NewRect("GaugeTrack", shieldRt); // 게이지 바탕 (어두운 고리)
        Stretch(track);
        var trackImg = track.gameObject.AddComponent<Image>();
        trackImg.sprite = ringSprite;
        trackImg.color = new Color(0f, 0f, 0f, 0.3f);
        trackImg.raycastTarget = false;

        var ring = NewRect("Gauge", shieldRt);       // 남은 게이지 (파란 고리, 위에서부터 줄어듦)
        Stretch(ring);
        shieldGauge = ring.gameObject.AddComponent<Image>();
        shieldGauge.sprite = ringSprite;
        shieldGauge.type = Image.Type.Filled;
        shieldGauge.fillMethod = Image.FillMethod.Radial360;
        shieldGauge.fillOrigin = (int)Image.Origin360.Top;
        shieldGauge.fillClockwise = false;
        shieldGauge.color = new Color32(0x6F, 0xC3, 0xFF, 0xFF);
        shieldGauge.raycastTarget = false;

        var body = NewRect("Body", shieldRt);          // 누르는 부분 (나무 원형 버튼)
        Place(body, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(94, 94));
        shieldButtonImg = body.gameObject.AddComponent<Image>();
        shieldButtonImg.sprite = chunkyCircleSprite;
        shieldButtonImg.color = Wood;
        shieldHold = body.gameObject.AddComponent<HoldButton>();

        var icon = NewRect("ShieldIcon", body);
        Place(icon, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(66, 66));
        shieldIconImg = icon.gameObject.AddComponent<Image>();
        shieldIconImg.sprite = shieldIcon;
        shieldIconImg.raycastTarget = false;

        // 쿨타임 숫자 (방패 위에 크게)
        shieldCdText = MakeText(body, "", 36, Color.white, TextAnchor.MiddleCenter);
        Stretch(shieldCdText.rectTransform);
        shieldCdText.rectTransform.offsetMin = new Vector2(0f, 6f);
        AddOutline(shieldCdText, IconBrown, 2.5f);

        // 조작 안내 : 톡 = 쾅 / 꾹 = 무적 (버튼 바로 아래 작게)
        var how = MakeText(shieldRt, "TAP / HOLD", 15, Cream, TextAnchor.MiddleCenter);
        Place(how.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Vector2(160, 22));
        AddOutline(how, IconBrown, 1.5f);
    }

    // 가상 조이스틱 (useDragControl = false 일 때)
    void BuildJoystick()
    {
        // 조이스틱 터치 영역 : 화면 전체 (투명). 어디를 눌러도 조이스틱이 그 자리로 옴 (다이내믹 조이스틱)
        // HUD 맨 뒤에 깔아서 HOME / SHIELD 버튼이 먼저 눌리게 함
        var zone = NewRect("JoystickZone", hud.transform);
        zone.SetAsFirstSibling();
        zone.anchorMin = Vector2.zero;
        zone.anchorMax = Vector2.one;
        zone.pivot = Vector2.zero;
        zone.offsetMin = Vector2.zero;
        zone.offsetMax = Vector2.zero;
        zone.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);

        // 조이스틱 그림 : 평소엔 좌측 하단에 흐리게, 누르면 그 자리로 와서 진해지고 손가락을 따라감.
        // 손을 떼면 기본 위치로 돌아감 (모바일 게임에서 흔히 쓰는 다이내믹 조이스틱)
        var stickBase = NewRect("Joystick", zone);
        Place(stickBase, new Vector2(0, 0), new Vector2(0.5f, 0.5f), new Vector2(90, 105), new Vector2(130, 130));
        var baseImg = stickBase.gameObject.AddComponent<Image>();
        baseImg.sprite = uiCircle;
        baseImg.color = new Color(1f, 1f, 1f, 0.4f);
        baseImg.raycastTarget = false;
        var knob = NewRect("Knob", stickBase);
        Place(knob, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56, 56));
        var knobImg = knob.gameObject.AddComponent<Image>();
        knobImg.sprite = uiCircle;
        knobImg.color = new Color(0.45f, 0.5f, 0.55f, 0.85f);
        knobImg.raycastTarget = false;
        joystick = zone.gameObject.AddComponent<VirtualJoystick>();
        joystick.stickBase = stickBase;
        joystick.knob = knob;
        joystick.radius = 42f;
        joystick.followFinger = true;
        joystick.hideWhenIdle = true;
        joystick.activeAlpha = 0f;    // 누르고 있어도 안 보이게 (조작은 그대로 됨). 보이게 하려면 1
        joystick.ResetStick(); // 처음부터 숨긴 상태로
    }

    void BuildTitle(Transform root)
    {
        var panel = Stretch(NewRect("TitlePanel", root));
        titlePanel = panel.gameObject;
        var bg = panel.gameObject.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.12f, 0.04f, 0.3f); // 숲이 비쳐 보이게 옅은 그늘만
        panel.gameObject.AddComponent<PointerDownRelay>().onDown = _ => OnTitleTap(); // 화면 터치 = 시작 (처음이면 조작 안내 먼저)


        // 걷는 무당벌레 (로고 위)
        var bug = NewRect("TitleLadybug", panel);
        Place(bug, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 270), new Vector2(130, 130));
        titleBug = bug.gameObject.AddComponent<Image>();
        titleBug.sprite = playerFrames[0];
        titleBug.raycastTarget = false;

        // 로고
        var title = MakeText(panel, "BugsBugs", 88, AccentYellow, TextAnchor.MiddleCenter);
        Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(460, 120));
        AddOutline(title, new Color(0.25f, 0.15f, 0f, 1f), 4f);

        // 최고 점수 : 나무 팻말
        var plank = NewRect("BestPlank", panel);
        Place(plank, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 55), new Vector2(230, 56));
        var plankImg = plank.gameObject.AddComponent<Image>();
        plankImg.sprite = chunkySprite;
        plankImg.type = Image.Type.Sliced;
        plankImg.pixelsPerUnitMultiplier = 100f / 56f;
        plankImg.color = Wood;
        plankImg.raycastTarget = false;
        titleBestText = MakeText(plank, "BEST 0", 26, Cream, TextAnchor.MiddleCenter);
        Stretch(titleBestText.rectTransform);
        titleBestText.rectTransform.offsetMin = new Vector2(0f, 4f);
        AddOutline(titleBestText, IconBrown, 2f);

        tapToStartText = MakeText(panel, "TAP TO START", 40, Cream, TextAnchor.MiddleCenter);
        Place(tapToStartText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -90), new Vector2(460, 60));
        AddOutline(tapToStartText, IconBrown, 3f);

        // 조작법 버튼 (로비 아래쪽). 누르면 나뭇잎 카드에 조작 설명
        Image unused;
        var how = MakeChunkyButton(panel, "HowTo", "HOW TO PLAY", null, 24, new Vector2(230, 58), LeafGreen, () => ShowHowTo(false), out unused);
        Place(how, new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 110), new Vector2(230, 58));

        // 사운드 켜기/끄기 (타이틀에서도)
        MakeSoundButton(panel, new Vector2(-12, -12));
    }

    // ================================================================= 조작 안내 (How to Play)
    // 로비 화면 터치 : 처음 하는 사람이면 조작 안내부터, 본 적 있으면 바로 시작
    void OnTitleTap()
    {
        if (PlayerPrefs.GetInt("BugsBugs_Tutorial", 0) == 0) ShowHowTo(true);
        else StartGame();
    }

    void ShowHowTo(bool startsGame)
    {
        howToStartsGame = startsGame;
        howToButtonLabel.text = startsGame ? "START" : "OK";
        howToPanel.SetActive(true);
    }

    void CloseHowTo()
    {
        howToPanel.SetActive(false);
        PlayerPrefs.SetInt("BugsBugs_Tutorial", 1); // 한 번 봤으면 다음부터는 바로 시작
        PlayerPrefs.Save();
        if (howToStartsGame) StartGame();
    }

    // 나뭇잎 카드에 그림 + 한 줄 설명 4가지 + 확인 버튼
    void BuildHowTo(Transform root)
    {
        var panel = Stretch(NewRect("HowToPanel", root));
        howToPanel = panel.gameObject;
        var bg = panel.gameObject.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.12f, 0.04f, 0.6f);

        var card = NewRect("LeafCard", panel);
        Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(430, 620));
        card.gameObject.AddComponent<Image>().sprite = leafSprite;

        var title = MakeText(card, "HOW TO PLAY", 42, AccentYellow, TextAnchor.MiddleCenter);
        Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -100), new Vector2(400, 56));
        AddOutline(title, new Color(0.25f, 0.15f, 0f, 1f), 3f);

        // 줄마다 : 왼쪽 그림 (작은 글자 꼬리표) + 오른쪽 설명
        HowToRow(card, -175, new[] { playerFrames[0] }, "DRAG", "화면 아무 곳이나 끌어서\n무당벌레를 움직여요");
        HowToRow(card, -255, new[] { shieldIcon }, "TAP", "방패를 톡! 누르면\n주변 벌레를 쾅 날려요");
        HowToRow(card, -335, new[] { shieldIcon }, "HOLD", "방패를 꾹 누르는 동안 무적!\n(최대 2.5초, 이후 15초 대기)");
        HowToRow(card, -415, new[] { itemOrbitSprite, itemShotSprite, pillbugSprite }, "ITEM", "꽃과 콩벌레를 먹으면\n벌레를 물리칠 수 있어요");

        var goal = MakeText(card, "벌레를 피해 오래 버티세요!", 24, AccentYellow, TextAnchor.MiddleCenter);
        Place(goal.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -478), new Vector2(400, 36));
        AddOutline(goal, new Color(0.25f, 0.15f, 0f, 1f), 2f);

        Image unused;
        var ok = MakeChunkyButton(card, "HowToOk", "START", null, 28, new Vector2(200, 60), LeafGreen, CloseHowTo, out unused);
        Place(ok, new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 75), new Vector2(200, 60));
        howToButtonLabel = ok.GetComponentInChildren<Text>();

        howToPanel.SetActive(false);
    }

    void HowToRow(Transform card, float y, Sprite[] icons, string tag, string desc)
    {
        // 그림 (여러 개면 작게 나란히)
        float iconSize = icons.Length > 1 ? 34f : 54f;
        float startX = 95f - (icons.Length - 1) * iconSize * 0.5f;
        for (int i = 0; i < icons.Length; i++)
        {
            var ic = NewRect("Icon", card);
            Place(ic, new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(startX + i * iconSize, y + 8f), new Vector2(iconSize, iconSize));
            var img = ic.gameObject.AddComponent<Image>();
            img.sprite = icons[i];
            img.raycastTarget = false;
        }
        // 꼬리표 (DRAG / TAP / HOLD / ITEM)
        var t = MakeText(card, tag, 17, AccentYellow, TextAnchor.MiddleCenter);
        Place(t.rectTransform, new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(95f, y - 30f), new Vector2(90, 22));
        AddOutline(t, new Color(0.25f, 0.15f, 0f, 1f), 1.5f);
        // 설명
        var d = MakeText(card, desc, 19, Cream, TextAnchor.MiddleLeft);
        Place(d.rectTransform, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(150f, y), new Vector2(260, 64));
        d.lineSpacing = 1.05f;
        AddOutline(d, DeepGreen, 1.5f);
    }

    // 일시정지 화면 : 작은 나뭇잎 카드 + 계속하기 / 홈
    void BuildPause(Transform root)
    {
        var panel = Stretch(NewRect("PausePanel", root));
        pausePanel = panel.gameObject;
        var bg = panel.gameObject.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.12f, 0.04f, 0.55f);

        var card = NewRect("LeafCard", panel);
        Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(320, 396));
        card.gameObject.AddComponent<Image>().sprite = leafSprite;

        var t = MakeText(card, "PAUSED", 50, AccentYellow, TextAnchor.MiddleCenter);
        Place(t.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -80), new Vector2(300, 64));
        AddOutline(t, new Color(0.25f, 0.15f, 0f, 1f), 3f);

        Image unused;
        var resume = MakeChunkyButton(card, "Resume", "PLAY", iconPlay, 26, new Vector2(210, 60), LeafGreen, ResumeGame, out unused);
        Place(resume, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 128), new Vector2(210, 60));
        var home = MakeChunkyButton(card, "Home", "HOME", iconHome, 26, new Vector2(210, 60), Wood, GoTitle, out unused);
        Place(home, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 54), new Vector2(210, 60));

        pausePanel.SetActive(false);
    }

    // 사운드 버튼 (타이틀·게임 화면 둘 다). 아이콘은 soundIcons 에 모아서 같이 바꿈
    void MakeSoundButton(Transform parent, Vector2 pos)
    {
        Image icon;
        var b = MakeChunkyButton(parent, "Sound", null, sound.Muted ? iconSoundOff : iconSoundOn, 0, new Vector2(58, 58), Wood, ToggleSound, out icon);
        Place(b, new Vector2(1, 1), new Vector2(1, 1), pos, new Vector2(58, 58));
        soundIcons.Add(icon);
    }

    void BuildResult(Transform root)
    {
        var panel = Stretch(NewRect("ResultPanel", root));
        resultPanel = panel.gameObject;
        var bg = panel.gameObject.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.12f, 0.04f, 0.55f); // 숲 그늘처럼 어두운 초록

        // 커다란 나뭇잎 카드
        var card = NewRect("LeafCard", panel);
        Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(380, 470));
        var cardImg = card.gameObject.AddComponent<Image>();
        cardImg.sprite = leafSprite;

        // 잎 위에 앉아 있는 무당벌레 (살짝 기울어짐)
        var bug = NewRect("Ladybug", card);
        Place(bug, new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-55, -40), new Vector2(60, 60));
        bug.localRotation = Quaternion.Euler(0f, 0f, -28f);
        var bugImg = bug.gameObject.AddComponent<Image>();
        bugImg.sprite = playerFrames[0];
        bugImg.raycastTarget = false;

        var over = MakeText(card, "GAME OVER", 50, AccentYellow, TextAnchor.MiddleCenter);
        Place(over.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -68), new Vector2(360, 64));
        AddOutline(over, new Color(0.25f, 0.15f, 0f, 1f), 3f);

        resultScoreLabel = MakeText(card, "SCORE", 22, Cream, TextAnchor.MiddleCenter);
        Place(resultScoreLabel.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -138), new Vector2(360, 30));
        AddOutline(resultScoreLabel, DeepGreen, 1.5f);

        // 최고 기록이면 "SCORE" 자리에 대신 나옴 (줄을 따로 쓰지 않아 버튼과 겹치지 않음)
        newBestText = MakeText(card, "NEW BEST!", 26, AccentYellow, TextAnchor.MiddleCenter);
        Place(newBestText.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -136), new Vector2(360, 34));
        AddOutline(newBestText, new Color(0.45f, 0.2f, 0f, 1f), 2f);

        resultScoreText = MakeText(card, "0", 60, Color.white, TextAnchor.MiddleCenter);
        Place(resultScoreText.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -166), new Vector2(360, 66));
        AddOutline(resultScoreText, DeepGreen, 3f);

        resultBestText = MakeText(card, "BEST 0", 22, Cream, TextAnchor.MiddleCenter);
        Place(resultBestText.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -234), new Vector2(360, 30));
        AddOutline(resultBestText, DeepGreen, 1.5f);


        Image unused;
        var retry = MakeChunkyButton(card, "Retry", "RETRY", iconRetry, 26, new Vector2(230, 60), LeafGreen, StartGame, out unused);
        Place(retry, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 128), new Vector2(230, 60));

        var home = MakeChunkyButton(card, "Home", "HOME", iconHome, 26, new Vector2(230, 60), Wood, GoTitle, out unused);
        Place(home, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 58), new Vector2(230, 60));
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
        rt.gameObject.AddComponent<LetterSpacing>(); // 자간 (외곽선보다 먼저 붙여야 함)
        return t;
    }

    static void AddOutline(Text t, Color c, float dist)
    {
        var o = t.gameObject.AddComponent<SoftOutline>(); // 매끈한 외곽선 (기본 Outline 은 모서리가 깨짐)
        o.effectColor = c;
        o.distance = dist;
    }

    // 도톰한 숲 버튼 (나무색 / 잎색). label 이 null 이면 아이콘만 가운데, 있으면 아이콘 왼쪽 + 글자
    RectTransform MakeChunkyButton(Transform parent, string name, string label, Sprite icon, int fontSize, Vector2 size, Color color,
        UnityEngine.Events.UnityAction onClick, out Image iconImg)
    {
        var rt = NewRect(name + "Button", parent);
        rt.sizeDelta = size;
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = chunkySprite;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 100f / size.y; // 9-slice 모서리 크기를 버튼 높이의 약 30% 로
        img.color = color;
        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var cb = btn.colors;
        cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        btn.colors = cb;
        btn.onClick.AddListener(() => sound.Play(BugsSound.Sfx.Click));
        btn.onClick.AddListener(onClick);

        iconImg = null;
        float h = size.y;
        if (icon != null)
        {
            var ic = NewRect("Icon", rt);
            float isz = h * (label == null ? 0.6f : 0.52f);
            if (label == null) Place(ic, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(isz, isz));
            else Place(ic, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(h * 0.55f, 2), new Vector2(isz, isz));
            iconImg = ic.gameObject.AddComponent<Image>();
            iconImg.sprite = icon;
            iconImg.color = IconBrown;
            iconImg.raycastTarget = false;
        }
        if (label != null)
        {
            var t = MakeText(rt, label, fontSize, Cream, TextAnchor.MiddleCenter);
            Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(icon != null ? h * 0.5f : 0f, 4f);
            AddOutline(t, IconBrown, 2f);
        }
        return rt;
    }

    void ToggleSound()
    {
        sound.Muted = !sound.Muted;
        foreach (var icon in soundIcons) icon.sprite = sound.Muted ? iconSoundOff : iconSoundOn;
    }
}
