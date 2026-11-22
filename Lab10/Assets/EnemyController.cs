using UnityEngine;

public class EnemyController : MonoBehaviour
{
    public float speed = 2f;
    private Transform player;

    void Start()
    {
        player = GameObject.Find("Player").transform;
    }

    // Задание 2: движение к игроку с постоянной скоростью
    void Update()
    {
        if (player != null)
        {
            transform.position = Vector2.MoveTowards(
                transform.position,
                player.position,
                speed * Time.deltaTime
            );
        }
    }
}
