using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ImageCycleAnimation : MonoBehaviour
{
    public Sprite[] images;
    public Image image;
    public SpriteRenderer spriteRenderer;
    public SpriteMask spriteMask;

    public SpriteRenderer[] spriteRenderers; // for multiple sprite renderers to cycle through the same images, if needed

    public float cycleInterval = 0.25f;

    public bool randomizeStart = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void OnEnable()
    {
        if (randomizeStart)
            index = Random.Range(0, images.Length);
        StopAllCoroutines();
        StartCoroutine(AnimateImages());
    }
    int index = 0;

    IEnumerator AnimateImages()
    {
        while (true)
        {
            if (image)
                image.sprite = images[index];

            if (spriteRenderer)
                spriteRenderer.sprite = images[index];

            if (spriteMask)
            { 
                spriteMask.sprite = images[index];
            }

            if (spriteRenderers.Length > 0)
            {
                foreach (SpriteRenderer sr in spriteRenderers)
                {
                    if (sr != null)
                        sr.sprite = images[index];
                }
            }

            index = (index + 1) % images.Length;
            yield return new WaitForSecondsRealtime(cycleInterval);
        }
    }
}
