using System.Collections;
using UnityEngine;

public class CabinetFallController : MonoBehaviour
{
    [Header("Sprites")]
    [SerializeField] private Sprite standingSprite;
    [SerializeField] private Sprite fallingSprite1;
    [SerializeField] private Sprite fallingSprite2;
    [SerializeField] private Sprite fallenSprite;

    [Header("Animation")]
    [SerializeField] private float frameDuration = 0.18f;

    [Header("Perspective")]
    [SerializeField] private float finalScaleMultiplier = 1.18f;
    [SerializeField] private float forwardOffset = 0.35f;

    private SpriteRenderer spriteRenderer;

    private bool hasFallen = false;
    private Coroutine fallCoroutine;

    private Vector3 startScale;
    private Vector3 startPosition;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        startScale = transform.localScale;
        startPosition = transform.localPosition;

        if (spriteRenderer != null && standingSprite != null)
        {
            spriteRenderer.sprite = standingSprite;
        }
    }

    private void Update()
    {
        if (GameTimelineManager.Instance == null)
            return;

        GameStage stage = GameTimelineManager.Instance.CurrentStage;

        if (stage == GameStage.Stage2_Tremor && !hasFallen)
        {
            fallCoroutine = StartCoroutine(FallAnimation());
        }

        if (stage == GameStage.Stage1_Preparation && hasFallen)
        {
            ResetCabinet();
        }
    }

    private IEnumerator FallAnimation()
    {
        hasFallen = true;

        // 1 - Ayakta
        spriteRenderer.sprite = standingSprite;
        SetPerspective(0f);

        yield return new WaitForSecondsRealtime(frameDuration);

        // 2 - Hafif öne düşmüş
        spriteRenderer.sprite = fallingSprite1;
        SetPerspective(0.3f);

        yield return new WaitForSecondsRealtime(frameDuration);

        // 3 - Daha fazla öne düşmüş
        spriteRenderer.sprite = fallingSprite2;
        SetPerspective(0.65f);

        yield return new WaitForSecondsRealtime(frameDuration);

        // 4 - Tamamen yerde
        spriteRenderer.sprite = fallenSprite;
        SetPerspective(1f);
    }

    private void SetPerspective(float amount)
    {
        float scale = Mathf.Lerp(1f, finalScaleMultiplier, amount);

        transform.localScale = startScale * scale;

        transform.localPosition =
            startPosition + Vector3.down * (forwardOffset * amount);
    }

    private void ResetCabinet()
    {
        if (fallCoroutine != null)
        {
            StopCoroutine(fallCoroutine);
            fallCoroutine = null;
        }

        hasFallen = false;

        transform.localScale = startScale;
        transform.localPosition = startPosition;

        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = standingSprite;
        }
    }
}