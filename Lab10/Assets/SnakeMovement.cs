using UnityEngine;

public class SnakeMovement : MonoBehaviour
{
    private static readonly Vector2[] Waypoints =
    {
        new Vector2(-3f, 3f), new Vector2(3f, 3f),
        new Vector2(3f, 1f), new Vector2(-3f, 1f),
        new Vector2(-3f, -1f), new Vector2(3f, -1f),
        new Vector2(3f, -3f), new Vector2(-3f, -3f),
        new Vector2(-4f, -3f), new Vector2(-4f, 3f)
    };

    [SerializeField, Min(0f)] private float speed = 2f;
    [SerializeField, Min(0f)] private float startDistance;
    [SerializeField] private SnakeMovement target;
    [SerializeField, Min(0f)] private float stoppingDistance;

    private static readonly float RouteLength = GetRouteLength();
    private Rigidbody2D body;
    private float distanceTravelled;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        distanceTravelled = startDistance;
    }

    void FixedUpdate()
    {
        float step = speed * Time.fixedDeltaTime;
        if (target != null)
        {
            float remaining = target.distanceTravelled - distanceTravelled - stoppingDistance;
            step = Mathf.Min(step, Mathf.Max(0f, remaining));
        }

        distanceTravelled += step;
        Vector2 position = GetRoutePosition(distanceTravelled % RouteLength);

        if (body != null)
            body.MovePosition(position);
        else
            transform.position = new Vector3(position.x, position.y, transform.position.z);
    }

    private static float GetRouteLength()
    {
        float length = 0f;
        for (int index = 0; index < Waypoints.Length; index++)
            length += Vector2.Distance(Waypoints[index], Waypoints[(index + 1) % Waypoints.Length]);

        return length;
    }

    private static Vector2 GetRoutePosition(float distance)
    {
        for (int index = 0; index < Waypoints.Length; index++)
        {
            Vector2 start = Waypoints[index];
            Vector2 end = Waypoints[(index + 1) % Waypoints.Length];
            float segmentLength = Vector2.Distance(start, end);

            if (distance <= segmentLength)
                return Vector2.MoveTowards(start, end, distance);

            distance -= segmentLength;
        }

        return Waypoints[0];
    }
}
