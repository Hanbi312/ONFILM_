using Fusion;
using UnityEngine;

/// <summary>
/// FilmItem - 필름 아이템
/// 하나를 획득하면 씬의 모든 FilmItem이 사라짐
/// </summary>
public class FilmItem : NetworkBehaviour
{
    [Networked] public NetworkBool IsPickedUp { get; set; }

    private void Update()
    {
        if (Object == null || !Object.IsValid) return;
        if (IsPickedUp && gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    // ActorInteraction RPC에서 서버가 호출
    public void PickUp()
    {
        if (IsPickedUp) return;
        IsPickedUp = true;

        // ★ 씬의 모든 FilmItem 서버에서 IsPickedUp = true로 설정
        // → [Networked] 변수라서 모든 클라이언트 Update()에서 자동 비활성화
        var allFilms = FindObjectsByType<FilmItem>(FindObjectsSortMode.None);
        foreach (var film in allFilms)
        {
            if (!film.IsPickedUp)
                film.IsPickedUp = true;
        }

        // ★ 모든 클라이언트에서 즉시 비활성화 RPC
        RPC_HideAllFilms();

        // 모든 카메라 라이트 ON
        var cameras = FindObjectsByType<SecurityCamera>(FindObjectsSortMode.None);
        foreach (var cam in cameras)
            cam.TurnOnLight();

        Debug.Log($"[FilmItem] 획득 완료 - 모든 필름 사라짐, 카메라 {cameras.Length}대 라이트 ON");
    }

    // ★ 모든 클라이언트에서 모든 FilmItem 비활성화
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_HideAllFilms()
    {
        var allFilms = FindObjectsByType<FilmItem>(FindObjectsSortMode.None);
        foreach (var film in allFilms)
            film.gameObject.SetActive(false);

        Debug.Log($"[FilmItem] 모든 필름 비활성화 완료");
    }
}
