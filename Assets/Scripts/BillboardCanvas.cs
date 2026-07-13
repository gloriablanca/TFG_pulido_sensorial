//orienta un canvas del mundo para que mire siempre a la cámara

using UnityEngine;

public class BillboardCanvas : MonoBehaviour
{
    private Camera camara;

    void Start()
    {
        //guardamos la cámara principal una sola vez (el billboard la usa cada frame)
        if (camara == null)
            camara = Camera.main;
    }

    void LateUpdate()
    {
        // la cámara ya ha terminado de moverse este frame, así el canvas se alinea con la orientación final y no pega tirones
        if (camara != null)
            transform.forward = camara.transform.forward;
    }
}
