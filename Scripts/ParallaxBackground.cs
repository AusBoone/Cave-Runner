using UnityEngine;

/// <summary>
/// Loops a background sprite horizontally to create a parallax scrolling
/// effect. The scroll speed is tied to the <see cref="GameManager"/> so it
/// matches the game's current speed.
/// </summary>
public class ParallaxBackground : MonoBehaviour
{
    public float scrollSpeed = 0.5f;
    public float resetPosition = -20f;
    public float startPosition = 20f;

    // Optional sprite name loaded from Assets/Art/Resources at runtime.
    public string spriteName;

    /// <summary>
    /// Optionally loads a sprite by name before the first frame.
    /// </summary>
    void Awake()
    {
        if (!string.IsNullOrEmpty(spriteName))
        {
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                Sprite loaded = Resources.Load<Sprite>("Art/" + spriteName);
                if (loaded != null)
                {
                    sr.sprite = loaded;
                }
            }
        }
    }

    /// <summary>
    /// Moves the background left each frame and wraps it back to the
    /// starting position once it reaches <see cref="resetPosition"/>.
    /// </summary>
    void Update()
    {
        float speed = GameManager.Instance != null ? GameManager.Instance.GetSpeed() : scrollSpeed;
        transform.Translate(Vector3.left * speed * Time.deltaTime);

        if (transform.position.x <= resetPosition)
        {
            Vector3 newPos = transform.position;
            newPos.x = startPosition;
            transform.position = newPos;
        }
    }
}
