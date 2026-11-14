using UnityEngine;

// 프로토타입용 인형 뼈대의 한 순간 자세. 길이는 미터, 각도는 도.
// 위치는 캐릭터 기준이다 (x 오른쪽, y 위, z 앞).
public struct PuppetPose
{
    public float rootZ, rootY;            // 전체 이동: 전진 방향, 높이
    public float hipY, hipZ;              // 골반 높이, 앞뒤
    public float hipPitch, hipYaw;        // 골반 숙임, 비틀기
    public float chestPitch, chestYaw;    // 상체 숙임, 비틀기 (골반 기준)
    public float headPitch, headYaw;      // 머리 (상체 기준)
    public Vector3 footL, footR;          // 발목 위치
    public float footPitchL, footPitchR;  // 발끝 내리기
    public Vector3 grip;                  // 창을 쥔 두 손의 가운데
    public float elev, azim, slide;       // 창끝 방향(올림각, 좌우각), 쥐는 위치(자루 중심 기준)
    public Vector3 handL, handR;          // 무기 없는 캐릭터의 손 위치 (상체 기준)

    public static PuppetPose Stand()
    {
        var p = new PuppetPose();
        p.hipY = 0.9f;
        p.footL = new Vector3(-0.11f, 0.07f, 0.03f);
        p.footR = new Vector3(0.11f, 0.07f, -0.03f);
        p.handL = new Vector3(-0.25f, -0.17f, 0.04f);
        p.handR = new Vector3(0.25f, -0.17f, 0.04f);
        p.grip = new Vector3(0.1f, 1.02f, 0.22f);
        p.elev = 18f;
        p.azim = -12f;
        p.slide = -0.1f;
        return p;
    }

    public static PuppetPose Lerp(in PuppetPose a, in PuppetPose b, float t)
    {
        var p = new PuppetPose();
        p.rootZ = L(a.rootZ, b.rootZ, t);
        p.rootY = L(a.rootY, b.rootY, t);
        p.hipY = L(a.hipY, b.hipY, t);
        p.hipZ = L(a.hipZ, b.hipZ, t);
        p.hipPitch = L(a.hipPitch, b.hipPitch, t);
        p.hipYaw = L(a.hipYaw, b.hipYaw, t);
        p.chestPitch = L(a.chestPitch, b.chestPitch, t);
        p.chestYaw = L(a.chestYaw, b.chestYaw, t);
        p.headPitch = L(a.headPitch, b.headPitch, t);
        p.headYaw = L(a.headYaw, b.headYaw, t);
        p.footL = Vector3.LerpUnclamped(a.footL, b.footL, t);
        p.footR = Vector3.LerpUnclamped(a.footR, b.footR, t);
        p.footPitchL = L(a.footPitchL, b.footPitchL, t);
        p.footPitchR = L(a.footPitchR, b.footPitchR, t);
        p.grip = Vector3.LerpUnclamped(a.grip, b.grip, t);
        p.elev = L(a.elev, b.elev, t);
        p.azim = L(a.azim, b.azim, t);
        p.slide = L(a.slide, b.slide, t);
        p.handL = Vector3.LerpUnclamped(a.handL, b.handL, t);
        p.handR = Vector3.LerpUnclamped(a.handR, b.handR, t);
        return p;
    }

    static float L(float a, float b, float t) => a + (b - a) * t;
}

// 자세(PuppetPose)를 받아 관절을 돌린다.
// 팔다리는 손발 위치에 맞춰 스스로 굽고(2관절 IK), 머리카락과 망토는 움직임을 뒤따라 흔들린다.
public class PuppetRig : MonoBehaviour
{
    public float laneSign = 1f; // +1이면 오른쪽(+X)으로 전진, -1이면 왼쪽

    const float Thigh = 0.4f, Shin = 0.4f, UpperArm = 0.26f, Forearm = 0.26f, HandHalf = 0.22f;

    Transform hips, spine, neck, legL, shinL, footL, legR, shinR, footR, armL, foreL, armR, foreR, cape, tail, spear;
    // 교전 위치. 처음 한 번만 기록해 씬에 저장한다 (시작 구도로 물러나 있어도 바뀌지 않게).
    [SerializeField] bool hasBase;
    [SerializeField] Vector3 basePos;
    [SerializeField] Quaternion baseRot = Quaternion.identity;
    Vector3 prevHead, prevChest;
    float tailLag, capeLag;
    bool hasPrev;

    public Vector3 BasePos => basePos;
    public Vector3 HeadPos => neck.position + Vector3.up * 0.15f;
    public Vector3 ChestPos => spine.position + Vector3.up * 0.2f;
    public Vector3 HandRPos => foreR.TransformPoint(new Vector3(0f, -0.26f, 0f));
    public Vector3 SpearPoint(float y) => spear.TransformPoint(new Vector3(0f, y, 0f));

    public void Init()
    {
        Transform r = transform;
        hips = r.Find("Hips");
        spine = hips.Find("Spine");
        neck = spine.Find("Neck");
        legL = hips.Find("LegL");
        shinL = legL.Find("ShinL");
        footL = shinL.Find("FootL");
        legR = hips.Find("LegR");
        shinR = legR.Find("ShinR");
        footR = shinR.Find("FootR");
        armL = spine.Find("ArmL");
        foreL = armL.Find("ForeL");
        armR = spine.Find("ArmR");
        foreR = armR.Find("ForeR");
        cape = spine.Find("CapePivot");
        tail = neck.Find("TailPivot");
        spear = r.Find("Spear");
        if (!hasBase)
        {
            basePos = r.position;
            baseRot = r.rotation;
            hasBase = true;
        }
        ResetSecondary();
    }

    public void ResetSecondary()
    {
        hasPrev = false;
        tailLag = 0f;
        capeLag = 0f;
    }

    // dt가 0이면 머리카락·망토의 흔들림 계산은 건너뛴다.
    public void Apply(in PuppetPose p, float dt)
    {
        Transform r = transform;
        r.SetPositionAndRotation(basePos + new Vector3(laneSign * p.rootZ, p.rootY, 0f), baseRot);
        hips.localPosition = new Vector3(0f, p.hipY, p.hipZ);
        hips.localRotation = Quaternion.Euler(p.hipPitch, p.hipYaw, 0f);
        spine.localRotation = Quaternion.Euler(p.chestPitch, p.chestYaw, 0f);
        neck.localRotation = Quaternion.Euler(p.headPitch, p.headYaw, 0f);

        Vector3 fwd = r.forward, right = r.right, up = Vector3.up;

        // 다리: 무릎은 앞쪽(살짝 바깥)으로 굽는다
        Solve(legL, shinL, Thigh, Shin, r.TransformPoint(p.footL), fwd - right * 0.15f);
        Solve(legR, shinR, Thigh, Shin, r.TransformPoint(p.footR), fwd + right * 0.15f);
        footL.rotation = baseRot * Quaternion.Euler(p.footPitchL, 0f, 0f);
        footR.rotation = baseRot * Quaternion.Euler(p.footPitchR, 0f, 0f);

        // 팔: 창이 있으면 두 손으로 자루를 쥔다 (왼손이 날 쪽)
        Vector3 tL, tR;
        if (spear != null)
        {
            float e = p.elev * Mathf.Deg2Rad, a = p.azim * Mathf.Deg2Rad;
            Vector3 d = r.TransformDirection(new Vector3(Mathf.Sin(a) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(a) * Mathf.Cos(e)));
            Vector3 g = r.TransformPoint(p.grip);
            Vector3 z = Vector3.Cross(right, d);
            if (z.sqrMagnitude < 1e-4f) z = Vector3.Cross(fwd, d);
            spear.SetPositionAndRotation(g - d * p.slide, Quaternion.LookRotation(z.normalized, d));
            tL = g + d * HandHalf;
            tR = g - d * HandHalf;
        }
        else
        {
            tL = spine.TransformPoint(p.handL);
            tR = spine.TransformPoint(p.handR);
        }
        Vector3 elbow = -up * 0.7f - fwd * 0.4f;
        Solve(armL, foreL, UpperArm, Forearm, tL, elbow - right * 0.6f);
        Solve(armR, foreR, UpperArm, Forearm, tR, elbow + right * 0.6f);

        // 머리카락·망토: 몸이 움직인 반대쪽으로 뒤따라 흔들린다
        if (dt > 0f)
        {
            Vector3 hp = neck.position, cp = spine.position;
            float tTarget = 0f, cTarget = 0f;
            if (hasPrev)
            {
                Quaternion inv = Quaternion.Inverse(baseRot);
                Vector3 vh = inv * ((hp - prevHead) / dt);
                Vector3 vc = inv * ((cp - prevChest) / dt);
                tTarget = Mathf.Clamp(vh.z * 14f - vh.y * 16f, -25f, 95f);
                cTarget = Mathf.Clamp(vc.z * 10f - vc.y * 12f, -10f, 80f);
            }
            float k = 1f - Mathf.Exp(-9f * dt);
            tailLag = Mathf.Lerp(tailLag, tTarget, k);
            capeLag = Mathf.Lerp(capeLag, cTarget, k);
            prevHead = hp;
            prevChest = cp;
            hasPrev = true;
        }
        float body = p.hipPitch + p.chestPitch;
        float yaw = p.hipYaw + p.chestYaw;
        if (cape != null)
            cape.rotation = baseRot * Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(Mathf.Max(8f + capeLag, body + 6f), 0f, 0f);
        if (tail != null)
            tail.rotation = baseRot * Quaternion.Euler(0f, yaw + p.headYaw, 0f) * Quaternion.Euler(Mathf.Max(15f + tailLag, body + p.headPitch + 8f), 0f, 0f);
    }

    // 2관절 IK. 뼈는 자기 -Y 방향으로 뻗어 있다고 본다.
    static void Solve(Transform upper, Transform lower, float l1, float l2, Vector3 target, Vector3 pole)
    {
        Vector3 a = upper.position;
        Vector3 to = target - a;
        float d = to.magnitude;
        Vector3 dir = d > 1e-5f ? to / d : Vector3.down;
        d = Mathf.Clamp(d, Mathf.Abs(l1 - l2) + 0.01f, l1 + l2 - 0.002f);
        float x = (l1 * l1 - l2 * l2 + d * d) / (2f * d);
        float h = Mathf.Sqrt(Mathf.Max(0f, l1 * l1 - x * x));
        Vector3 p = pole - Vector3.Dot(pole, dir) * dir;
        if (p.sqrMagnitude < 1e-6f) p = Vector3.Cross(dir, Vector3.right);
        p.Normalize();
        Vector3 j = a + dir * x + p * h;
        Vector3 end = a + dir * d;
        upper.rotation = Aim(a - j, p);
        lower.rotation = Aim(j - end, p);
    }

    static Quaternion Aim(Vector3 y, Vector3 hint)
    {
        y.Normalize();
        Vector3 z = hint - Vector3.Dot(hint, y) * y;
        if (z.sqrMagnitude < 1e-6f) z = Vector3.Cross(y, Vector3.right);
        return Quaternion.LookRotation(z.normalized, y);
    }
}
