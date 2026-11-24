using UnityEngine;

public class EnemyController : MonoBehaviour
{
    public float speed = 2f;
    [SerializeField] private Transform target;
    [SerializeField, Min(0f)] private float stoppingDistance;

    void Start()
    {
        if (target == null)
            target = GameObject.Find("Player")?.transform;
    }

    // Задание 2: движение к цели с постоянной скоростью
    void Update()
    {
        if (target != null)
        {
            Vector2 position = transform.position;
            Vector2 targetPosition = target.position;
            float remainingDistance = Vector2.Distance(position, targetPosition) - stoppingDistance;
            float step = Mathf.Min(speed * Time.deltaTime, Mathf.Max(0f, remainingDistance));
            transform.position = Vector2.MoveTowards(position, targetPosition, step);
        }
    }
}
