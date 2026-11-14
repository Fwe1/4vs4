using UnityEngine;

// 도트 화면(16:9)을 Game 뷰 모양에 상관없이 통째로 보여 준다.
// Game 뷰가 16:9보다 좁으면 그림 폭에 맞추고 위아래에 검은 띠를 둔다. 더 넓으면 좌우에 띠가 생긴다.
[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class PixelDisplayFit : MonoBehaviour
{
    public float pictureAspect = 16f / 9f;

    Camera cam;

    void OnEnable()
    {
        cam = GetComponent<Camera>();
        Fit();
    }

    void Update() { Fit(); }

    void Fit()
    {
        if (cam == null) return;
        // 반 높이 0.5가 그림 높이에 딱 맞는 값이다. 화면이 더 좁으면 그림 폭이 다 들어오도록 키운다.
        cam.orthographicSize = Mathf.Max(0.5f, 0.5f * pictureAspect / cam.aspect);
    }
}
