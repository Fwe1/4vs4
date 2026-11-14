using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 프로토타입 씬(PixelPrototype)의 캐릭터를 관절 구조로 다시 만들고,
// 스킬 장면을 이미지로 찍어 주는 에디터 도구.
public static class PixelPrototypeBuilder
{
    const string ScenePath = "Assets/Scenes/PixelPrototype.unity";
    const string MatDir = "Assets/Prototype/Materials";
    const float Outline = 0.022f;

    static Shader toon;
    static Material skin, white, tights, boot, metal, gold, wood, eye, ribbon;

    static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

    [MenuItem("KongsRaid/Rebuild Pixel Prototype")]
    public static void Rebuild()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("플레이 모드를 끄고 다시 실행하세요.");
            return;
        }
        var scene = EditorSceneManager.OpenScene(ScenePath);
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            string n = go.name;
            if (n == "Lancer" || n == "SkillDemo" || n == "Environment" || n.StartsWith("Ally_") || n.StartsWith("Enemy_") || n.StartsWith("Hero_") || n.StartsWith("VFX"))
                Object.DestroyImmediate(go);
        }

        toon = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Prototype/Shaders/Toon.shader");
        Color cool = new Color(0.62f, 0.62f, 0.8f);
        skin = Mat("Skin", new Color(1f, 0.87f, 0.78f), new Color(0.95f, 0.72f, 0.72f), Outline);
        white = Mat("White", new Color(0.96f, 0.95f, 0.92f), new Color(0.7f, 0.72f, 0.88f), Outline);
        tights = Mat("Tights", new Color(0.15f, 0.17f, 0.3f), new Color(0.65f, 0.65f, 0.8f), Outline);
        boot = Mat("Boot", new Color(0.38f, 0.24f, 0.16f), new Color(0.62f, 0.6f, 0.72f), Outline);
        metal = Mat("Metal", new Color(0.82f, 0.85f, 0.9f), new Color(0.6f, 0.62f, 0.75f), Outline);
        gold = Mat("Gold", new Color(0.95f, 0.78f, 0.3f), new Color(0.75f, 0.6f, 0.6f), Outline);
        wood = Mat("Wood", new Color(0.45f, 0.3f, 0.2f), new Color(0.65f, 0.6f, 0.72f), Outline);
        eye = Mat("Eye", new Color(0.1f, 0.2f, 0.3f), Color.white, 0f);
        ribbon = Mat("Ribbon", new Color(0.85f, 0.2f, 0.25f), new Color(0.7f, 0.6f, 0.72f), Outline);
        Material capeNavy = Mat("Cape", new Color(0.18f, 0.22f, 0.42f), new Color(0.65f, 0.65f, 0.8f), Outline);
        Material capeRed = Mat("Cape_Red", new Color(0.45f, 0.12f, 0.15f), cool, Outline);

        BuildEnvironment(cool);

        // 캐릭터는 교전 위치에 놓는다. 시작 구도(양 팀이 떨어져 선 상태)는 LancerSkillDemo가 뒤로 물려서 만든다.
        // 좌우 자리: 1번 중앙, 2번 위(+z), 3번 아래(-z), 4번 위. 상대는 좌우 거울 반전.
        Build("Lancer", V(-0.9f, 0f, 0f), 110f, 1f,
            Mat("Jacket", new Color(0.15f, 0.55f, 0.6f), new Color(0.55f, 0.6f, 0.8f), Outline),
            Mat("Hair", new Color(0.98f, 0.55f, 0.25f), new Color(0.8f, 0.55f, 0.62f), Outline), capeNavy, true);
        Build("Ally_1", V(-2.2f, 0f, 1.4f), 110f, 1f,
            Mat("Jacket_Purple", new Color(0.5f, 0.32f, 0.72f), cool, Outline),
            Mat("Hair_Dark", new Color(0.18f, 0.16f, 0.22f), cool, Outline), capeNavy, false);
        Build("Ally_2", V(-3.4f, 0f, -1.4f), 110f, 1f,
            Mat("Jacket_Green", new Color(0.3f, 0.6f, 0.32f), cool, Outline),
            Mat("Hair_Blond", new Color(0.98f, 0.86f, 0.45f), new Color(0.8f, 0.65f, 0.65f), Outline), capeNavy, false);
        Build("Ally_3", V(-4.6f, 0f, 1.4f), 110f, 1f,
            Mat("Jacket_Amber", new Color(0.92f, 0.66f, 0.22f), cool, Outline),
            Mat("Hair_Pink", new Color(0.96f, 0.72f, 0.8f), cool, Outline), capeNavy, false);
        Build("Enemy_1", V(0.9f, 0f, 0f), -110f, -1f,
            Mat("Jacket_Red", new Color(0.82f, 0.22f, 0.2f), cool, Outline),
            Mat("Hair_Gray", new Color(0.45f, 0.45f, 0.5f), cool, Outline), capeRed, false);
        Build("Enemy_2", V(2.1f, 0f, 1.4f), -110f, -1f,
            Mat("Jacket_Maroon", new Color(0.55f, 0.16f, 0.24f), cool, Outline),
            Mat("Hair_Silver", new Color(0.9f, 0.9f, 0.95f), cool, Outline), capeRed, false);
        Build("Enemy_3", V(3.3f, 0f, -1.4f), -110f, -1f,
            Mat("Jacket_Slate", new Color(0.32f, 0.32f, 0.4f), cool, Outline),
            Mat("Hair_Crimson", new Color(0.78f, 0.2f, 0.22f), cool, Outline), capeRed, false);
        Build("Enemy_4", V(4.5f, 0f, 1.4f), -110f, -1f,
            Mat("Jacket_Plum", new Color(0.42f, 0.2f, 0.46f), cool, Outline),
            Mat("Hair_Dark", new Color(0.18f, 0.16f, 0.22f), cool, Outline), capeRed, false);

        // 시작 구도: 약 25° 내려다보는 고정 카메라로 양 팀 8명을 뒷줄까지 한 화면에 담는다.
        Transform cam = GameObject.Find("Main Camera").transform;
        cam.rotation = Quaternion.Euler(25f, 0f, 0f);
        cam.position = V(0f, 0.8f, 0.2f) - cam.forward * 12.5f;

        // 도트 화면 해상도. 8명이 다 보이도록 멀리서 찍으므로 384×216보다 조금 키웠다.
        RenderTexture pixelRt = cam.GetComponent<Camera>().targetTexture;
        if (pixelRt != null && (pixelRt.width != 480 || pixelRt.height != 270))
        {
            pixelRt.Release();
            pixelRt.width = 480;
            pixelRt.height = 270;
            EditorUtility.SetDirty(pixelRt);
        }

        // Game 뷰가 16:9가 아니어도 화면 양 끝이 잘리지 않게 한다.
        GameObject display = GameObject.Find("Display Camera (pixel upscale)");
        if (display != null && display.GetComponent<PixelDisplayFit>() == null) display.AddComponent<PixelDisplayFit>();

        LancerSkillDemo demo = new GameObject("SkillDemo").AddComponent<LancerSkillDemo>();
        demo.Init();
        demo.ResetPose();
        demo.Cleanup();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    // 스킬 3개와 평타를 5장씩 찍어 한 장으로 붙인다 (위부터 스킬 1, 2, 3, 평타). dist가 0보다 크면 그 거리까지 카메라를 당겨서 찍는다.
    public static string CaptureSheet(string outPath, float dist, float focusX)
    {
        LancerSkillDemo demo = Object.FindFirstObjectByType<LancerSkillDemo>();
        if (demo == null) return "SkillDemo가 씬에 없습니다";
        Camera cam = GameObject.Find("Main Camera").GetComponent<Camera>();
        RenderTexture rt = cam.targetTexture;
        Transform ct = cam.transform;
        Vector3 keepPos = ct.position;
        if (dist > 0f) ct.position = V(focusX, 1.1f, 0f) - ct.forward * dist;
        demo.Init();

        float[][] times =
        {
            new[] { 0.1f, 0.2f, 0.3f, 0.34f, 0.46f },
            new[] { 0.15f, 0.3f, 0.42f, 0.6f, 1.05f },
            new[] { 0.2f, 0.32f, 0.5f, 0.74f, 1.0f },
            new[] { 0.08f, 0.16f, 0.26f, 0.4f, 0.6f }, // 평타 (LancerSkillDemo.Basic)
            new[] { 0.5f, 1.45f, 1.9f, 2.5f, 3.3f },   // 시작 구도 → 전진 → 교전 (LancerSkillDemo.Intro)
        };
        int rows = times.Length, cols = times[0].Length, fw = rt.width, fh = rt.height;
        var sheet = new Texture2D(fw * cols, fh * rows, TextureFormat.RGB24, false);
        var frame = new Texture2D(fw, fh, TextureFormat.RGB24, false);
        bool prevAsync = ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation = false;
        for (int row = 0; row < rows; row++)
            for (int col = 0; col < cols; col++)
            {
                demo.SimulateTo(row, times[row][col]);
                cam.Render();
                RenderTexture.active = rt;
                frame.ReadPixels(new Rect(0, 0, fw, fh), 0, 0);
                frame.Apply();
                RenderTexture.active = null;
                sheet.SetPixels32(col * fw, (rows - 1 - row) * fh, fw, fh, frame.GetPixels32());
            }
        ShaderUtil.allowAsyncCompilation = prevAsync;
        sheet.Apply();
        System.IO.File.WriteAllBytes(outPath, sheet.EncodeToPNG());
        Object.DestroyImmediate(sheet);
        Object.DestroyImmediate(frame);

        demo.ResetPose();
        demo.Cleanup();
        ct.position = keepPos;
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return outPath;
    }

    // 한 장면(skill: 0~2 스킬, 3 평타, 4 시작)의 여러 시점을 가로로 이어 붙여 저장한다. scale은 픽셀 확대 배수.
    public static string CaptureStrip(string outPath, int skill, string timesCsv, int scale)
    {
        LancerSkillDemo demo = Object.FindFirstObjectByType<LancerSkillDemo>();
        if (demo == null) return "SkillDemo가 씬에 없습니다";
        Camera cam = GameObject.Find("Main Camera").GetComponent<Camera>();
        RenderTexture rt = cam.targetTexture;
        string[] parts = timesCsv.Split(',');
        int n = parts.Length, fw = rt.width, fh = rt.height, bw = fw * scale, bh = fh * scale;
        demo.Init();
        var frame = new Texture2D(fw, fh, TextureFormat.RGB24, false);
        var sheet = new Texture2D(bw * n, bh, TextureFormat.RGB24, false);
        var big = new Color32[bw * bh];
        bool prevAsync = ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation = false;
        for (int i = 0; i < n; i++)
        {
            demo.SimulateTo(skill, float.Parse(parts[i], System.Globalization.CultureInfo.InvariantCulture));
            cam.Render();
            RenderTexture.active = rt;
            frame.ReadPixels(new Rect(0, 0, fw, fh), 0, 0);
            frame.Apply();
            RenderTexture.active = null;
            Color32[] src = frame.GetPixels32();
            for (int y = 0; y < bh; y++)
                for (int x = 0; x < bw; x++)
                    big[y * bw + x] = src[(y / scale) * fw + x / scale];
            sheet.SetPixels32(i * bw, 0, bw, bh, big);
        }
        ShaderUtil.allowAsyncCompilation = prevAsync;
        sheet.Apply();
        System.IO.File.WriteAllBytes(outPath, sheet.EncodeToPNG());
        Object.DestroyImmediate(sheet);
        Object.DestroyImmediate(frame);

        demo.ResetPose();
        demo.Cleanup();
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return outPath;
    }

    // 넓은 풀밭, 가운데 흙길(전장), 뒤쪽 숲. 카메라가 위에서 넓게 보므로 화면 위쪽까지 채운다.
    static void BuildEnvironment(Color cool)
    {
        Material ground = Mat("Ground", new Color(0.52f, 0.76f, 0.42f), new Color(0.75f, 0.8f, 0.88f), 0f);
        Material path = Mat("Path", new Color(0.84f, 0.74f, 0.52f), new Color(0.78f, 0.75f, 0.85f), 0f);
        Material trunk = Mat("Trunk", new Color(0.42f, 0.29f, 0.18f), new Color(0.65f, 0.6f, 0.72f), Outline);
        Material leaf = Mat("Leaf", new Color(0.3f, 0.6f, 0.32f), new Color(0.6f, 0.7f, 0.8f), Outline);
        Material rock = Mat("Rock", new Color(0.62f, 0.6f, 0.58f), cool, Outline);

        Transform env = new GameObject("Environment").transform;
        Part(PrimitiveType.Plane, "Ground", env, V(0f, 0f, 10f), V(12f, 1f, 8f), ground);
        Part(PrimitiveType.Cube, "Path", env, V(0f, 0f, 0.2f), V(70f, 0.02f, 6.6f), path);

        float[,] trees =
        {
            { -13f, 8.5f }, { -9f, 7.2f }, { -5.5f, 8.8f }, { -2f, 7.4f }, { 1.5f, 8.6f }, { 5f, 7.3f }, { 8.5f, 8.9f }, { 12f, 7.5f },
            { -15.5f, 11f }, { -11f, 12.5f }, { -6.5f, 13f }, { -2.5f, 12.2f }, { 2f, 13.2f }, { 6f, 12.4f }, { 10.5f, 13f }, { 15f, 11.5f },
            { -13.5f, 16.5f }, { -8.5f, 17f }, { -4f, 16.2f }, { 0.5f, 17.2f }, { 4.5f, 16.4f }, { 9f, 17f }, { 13.5f, 16.3f },
        };
        for (int i = 0; i < trees.GetLength(0); i++)
        {
            Vector3 p = V(trees[i, 0], 0f, trees[i, 1]);
            Part(PrimitiveType.Cylinder, "Trunk", env, p + V(0f, 1f, 0f), V(0.35f, 1f, 0.35f), trunk);
            Part(PrimitiveType.Sphere, "Leaf", env, p + V(0f, 2.9f, 0f), V(2.4f, 2.2f, 2.4f), leaf);
        }
        Part(PrimitiveType.Sphere, "Rock", env, V(-5.2f, 0.15f, 4.6f), V(0.8f, 0.5f, 0.7f), rock);
        Part(PrimitiveType.Sphere, "Rock", env, V(6.1f, 0.2f, 4.9f), V(1f, 0.6f, 0.8f), rock);
        Part(PrimitiveType.Sphere, "Rock", env, V(0.8f, 0.12f, 5.3f), V(0.6f, 0.35f, 0.5f), rock);
        Part(PrimitiveType.Sphere, "Rock", env, V(-2.6f, 0.14f, -3.9f), V(0.7f, 0.4f, 0.6f), rock);
    }

    static Material Mat(string name, Color baseC, Color shade, float outline)
    {
        string path = MatDir + "/PxToon_" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(toon);
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = toon;
        m.SetColor("_BaseColor", baseC);
        m.SetColor("_ShadeColor", shade);
        m.SetFloat("_OutlineWidth", outline);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Transform Joint(string name, Transform parent, Vector3 pos)
    {
        Transform t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = pos;
        return t;
    }

    static void Part(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material m)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        g.name = name;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = m;
    }

    // 관절(빈 오브젝트) 아래에 도형을 매다는 구조. 관절 이름은 PuppetRig가 찾는 이름과 같아야 한다.
    static void Build(string name, Vector3 pos, float yaw, float laneSign, Material jacket, Material hair, Material cape, bool withSpear)
    {
        const PrimitiveType Sphere = PrimitiveType.Sphere, Capsule = PrimitiveType.Capsule, Cylinder = PrimitiveType.Cylinder, Cube = PrimitiveType.Cube;
        Transform root = new GameObject(name).transform;
        root.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));

        Transform hips = Joint("Hips", root, V(0f, 0.9f, 0f));
        Part(Sphere, "Skirt", hips, V(0f, -0.04f, 0f), V(0.42f, 0.22f, 0.34f), white);
        Part(Cylinder, "Belt", hips, V(0f, 0.04f, 0f), V(0.3f, 0.02f, 0.24f), boot);
        for (int sd = -1; sd <= 1; sd += 2)
        {
            string s = sd < 0 ? "L" : "R";
            Transform leg = Joint("Leg" + s, hips, V(0.1f * sd, -0.04f, 0f));
            Part(Capsule, "Thigh", leg, V(0f, -0.2f, 0f), V(0.15f, 0.2f, 0.15f), tights);
            Transform shin = Joint("Shin" + s, leg, V(0f, -0.4f, 0f));
            Part(Sphere, "Knee", shin, V(0f, 0f, 0f), V(0.14f, 0.14f, 0.14f), tights);
            Part(Capsule, "Boot", shin, V(0f, -0.19f, 0f), V(0.13f, 0.21f, 0.13f), boot);
            Transform foot = Joint("Foot" + s, shin, V(0f, -0.4f, 0f));
            Part(Sphere, "Sole", foot, V(0f, -0.025f, 0.06f), V(0.13f, 0.09f, 0.24f), boot);
        }

        Transform spine = Joint("Spine", hips, V(0f, 0.08f, 0f));
        Part(Capsule, "Waist", spine, V(0f, 0.01f, 0f), V(0.26f, 0.14f, 0.2f), jacket);
        Part(Capsule, "Chest", spine, V(0f, 0.19f, 0f), V(0.32f, 0.2f, 0.22f), jacket);
        Part(Sphere, "Collar", spine, V(0f, 0.36f, 0.01f), V(0.28f, 0.08f, 0.24f), white);
        Transform capePivot = Joint("CapePivot", spine, V(0f, 0.36f, -0.13f));
        Part(Cube, "Cape", capePivot, V(0f, -0.26f, 0f), V(0.4f, 0.5f, 0.03f), cape);

        Transform neck = Joint("Neck", spine, V(0f, 0.4f, 0f));
        Part(Cylinder, "NeckSkin", neck, V(0f, 0.02f, 0f), V(0.08f, 0.05f, 0.08f), skin);
        Part(Sphere, "Head", neck, V(0f, 0.15f, 0f), V(0.3f, 0.34f, 0.31f), skin);
        Part(Sphere, "HairCap", neck, V(0f, 0.2f, -0.025f), V(0.33f, 0.33f, 0.33f), hair);
        Part(Sphere, "Bangs", neck, V(0f, 0.25f, 0.08f), V(0.3f, 0.13f, 0.17f), hair);
        Part(Sphere, "Tie", neck, V(0f, 0.28f, -0.16f), V(0.08f, 0.08f, 0.08f), ribbon);
        Transform tail = Joint("TailPivot", neck, V(0f, 0.27f, -0.17f));
        Part(Capsule, "Ponytail", tail, V(0f, -0.27f, 0f), V(0.12f, 0.3f, 0.1f), hair);

        for (int sd = -1; sd <= 1; sd += 2)
        {
            string s = sd < 0 ? "L" : "R";
            Part(Capsule, "Lock" + s, neck, V(0.135f * sd, 0.09f, 0.06f), V(0.06f, 0.11f, 0.06f), hair);
            Part(Sphere, "Eye" + s, neck, V(0.065f * sd, 0.14f, 0.145f), V(0.05f, 0.075f, 0.03f), eye);
            Part(Sphere, "Pauldron" + s, spine, V(0.2f * sd, 0.35f, 0f), V(0.15f, 0.09f, 0.15f), metal);
            Transform arm = Joint("Arm" + s, spine, V(0.2f * sd, 0.33f, 0f));
            Part(Capsule, "UpperArm", arm, V(0f, -0.13f, 0f), V(0.09f, 0.14f, 0.09f), jacket);
            Transform fore = Joint("Fore" + s, arm, V(0f, -0.26f, 0f));
            Part(Sphere, "Elbow", fore, V(0f, 0f, 0f), V(0.09f, 0.09f, 0.09f), jacket);
            Part(Capsule, "Forearm", fore, V(0f, -0.12f, 0f), V(0.08f, 0.13f, 0.08f), white);
            Part(Sphere, "Hand", fore, V(0f, -0.26f, 0f), V(0.085f, 0.085f, 0.085f), skin);
        }

        if (withSpear)
        {
            Transform spear = Joint("Spear", root, V(0.1f, 1f, 0.25f));
            Part(Cylinder, "Shaft", spear, V(0f, 0f, 0f), V(0.035f, 0.95f, 0.035f), wood);
            Part(Cube, "Guard", spear, V(0f, 0.95f, 0f), V(0.14f, 0.03f, 0.05f), gold);
            Part(Sphere, "Blade", spear, V(0f, 1.2f, 0f), V(0.08f, 0.5f, 0.035f), metal);
            Part(Sphere, "Pommel", spear, V(0f, -0.95f, 0f), V(0.07f, 0.07f, 0.07f), gold);
        }

        root.gameObject.AddComponent<PuppetRig>().laneSign = laneSign;
    }
}
