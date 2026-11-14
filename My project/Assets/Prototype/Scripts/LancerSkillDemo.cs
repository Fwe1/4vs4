using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 프로토타입: 오리지널 임시 창술사의 평타와 스킬 3종 모션, 연출.
// 규칙: 캐릭터는 스킬 시전이나 스킬 모션 중이 아니면 항상 평타를 친다.
// 자세는 핵심 자세(키) 사이를 이어 만들고, 팔다리는 PuppetRig가 손발 위치에 맞춰 굽힌다.
// 실제 효과(피해, 물리 피해 증가, 상태이상 해제, 실드, 기절)는 나중에 상태 효과 시스템으로 옮긴다.
// 여기서는 화면에 보이는 것만 흉내 낸다.
public class LancerSkillDemo : MonoBehaviour
{
    public bool autoLoop = true;          // 스킬을 자동으로 번갈아 쓴다
    public bool stepped = true;           // 도트 애니메이션처럼 끊어서 재생
    public float stepFps = 12f;
    public int basicsBetweenSkills = 2;   // 자동 사용 시 스킬 사이에 치는 평타 횟수

    public int skill = -1;                // -1이면 평타 중
    public float t, basicT, clock;
    public bool alliesDebuffed = true;

    public const int Basic = 3;           // SimulateTo에서 평타를 가리키는 번호
    public const int Intro = 4;           // SimulateTo에서 시작 장면(시작 구도 → 전진)을 가리키는 번호

    static readonly float[] Dur = { 0.9f, 1.4f, 1.6f };
    static readonly float[] HitTime = { 0.34f, 0.42f, 0.74f };
    static readonly float[] HitStop = { 0.07f, 0.05f, 0.1f }; // 타격 순간 잠깐 멈추는 시간
    static readonly string[] Names = { "1 올려치기 (물피증)", "2 창 꽂기 (해제+실드)", "3 내려찍기 (기절)" };
    const float DebuffTime = 5f, ShieldTime = 4f, StunTime = 2.5f;
    const float BasicDur = 0.75f, BasicHit = 0.26f;                    // 창술사 평타 한 번의 길이, 맞는 순간
    const float DummyDur = 1f, DummyFire = 0.4f, OrbFlight = 0.22f;    // 임시 캐릭터 평타 주기, 탄 발사 순간, 탄 비행 시간
    const float BlendIn = 0.08f;                                       // 평타를 끊고 스킬로 넘어갈 때 자세를 잇는 시간
    const float IntroHold = 1f, RunSpeed = 2.4f;                       // 시작 구도로 서 있는 시간, 전진 속도
    const float StartFront = 2.8f, StartGap = 0.95f;                   // 시작 때 맨 앞 캐릭터와 중앙선의 거리, 앞뒤 간격
    const float SlamRadius = 2.4f;                                     // 3스킬 기절 범위

    struct Key
    {
        public float t;
        public PuppetPose p;
        public int ease; // 0 부드럽게, 1 가속(타격 직전에 빨라짐), 2 감속(뻗은 뒤 멈춤)
        public Key(float t, PuppetPose p, int ease = 0) { this.t = t; this.p = p; this.ease = ease; }
    }

    // 무기 없는 임시 캐릭터(아군, 적). 평타로 탄을 쏜다.
    class Dummy
    {
        public PuppetRig rig, target;
        public bool enemy;
        public int index;
        public float atk;          // 평타 주기 안에서의 시간
        public float travel;       // 시작 위치에서 교전 위치까지 달리는 거리
        public float orbT = -1f;   // 날아가는 탄의 시간 (-1이면 없음)
        public Vector3 orbFrom;
        public Transform orb;
    }

    PuppetRig lancer;
    PuppetRig[] allies, enemies;
    Dummy[] dummies;
    Key[][] clips;
    Key[] basicClip;
    PuppetPose ready, lastPose, blendFrom;
    Transform cam, vfx, slash, spark, jabSpark, ring, plantRing, enemyMark;
    Transform[] shields, allyMarks, stars, dust;
    Material slashMat, sparkMat, jabMat, ringMat, plantMat, dustMat;
    Vector3 camPos, slamPos;
    float debuffTimer, shieldTimer, stunTimer, hitStop, slump = 1f;
    float sinceHit1 = 99f, sinceHit3 = 99f, sinceBasic = 99f;
    float tickAcc;
    float introT, lancerTravel, maxTravel;
    int next, basicCount;
    int phase = 2; // 0 시작 구도로 대기, 1 전진, 2 교전
    bool forceTick = true, pendingSlam;
    bool[] inSlam; // 3스킬 범위 안에 있던 적

    void Awake() { Init(); }

    public void Init()
    {
        lancer = GameObject.Find("Lancer").GetComponent<PuppetRig>();
        allies = FindRigs("Ally_");
        enemies = FindRigs("Enemy_");
        lancer.Init();
        var list = new List<Dummy>();
        for (int i = 0; i < allies.Length; i++)
        {
            allies[i].Init();
            list.Add(new Dummy { rig = allies[i], target = enemies.Length > 0 ? enemies[0] : null, index = i, travel = Travel(allies[i], i + 1) });
        }
        for (int i = 0; i < enemies.Length; i++)
        {
            enemies[i].Init();
            list.Add(new Dummy { rig = enemies[i], target = lancer, enemy = true, index = i, travel = Travel(enemies[i], i) });
        }
        dummies = list.ToArray();
        lancerTravel = Travel(lancer, 0);
        maxTravel = lancerTravel;
        foreach (Dummy d in dummies) maxTravel = Mathf.Max(maxTravel, d.travel);
        inSlam = new bool[enemies.Length];
        cam = GameObject.Find("Main Camera").transform;
        camPos = cam.position;
        BuildClips();
        ResetState(true);
        BuildVfx();
        ApplyRigs(0f);
        UpdateVfx();
    }

    PuppetRig[] FindRigs(string prefix)
    {
        var list = new List<PuppetRig>();
        foreach (GameObject g in gameObject.scene.GetRootGameObjects())
        {
            if (!g.name.StartsWith(prefix)) continue;
            PuppetRig r = g.GetComponent<PuppetRig>();
            if (r != null) list.Add(r);
        }
        list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return list.ToArray();
    }

    // 시작 때는 앞에서 row번째 캐릭터가 중앙선에서 StartFront + StartGap * row만큼 떨어져 선다.
    // 씬에 놓인 자리는 교전 위치이므로, 그 차이만큼 달려와야 한다.
    static float Travel(PuppetRig r, int row) => Mathf.Max(0f, StartFront + StartGap * row - Mathf.Abs(r.BasePos.x));

    // 교전 위치까지 남은 거리
    float Back(float travel) => phase >= 2 ? 0f : Mathf.Max(0f, travel - RunSpeed * Mathf.Max(0f, introT - IntroHold));

    // ---------- 핵심 자세 ----------
    static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

    // 달리기: 발을 번갈아 내딛고 몸을 앞으로 숙인다. dist는 지금까지 달린 거리.
    static void Run(ref PuppetPose p, float dist, bool spear)
    {
        float ph = dist * (Mathf.PI * 2f / 1.3f); // 1.3m마다 한 걸음 주기
        float s = Mathf.Sin(ph), c = Mathf.Cos(ph);
        p.hipY = 0.8f + 0.03f * Mathf.Cos(ph * 2f);
        p.hipZ = 0.04f;
        p.hipPitch = 12f;
        p.chestPitch = 8f;
        p.headPitch = -12f;
        p.hipYaw += 6f * c;
        p.chestYaw -= 10f * c;
        p.footL = V(-0.11f, 0.07f + 0.22f * Mathf.Max(0f, -s), 0.3f * c);
        p.footR = V(0.11f, 0.07f + 0.22f * Mathf.Max(0f, s), -0.3f * c);
        p.footPitchL = 30f * Mathf.Max(0f, -s);
        p.footPitchR = 30f * Mathf.Max(0f, s);
        if (spear)
        {
            p.grip = V(0.14f, 0.98f + 0.02f * Mathf.Cos(ph * 2f), 0.16f);
            p.elev = 14f;
            p.azim = -12f;
        }
        else
        {
            p.handL = V(-0.24f, -0.06f, 0.08f - 0.2f * c);
            p.handR = V(0.24f, -0.06f, 0.08f + 0.2f * c);
        }
    }

    // 골반(높이, 앞뒤, 숙임, 비틀기), 상체(숙임, 비틀기), 머리(숙임, 돌림),
    // 왼발(앞뒤, 높이), 오른발(앞뒤, 높이), 창(쥔 위치, 올림각, 좌우각, 쥐는 자리), 오른발 뒤꿈치 들기
    static PuppetPose Mk(float hipY, float hipZ, float hipP, float hipYw, float chP, float chYw, float hdP, float hdYw,
        float fLz, float fLy, float fRz, float fRy, Vector3 grip, float elev, float azim, float slide = -0.1f, float toeR = -1f)
    {
        PuppetPose p = PuppetPose.Stand();
        p.hipY = hipY; p.hipZ = hipZ; p.hipPitch = hipP; p.hipYaw = hipYw;
        p.chestPitch = chP; p.chestYaw = chYw; p.headPitch = hdP; p.headYaw = hdYw;
        p.footL = V(-0.11f, fLy, fLz);
        p.footR = V(0.11f, fRy, fRz);
        p.footPitchL = Mathf.Clamp((fLy - 0.07f) * 200f, 0f, 55f);
        p.footPitchR = toeR >= 0f ? toeR : Mathf.Clamp((fRy - 0.07f) * 200f, 0f, 55f);
        p.grip = grip; p.elev = elev; p.azim = azim; p.slide = slide;
        return p;
    }

    void BuildClips()
    {
        // 준비 자세: 왼발과 왼어깨를 앞에 두고 창을 비스듬히 든다
        ready = Mk(0.8f, 0f, 4, 20, 6, 20, 0, -35, 0.2f, 0.07f, -0.18f, 0.07f, V(0.1f, 1.02f, 0.22f), 18, -12);
        PuppetPose R = ready;

        // 평타 찌르기: 창을 뒤로 당김 → 왼발을 내디디며 찌름 → 잠깐 버팀 → 복귀
        basicClip = new[]
        {
            new Key(0f, R),
            new Key(0.16f, Mk(0.76f, -0.06f, 6, 30, 8, 35, -4, -55, 0.2f, 0.07f, -0.2f, 0.07f, V(0.2f, 1f, 0.02f), 4, -14), 2),
            new Key(0.26f, Mk(0.76f, 0.24f, 8, -10, 10, -25, -6, 22, 0.55f, 0.07f, -0.22f, 0.15f, V(0.01f, 1.05f, 0.42f), 3, -20, -0.1f, 40), 1),
            new Key(0.36f, Mk(0.77f, 0.25f, 8, -10, 9, -24, -6, 22, 0.55f, 0.07f, -0.22f, 0.15f, V(0f, 1.05f, 0.45f), 3, -20, -0.1f, 40), 2),
            new Key(0.6f, R),
            new Key(0.75f, R),
        };

        clips = new Key[3][];

        // 1 올려치기: 몸을 낮추고 감았다가 → 왼발을 내디디며 아래에서 위로 쳐 올림 → 끝까지 뻗음 → 복귀
        clips[0] = new[]
        {
            new Key(0f, R),
            new Key(0.2f, Mk(0.68f, -0.08f, 12, 35, 18, 30, -10, -50, 0.22f, 0.07f, -0.22f, 0.07f, V(0.22f, 0.72f, 0f), -22, -15), 2),
            new Key(0.34f, Mk(0.8f, 0.28f, -4, -5, -8, -15, -15, 10, 0.6f, 0.07f, -0.2f, 0.16f, V(0f, 1.15f, 0.42f), 72, 0, -0.1f, 45), 1),
            new Key(0.44f, Mk(0.84f, 0.3f, -6, -10, -14, -25, -25, 20, 0.6f, 0.07f, -0.18f, 0.16f, V(-0.02f, 1.32f, 0.34f), 105, 0, -0.1f, 45), 2),
            new Key(0.62f, Mk(0.82f, 0.22f, -2, 0, -6, -5, -10, -5, 0.52f, 0.07f, -0.18f, 0.1f, V(0.03f, 1.22f, 0.34f), 84, -4), 0),
            new Key(0.9f, R),
        };

        // 2 창 꽂기: 창을 세워 들어 올림 → 무릎을 굽히며 자루 끝을 땅에 꽂음 → 고개를 들어 유지 → 복귀
        clips[1] = new[]
        {
            new Key(0f, R),
            new Key(0.3f, Mk(0.89f, 0f, -3, 5, -6, 5, -15, -5, 0.1f, 0.07f, -0.08f, 0.07f, V(0.03f, 1.3f, 0.3f), 90, 0, 0.05f), 2),
            new Key(0.42f, Mk(0.7f, 0.04f, 6, 5, 14, 5, 15, -5, 0.14f, 0.07f, -0.12f, 0.07f, V(0.03f, 1f, 0.3f), 90, 0, 0.05f), 1),
            new Key(0.55f, Mk(0.72f, 0.04f, 5, 5, 10, 5, 5, -5, 0.14f, 0.07f, -0.12f, 0.07f, V(0.03f, 1f, 0.3f), 90, 0, 0.05f), 2),
            new Key(1.05f, Mk(0.76f, 0.03f, 2, 5, 2, 5, -18, -5, 0.14f, 0.07f, -0.12f, 0.07f, V(0.03f, 1f, 0.3f), 90, 0, 0.05f), 0),
            new Key(1.4f, R),
        };

        // 3 내려찍기: 웅크림 → 도약 → 공중에서 몸을 젖히고 창을 머리 위로 → 착지하며 땅에 내리꽂음 → 버팀 → 뒤로 뛰어 복귀
        clips[2] = new[]
        {
            new Key(0f, R),
            new Key(0.2f, Mk(0.58f, -0.05f, 15, 30, 22, 25, -25, -45, 0.2f, 0.07f, -0.2f, 0.07f, V(0.22f, 0.78f, -0.02f), 12, -15), 2),
            new Key(0.3f, Mk(0.84f, 0f, -5, 10, -10, 10, -20, -15, 0f, 0.1f, -0.15f, 0.12f, V(0.08f, 1.32f, 0.1f), 55, -5), 1),
            new Key(0.48f, Mk(0.8f, 0f, -15, 0, -18, 0, 20, 0, 0.25f, 0.42f, -0.3f, 0.4f, V(0.15f, 1.42f, -0.08f), -35, 0, 0.3f), 2),
            new Key(0.74f, Mk(0.5f, 0.05f, 12, 10, 30, 5, 10, -10, 0.38f, 0.07f, -0.38f, 0.16f, V(0.05f, 0.823f, 0.281f), -62, 0, 0.45f, 45), 1),
            new Key(0.9f, Mk(0.54f, 0.05f, 10, 10, 26, 5, 0, -10, 0.38f, 0.07f, -0.38f, 0.16f, V(0.05f, 0.823f, 0.281f), -62, 0, 0.45f, 45), 2),
            new Key(1.15f, Mk(0.66f, 0.02f, 6, 15, 14, 10, -5, -20, 0.34f, 0.07f, -0.3f, 0.07f, V(0.1f, 0.95f, 0.22f), -18, -6, 0.2f), 0),
            new Key(1.38f, Mk(0.82f, 0f, 2, 18, 4, 15, 0, -30, 0.15f, 0.2f, -0.12f, 0.22f, V(0.1f, 1.05f, 0.2f), 10, -10, 0f), 0),
            new Key(1.6f, R),
        };
    }

    static PuppetPose Eval(Key[] c, float x)
    {
        if (x <= c[0].t) return c[0].p;
        for (int i = 1; i < c.Length; i++)
        {
            if (x > c[i].t) continue;
            float u = (x - c[i - 1].t) / (c[i].t - c[i - 1].t);
            if (c[i].ease == 1) u = u * u;
            else if (c[i].ease == 2) u = 1f - (1f - u) * (1f - u);
            else u = Smooth(u);
            return PuppetPose.Lerp(c[i - 1].p, c[i].p, u);
        }
        return c[c.Length - 1].p;
    }

    // x는 스킬 진행 시간. 스킬 중이 아니면 평타 자세를 돌려준다.
    PuppetPose LancerPose(float x)
    {
        PuppetPose p;
        if (skill < 0 && phase < 2)
        {
            // 시작 구도: 제자리에서 기다리다가 상대를 향해 달려간다
            p = ready;
            float back = Back(lancerTravel);
            if (phase == 1 && back > 0f) Run(ref p, lancerTravel - back, true);
            p.rootZ = -back;
        }
        else if (skill >= 0)
        {
            p = Eval(clips[skill], x);
            if (skill == 2)
            {
                // 도약: 포물선으로 0.9m 전진, 끝난 뒤 작은 뜀으로 제자리 복귀
                float u = Mathf.Clamp01((x - 0.26f) / 0.48f);
                p.rootY = 1.25f * 4f * u * (1f - u);
                p.rootZ = 0.9f * u;
                if (x > 1.15f)
                {
                    float v = Mathf.Clamp01((x - 1.15f) / 0.45f);
                    p.rootZ = 0.9f * (1f - Smooth(v));
                    p.rootY = 0.3f * 4f * v * (1f - v);
                }
            }
            if (x < BlendIn) p = PuppetPose.Lerp(blendFrom, p, Smooth(x / BlendIn));
        }
        else p = Eval(basicClip, basicT);

        float b = Mathf.Sin(clock * 2.6f) * 0.3f; // 숨쉬기
        p.hipY += 0.012f * b;
        p.chestPitch += 1.5f * b;
        p.grip.y += 0.012f * b;
        p.elev += 1.5f * b;
        return p;
    }

    bool CanAttack(Dummy d)
    {
        if (phase < 2) return false; // 전진 중에는 평타를 치지 않는다
        if (!d.enemy) return true;
        // 기절했거나 띄워진 동안에는 평타를 못 친다
        bool slammed = inSlam[d.index] && (stunTimer > 0f || sinceHit3 < 0.5f);
        return !(slammed || (d.index == 0 && sinceHit1 < 0.7f));
    }

    PuppetPose DummyPose(Dummy d)
    {
        PuppetPose p = PuppetPose.Stand();
        float br = Mathf.Sin(clock * 2.2f + d.index * 1.9f + (d.enemy ? 1.3f : 0f));
        p.hipY += 0.006f * br - 0.006f;
        p.chestPitch = 2f + 1.2f * br;
        if (phase < 2)
        {
            float back = Back(d.travel);
            if (phase == 1 && back > 0f) Run(ref p, d.travel - back, false);
            p.rootZ = -back;
            return p;
        }
        if (!d.enemy)
        {
            // 상태이상이 걸려 있으면 축 처진다
            p.hipY -= 0.03f * slump;
            p.chestPitch += 8f * slump;
            p.headPitch += 12f * slump;
        }
        if (CanAttack(d)) Jab(ref p, d.atk);
        if (d.enemy) React(ref p, d.index);
        return p;
    }

    // 임시 캐릭터의 평타: 오른손을 뒤로 당겼다가 앞으로 내질러 탄을 쏜다.
    static void Jab(ref PuppetPose p, float time)
    {
        float u = time / DummyDur;
        float a, b; // 당기기, 내지르기
        if (u < 0.3f) { a = Smooth(u / 0.3f); b = 0f; }
        else if (u < 0.4f) { float k = (u - 0.3f) / 0.1f; a = 1f - k; b = k * k; }
        else if (u < 0.52f) { a = 0f; b = 1f; }
        else { a = 0f; b = 1f - Smooth((u - 0.52f) / 0.48f); }
        float twist = 22f * a - 28f * b, hipTwist = 8f * a - 10f * b;
        p.hipY -= 0.03f * a + 0.04f * b;
        p.hipZ += -0.03f * a + 0.09f * b;
        p.hipYaw += hipTwist;
        p.chestYaw += twist;
        p.chestPitch += 4f * a + 8f * b;
        p.headYaw -= (twist + hipTwist) * 0.8f; // 얼굴은 계속 앞을 본다
        p.footL.z += 0.1f * b;
        Vector3 hr = p.handR, hl = p.handL;
        p.handR = hr + (V(0.28f, 0.05f, -0.12f) - hr) * a + (V(0.08f, 0.2f, 0.46f) - hr) * b;
        p.handL = hl + (V(-0.22f, 0f, 0.18f) - hl) * (0.6f * (a + b));
    }

    // 적이 맞았을 때의 반응
    void React(ref PuppetPose p, int i)
    {
        // 평타에 맞은 앞쪽 적: 살짝 움찔
        if (i == 0 && sinceBasic < 0.18f)
        {
            float f = 1f - sinceBasic / 0.18f;
            p.chestPitch -= 8f * f;
            p.rootZ -= 0.04f * f;
        }

        // 1스킬에 맞은 앞쪽 적: 살짝 떠오르며 뒤로 젖혀진다
        if (i == 0 && sinceHit1 < 0.7f)
        {
            float u = sinceHit1 / 0.7f;
            float f = 1f - Smooth(u);
            float air = Mathf.Clamp01(sinceHit1 / 0.42f);
            p.rootY += 0.38f * 4f * air * (1f - air);
            p.rootZ -= 0.2f * f * Mathf.Clamp01(sinceHit1 / 0.08f);
            p.hipPitch -= 10f * f;
            p.chestPitch -= 30f * f;
            p.headPitch -= 18f * f;
            p.handL += V(-0.06f, 0.3f, 0.22f) * f;
            p.handR += V(0.06f, 0.3f, 0.22f) * f;
        }

        // 3스킬 충격: 범위 안의 적은 들썩인 뒤 기절해서 비틀거린다
        if (!inSlam[i]) return;
        if (sinceHit3 < 0.5f)
        {
            float u = sinceHit3 / 0.5f;
            float f = 1f - Smooth(u);
            float air = Mathf.Clamp01(sinceHit3 / 0.3f);
            p.rootY += 0.22f * 4f * air * (1f - air);
            p.chestPitch -= 22f * f;
            p.headPitch -= 12f * f;
            p.handL += V(-0.05f, 0.25f, 0.18f) * f;
            p.handR += V(0.05f, 0.25f, 0.18f) * f;
        }
        if (stunTimer > 0f)
        {
            float w = Mathf.Clamp01((sinceHit3 - 0.25f) / 0.3f) * Mathf.Clamp01(stunTimer / 0.3f);
            float s = Mathf.Sin(clock * 4f + i * 2f);
            p.hipY -= 0.08f * w;
            p.hipYaw += 6f * w * Mathf.Sin(clock * 4f + i * 2f + 1f);
            p.chestPitch += 16f * w;
            p.chestYaw += 14f * w * s;
            p.headPitch += 26f * w;
            p.headYaw += 10f * w * Mathf.Sin(clock * 3f + i);
            p.handL += V(0f, 0f, 0.14f) * w;
            p.handR += V(0f, 0f, 0.14f) * w;
        }
    }

    // ---------- 진행 ----------
    void Update() { Step(Time.deltaTime); }

    public void Step(float dt)
    {
        clock += dt;
        debuffTimer = Mathf.Max(0f, debuffTimer - dt);
        shieldTimer = Mathf.Max(0f, shieldTimer - dt);
        stunTimer = Mathf.Max(0f, stunTimer - dt);
        sinceHit1 += dt;
        sinceHit3 += dt;
        sinceBasic += dt;

        if (phase < 2)
        {
            // 시작: 잠깐 서 있다가 전원이 전진하고, 다 도착하면 교전을 시작한다
            introT += dt;
            phase = introT < IntroHold ? 0 : 1;
            if (introT >= IntroHold + maxTravel / RunSpeed) { phase = 2; basicT = 0f; basicCount = 0; forceTick = true; }
        }
        else if (hitStop > 0f) hitStop -= dt;
        else if (skill >= 0)
        {
            float prev = t;
            t += dt;
            float hit = HitTime[skill];
            if (prev < hit && t >= hit) { t = hit; OnHit(skill); }
            else if (t >= Dur[skill]) { skill = -1; t = 0f; basicT = 0f; basicCount = 0; forceTick = true; }
        }
        else
        {
            // 스킬을 쓰고 있지 않으면 쉬지 않고 평타를 친다
            float prev = basicT;
            basicT += dt;
            if (prev < BasicHit && basicT >= BasicHit) { sinceBasic = 0f; forceTick = true; }
            if (basicT >= BasicDur)
            {
                basicT -= BasicDur;
                basicCount++;
                if (autoLoop && basicCount >= basicsBetweenSkills) { Begin(next); next = (next + 1) % 3; }
            }
        }

        foreach (Dummy d in dummies)
        {
            if (CanAttack(d))
            {
                float prev = d.atk;
                d.atk += dt;
                if (prev < DummyFire && d.atk >= DummyFire && d.target != null) { d.orbT = 0f; d.orbFrom = d.rig.HandRPos; }
                if (d.atk >= DummyDur) d.atk -= DummyDur;
            }
            else if (phase >= 2) d.atk = 0f;
            if (d.orbT >= 0f)
            {
                d.orbT += dt;
                if (d.orbT >= OrbFlight) d.orbT = -1f;
            }
        }

        tickAcc += dt;
        if (forceTick || !stepped || tickAcc >= 1f / stepFps)
        {
            ApplyRigs(tickAcc);
            tickAcc = 0f;
            forceTick = false;
        }
        UpdateVfx();
    }

    // 스킬 시작. 평타 중이었다면 평타를 끊고 바로 나간다.
    public void Begin(int s)
    {
        phase = 2; // 시작 장면 중에 누르면 바로 교전으로 넘어간다
        blendFrom = lastPose;
        skill = s;
        t = 0f;
        forceTick = true;
        if (s == 0) alliesDebuffed = true; // 데모 반복용: 아군에게 다시 상태이상이 걸린 것으로 둔다
    }

    void OnHit(int s)
    {
        hitStop = HitStop[s];
        forceTick = true;
        if (s == 0) { debuffTimer = DebuffTime; sinceHit1 = 0f; }
        if (s == 1) { alliesDebuffed = false; shieldTimer = ShieldTime; }
        if (s == 2) { stunTimer = StunTime; sinceHit3 = 0f; pendingSlam = true; }
    }

    void ApplyRigs(float dt)
    {
        slump = Mathf.MoveTowards(slump, alliesDebuffed ? 1f : 0f, dt * 4f);
        PuppetPose lp = LancerPose(t);
        lancer.Apply(lp, dt);
        lastPose = lp;
        foreach (Dummy d in dummies) d.rig.Apply(DummyPose(d), dt);
        if (pendingSlam)
        {
            slamPos = lancer.SpearPoint(1.45f);
            slamPos.y = 0.05f;
            for (int i = 0; i < enemies.Length; i++)
            {
                Vector3 e = enemies[i].BasePos - slamPos;
                e.y = 0f;
                inSlam[i] = e.magnitude < SlamRadius;
            }
            pendingSlam = false;
        }
    }

    // intro가 true면 시작 구도(양 팀이 떨어져 선 상태)부터, false면 바로 교전 상태로 되돌린다.
    void ResetState(bool intro = false)
    {
        phase = intro ? 0 : 2;
        introT = 0f;
        skill = -1;
        t = 0f;
        basicT = 0f;
        basicCount = 0;
        next = 0;
        clock = 0f;
        tickAcc = 0f;
        hitStop = 0f;
        debuffTimer = shieldTimer = stunTimer = 0f;
        sinceHit1 = sinceHit3 = sinceBasic = 99f;
        alliesDebuffed = !intro;
        slump = intro ? 0f : 1f;
        for (int i = 0; i < inSlam.Length; i++) inSlam[i] = false;
        forceTick = true;
        pendingSlam = false;
        lastPose = ready;
        blendFrom = ready;
        lancer.ResetSecondary();
        for (int k = 0; k < dummies.Length; k++)
        {
            dummies[k].atk = (k * 0.37f) % DummyDur; // 서로 엇갈리게 친다
            dummies[k].orbT = -1f;
            dummies[k].rig.ResetSecondary();
        }
    }

    // 에디터에서 특정 순간의 장면을 찍을 때 쓴다. s가 Basic(3)이면 평타의 x초 시점.
    // s가 Intro(4)면 게임 시작 후 x초 시점.
    public void SimulateTo(int s, float x)
    {
        ResetState(s == Intro);
        ApplyRigs(0f);
        bool keepAuto = autoLoop;
        int guard = 0;
        if (s == Intro)
        {
            for (float el = 0f; el < x; el += 1f / 120f) Step(1f / 120f);
        }
        else if (s == Basic)
        {
            autoLoop = false;
            while (basicT < x && basicCount == 0 && guard++ < 5000) Step(1f / 120f);
        }
        else
        {
            Begin(s);
            while (skill == s && t < x && guard++ < 5000) Step(1f / 120f);
        }
        autoLoop = keepAuto;
        ApplyRigs(Mathf.Max(tickAcc, 0.001f));
        tickAcc = 0f;
        UpdateVfx();
    }

    // 시작 구도로 되돌린다.
    public void ResetPose()
    {
        ResetState(true);
        ApplyRigs(0f);
        UpdateVfx();
    }

    void OnGUI()
    {
        if (!Application.isPlaying) return;
        GUILayout.BeginArea(new Rect(10, 10, 250, 240));
        if (GUILayout.Button("처음부터 (시작 구도)")) ResetPose();
        for (int i = 0; i < 3; i++)
            if (GUILayout.Button(Names[i])) { autoLoop = false; Begin(i); }
        autoLoop = GUILayout.Toggle(autoLoop, " 스킬 자동 사용");
        stepped = GUILayout.Toggle(stepped, " 끊어 재생 (12fps)");
        GUILayout.EndArea();
    }

    // ---------- 연출 ----------
    void UpdateVfx()
    {
        // 평타: 창끝 섬광, 임시 캐릭터들의 탄
        bool jOn = sinceBasic < 0.12f;
        jabSpark.gameObject.SetActive(jOn);
        if (jOn)
        {
            float u = sinceBasic / 0.12f;
            jabSpark.position = lancer.SpearPoint(1.45f);
            jabSpark.localScale = Vector3.one * Mathf.Lerp(0.18f, 0.55f, u);
            SetA(jabMat, 1f - u);
        }
        foreach (Dummy d in dummies)
        {
            bool on = d.orbT >= 0f && d.target != null;
            d.orb.gameObject.SetActive(on);
            if (on) d.orb.position = Vector3.Lerp(d.orbFrom, d.target.ChestPos, d.orbT / OrbFlight);
        }

        // 1스킬: 창끝이 지나간 궤적 + 타격 섬광
        bool sOn = skill == 0 && t >= HitTime[0] && t < 0.56f;
        slash.gameObject.SetActive(sOn);
        if (sOn) SetA(slashMat, 0.9f * (1f - (t - HitTime[0]) / 0.22f));

        bool kOn = sinceHit1 < 0.16f && enemies.Length > 0;
        spark.gameObject.SetActive(kOn);
        if (kOn)
        {
            float u = sinceHit1 / 0.16f;
            spark.position = enemies[0].ChestPos;
            spark.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, u);
            SetA(sparkMat, 1f - u);
        }

        // 2스킬: 자루 끝을 꽂은 자리에서 퍼지는 고리
        bool pOn = skill == 1 && t >= HitTime[1] && t < HitTime[1] + 0.55f;
        plantRing.gameObject.SetActive(pOn);
        if (pOn)
        {
            float u = (t - HitTime[1]) / 0.55f;
            Vector3 b = lancer.SpearPoint(-0.95f);
            b.y = 0.04f;
            plantRing.position = b;
            plantRing.localScale = Vector3.one * Mathf.Lerp(0.3f, 3.2f, u);
            SetA(plantMat, 0.7f * (1f - u));
        }

        // 3스킬: 충격파 고리 + 먼지 + 화면 흔들림
        bool rOn = sinceHit3 < 0.5f;
        ring.gameObject.SetActive(rOn);
        if (rOn)
        {
            float u = sinceHit3 / 0.5f;
            ring.position = slamPos;
            ring.localScale = Vector3.one * Mathf.Lerp(0.4f, SlamRadius * 2f, u);
            SetA(ringMat, 0.8f * (1f - u));
        }
        for (int k = 0; k < dust.Length; k++)
        {
            dust[k].gameObject.SetActive(rOn);
            if (!rOn) continue;
            float u = sinceHit3 / 0.5f;
            float e = 1f - (1f - u) * (1f - u);
            float ang = k * Mathf.PI * 2f / dust.Length + 0.4f;
            dust[k].position = slamPos + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (0.3f + 1.1f * e) + Vector3.up * (0.12f + 0.3f * u);
            dust[k].localScale = Vector3.one * Mathf.Lerp(0.42f, 0.2f, u);
        }
        if (rOn) SetA(dustMat, 0.75f * (1f - sinceHit3 / 0.5f));

        float amp = 0f;
        if (sinceHit3 < 0.4f) amp = 0.14f * (1f - sinceHit3 / 0.4f);
        if (sinceHit1 < 0.15f) amp = Mathf.Max(amp, 0.05f * (1f - sinceHit1 / 0.15f));
        cam.position = camPos + new Vector3(Mathf.Sin(clock * 95f) * amp, Mathf.Cos(clock * 73f) * amp, 0f);

        // 상태 표시: 아군 상태이상(보라), 적 물피증(빨강), 실드(하늘색 막), 기절(노란 별)
        for (int i = 0; i < allyMarks.Length; i++)
        {
            allyMarks[i].gameObject.SetActive(alliesDebuffed);
            if (alliesDebuffed) Mark(allyMarks[i], allies[i].HeadPos, i);
        }
        bool eOn = debuffTimer > 0f && enemies.Length > 0;
        enemyMark.gameObject.SetActive(eOn);
        if (eOn) Mark(enemyMark, enemies[0].HeadPos, 0);

        float pop = Mathf.Clamp01((ShieldTime - shieldTimer) / 0.15f);
        for (int i = 0; i < shields.Length; i++)
        {
            bool on = shieldTimer > 0f;
            shields[i].gameObject.SetActive(on);
            if (!on) continue;
            PuppetRig rg = i == 0 ? lancer : allies[i - 1];
            shields[i].position = rg.transform.position + Vector3.up * 0.9f;
            shields[i].localScale = new Vector3(1.1f, 2f, 1.1f) * pop;
        }

        for (int i = 0; i < enemies.Length; i++)
            for (int k = 0; k < 3; k++)
            {
                Transform st = stars[i * 3 + k];
                bool on = inSlam[i] && stunTimer > 0f && sinceHit3 > 0.2f;
                st.gameObject.SetActive(on);
                if (!on) continue;
                float ang = clock * 6f + k * 2.094f;
                st.position = enemies[i].HeadPos + new Vector3(Mathf.Cos(ang) * 0.24f, 0.36f, Mathf.Sin(ang) * 0.24f);
            }
    }

    void Mark(Transform m, Vector3 head, int i)
    {
        m.position = head + Vector3.up * (0.45f + 0.04f * Mathf.Sin(clock * 4f + i));
        m.rotation = Quaternion.Euler(0f, clock * 90f, 45f);
    }

    void BuildVfx()
    {
        Cleanup();
        vfx = new GameObject("VFX (runtime)").transform;
        slashMat = Mat(new Color(0.85f, 0.97f, 1f, 0.8f));
        sparkMat = Mat(new Color(1f, 0.97f, 0.75f, 1f));
        jabMat = Mat(new Color(1f, 1f, 0.9f, 1f));
        ringMat = Mat(new Color(1f, 0.72f, 0.3f, 0.7f));
        plantMat = Mat(new Color(0.4f, 0.95f, 1f, 0.6f));
        dustMat = Mat(new Color(0.93f, 0.86f, 0.7f, 0.7f));
        Material shieldMat = Mat(new Color(0.45f, 0.85f, 1f, 0.3f));
        Material red = Mat(new Color(1f, 0.25f, 0.2f, 1f));
        Material purple = Mat(new Color(0.7f, 0.35f, 1f, 1f));
        Material yellow = Mat(new Color(1f, 0.93f, 0.3f, 1f));
        Material allyOrb = Mat(new Color(0.6f, 0.92f, 1f, 1f));
        Material enemyOrb = Mat(new Color(1f, 0.55f, 0.3f, 1f));

        slash = MeshObj("Slash", BakeSlash(), slashMat);
        slash.SetPositionAndRotation(lancer.BasePos, lancer.transform.rotation);
        spark = Prim(PrimitiveType.Sphere, "HitSpark", sparkMat, Vector3.one);
        jabSpark = Prim(PrimitiveType.Sphere, "BasicSpark", jabMat, Vector3.one * 0.3f);
        ring = MeshObj("SlamRing", Ring(0.82f, 1f, 48), ringMat);
        plantRing = MeshObj("PlantRing", Ring(0.82f, 1f, 48), plantMat);
        dust = new Transform[6];
        for (int i = 0; i < dust.Length; i++) dust[i] = Prim(PrimitiveType.Sphere, "Dust", dustMat, Vector3.one * 0.4f);
        foreach (Dummy d in dummies) d.orb = Prim(PrimitiveType.Sphere, "Orb", d.enemy ? enemyOrb : allyOrb, Vector3.one * 0.16f);
        shields = new Transform[allies.Length + 1];
        for (int i = 0; i < shields.Length; i++) shields[i] = Prim(PrimitiveType.Sphere, "Shield", shieldMat, Vector3.one);
        allyMarks = new Transform[allies.Length];
        for (int i = 0; i < allyMarks.Length; i++) allyMarks[i] = Prim(PrimitiveType.Cube, "DebuffMark", purple, Vector3.one * 0.16f);
        enemyMark = Prim(PrimitiveType.Cube, "PDmgUpMark", red, Vector3.one * 0.16f);
        stars = new Transform[enemies.Length * 3];
        for (int i = 0; i < stars.Length; i++) stars[i] = Prim(PrimitiveType.Sphere, "StunStar", yellow, Vector3.one * 0.09f);

        if (!Application.isPlaying)
            foreach (Transform tr in vfx.GetComponentsInChildren<Transform>(true)) tr.gameObject.hideFlags = HideFlags.DontSave;
    }

    // 올려치기 동안 창날이 실제로 지나가는 길을 따라 띠 모양 메시를 만든다.
    Mesh BakeSlash()
    {
        const int n = 14;
        var v = new Vector3[(n + 1) * 2];
        var tri = new int[n * 6];
        int keep = skill;
        skill = 0;
        Transform root = lancer.transform;
        for (int i = 0; i <= n; i++)
        {
            lancer.Apply(LancerPose(Mathf.Lerp(0.21f, 0.36f, (float)i / n)), 0f);
            v[i * 2] = root.InverseTransformPoint(lancer.SpearPoint(0.55f));
            v[i * 2 + 1] = root.InverseTransformPoint(lancer.SpearPoint(1.45f));
        }
        skill = keep;
        for (int i = 0; i < n; i++)
        {
            int k = i * 6, b = i * 2;
            tri[k] = b; tri[k + 1] = b + 1; tri[k + 2] = b + 2;
            tri[k + 3] = b + 1; tri[k + 4] = b + 3; tri[k + 5] = b + 2;
        }
        var m = new Mesh { hideFlags = HideFlags.DontSave };
        m.vertices = v;
        m.triangles = tri;
        m.RecalculateBounds();
        return m;
    }

    public void Cleanup()
    {
        GameObject old = GameObject.Find("VFX (runtime)");
        while (old != null)
        {
            DestroyImmediate(old);
            old = GameObject.Find("VFX (runtime)");
        }
        vfx = null;
    }

    Transform Prim(PrimitiveType type, string n, Material m, Vector3 scale)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        g.name = n;
        DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(vfx, false);
        g.transform.localScale = scale;
        MeshRenderer r = g.GetComponent<MeshRenderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        g.SetActive(false);
        return g.transform;
    }

    Transform MeshObj(string n, Mesh mesh, Material m)
    {
        GameObject g = new GameObject(n);
        g.transform.SetParent(vfx, false);
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer r = g.AddComponent<MeshRenderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        g.SetActive(false);
        return g.transform;
    }

    static Material Mat(Color c)
    {
        Material m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        m.hideFlags = HideFlags.DontSave;
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_Cull", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = 3000;
        return m;
    }

    // 바닥(XZ)에 놓인 고리
    static Mesh Ring(float r0, float r1, int seg)
    {
        var v = new Vector3[(seg + 1) * 2];
        var tri = new int[seg * 6];
        for (int i = 0; i <= seg; i++)
        {
            float a = Mathf.PI * 2f * i / seg;
            var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            v[i * 2] = d * r0;
            v[i * 2 + 1] = d * r1;
        }
        for (int i = 0; i < seg; i++)
        {
            int k = i * 6, b = i * 2;
            tri[k] = b; tri[k + 1] = b + 1; tri[k + 2] = b + 2;
            tri[k + 3] = b + 1; tri[k + 4] = b + 3; tri[k + 5] = b + 2;
        }
        var m = new Mesh { hideFlags = HideFlags.DontSave };
        m.vertices = v;
        m.triangles = tri;
        m.RecalculateBounds();
        return m;
    }

    static float Smooth(float u) => u * u * (3f - 2f * u);

    static void SetA(Material m, float a)
    {
        Color c = m.GetColor("_BaseColor");
        c.a = Mathf.Clamp01(a);
        m.SetColor("_BaseColor", c);
    }
}
