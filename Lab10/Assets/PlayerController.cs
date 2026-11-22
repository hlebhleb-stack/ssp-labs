using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;      // скорость движения
    public float rotateSpeed = 180f;  // скорость вращения (градусов в секунду)

    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        // Вращение вокруг оси Z: Q влево, E вправо
        if (Input.GetKey(KeyCode.Q))
            transform.Rotate(0, 0, rotateSpeed * Time.deltaTime);
        if (Input.GetKey(KeyCode.E))
            transform.Rotate(0, 0, -rotateSpeed * Time.deltaTime);
    }

    void FixedUpdate()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        Vector2 movement = new Vector2(horizontal, vertical);

        // Задание 1: ускорение x2 при зажатом Shift
        float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? moveSpeed * 2 : moveSpeed;

        // Задание 3: движение через физику
        // В Unity 6 свойство называется linearVelocity, в старых версиях (2022 и ниже) rb.velocity
        rb.linearVelocity = movement * currentSpeed;
    }
}
