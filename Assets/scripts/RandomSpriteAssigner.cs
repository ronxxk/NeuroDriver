using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class RandomSpriteAssigner : MonoBehaviour
{
    [Header("Sprite Pools")]
    [SerializeField] private Sprite[] spritePool;

    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        // Get the SpriteRenderer component attached to this GameObject
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        AssignRandomSprite();
    }

    /// <summary>
    /// Picks a random sprite from the pool and assigns it to the SpriteRenderer.
    /// </summary>
    public void AssignRandomSprite()
    {
        // Check if the pool is empty to avoid errors
        if (spritePool == null || spritePool.Length == 0)
        {
            Debug.LogWarning($"No sprites assigned in the pool on {gameObject.name}!");
            return;
        }

        // Pick a random index from the array
        int randomIndex = Random.Range(0, spritePool.Length);

        // Assign the random sprite
        spriteRenderer.sprite = spritePool[randomIndex];
    }
}