using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField, Range(1f, 20f)]   private float velocidad = 6f;
    //grados por segundo que puede girar el jugador
    [SerializeField, Range(60f, 720f)] private float velocidadGiro = 360f;

    private Camera cam;

    void Start()
    {
        cam = Camera.main;
    }

    void Update()
    {
        //REVISAR esto es una liada cuidado con las flechas!!!
        float h = Input.GetAxisRaw("Horizontal");//movimiento AD
        float v = Input.GetAxisRaw("Vertical");//movimento WS

        if (h == 0f && v == 0f) return;//sin input q no haga nada

        //proyecto los ejes para que el movimiento ignore la inclinación de la cámara
        Vector3 adelante = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        Vector3 derecha  = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized;

        Vector3 direccion = (adelante * v + derecha * h).normalized;

        //desplazamiento
        transform.position += direccion * velocidad * Time.deltaTime;

        //rotación hacia la dirección de movimiento (por el arco corto claro)
        //vale a ver un mismo giro tiene dos cuaterniones antipodales (q y -q) entonces si el destino está en
        //el hemisferio opuesto al actual, RotateTowards recorrería el arco largo
        //si niego el destino cuando dot < 0 se fuerza siempre el arco corto (≤ 180°)
        Quaternion rotDestino = Quaternion.LookRotation(direccion);
        if (Quaternion.Dot(transform.rotation, rotDestino) < 0f)
            rotDestino = new Quaternion(-rotDestino.x, -rotDestino.y,
                                        -rotDestino.z, -rotDestino.w);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, rotDestino, velocidadGiro * Time.deltaTime);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other == null) return;
        if (other.gameObject == this) return;

        if (other.gameObject.CompareTag("HitCollider"))
        {
            Debug.Log("Me han pegao :(");
        }
    }
}
