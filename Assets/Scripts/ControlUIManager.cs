using UnityEngine;
using UnityEngine.UI;

public class ControlUIManager : MonoBehaviour
{
    [SerializeField] Image wImage;
    [SerializeField] Image aImage;
    [SerializeField] Image sImage;
    [SerializeField] Image dImage;
    [SerializeField] Image spaceImage;
    [SerializeField] Image clickImage;

    void Update()
    {
        //la capa 5 decide si se ven los controles, mismo estado para todas las teclas
        bool mostrar = PolishManager.Instance.interfaz;
        wImage.gameObject.SetActive(mostrar);
        aImage.gameObject.SetActive(mostrar);
        sImage.gameObject.SetActive(mostrar);
        dImage.gameObject.SetActive(mostrar);
        spaceImage.gameObject.SetActive(mostrar);
        clickImage.gameObject.SetActive(mostrar);

        //al pulsar una tecla sube el alfa y parece q se ilumina y al soltarla vuelve a estar flojito
        if (Input.GetKeyDown(KeyCode.W)) wImage.color = new Color(1, 1, 1, .75f);
        if (Input.GetKeyUp(KeyCode.W))   wImage.color = new Color(1, 1, 1, .2f);

        if (Input.GetKeyDown(KeyCode.A)) aImage.color = new Color(1, 1, 1, .75f);
        if (Input.GetKeyUp(KeyCode.A))   aImage.color = new Color(1, 1, 1, .2f);

        if (Input.GetKeyDown(KeyCode.S)) sImage.color = new Color(1, 1, 1, .75f);
        if (Input.GetKeyUp(KeyCode.S))   sImage.color = new Color(1, 1, 1, .2f);

        if (Input.GetKeyDown(KeyCode.D)) dImage.color = new Color(1, 1, 1, .75f);
        if (Input.GetKeyUp(KeyCode.D))   dImage.color = new Color(1, 1, 1, .2f);

        if (Input.GetKeyDown(KeyCode.Space)) spaceImage.color = new Color(1, 1, 1, .75f);
        if (Input.GetKeyUp(KeyCode.Space))   spaceImage.color = new Color(1, 1, 1, .2f);

        if (Input.GetMouseButtonDown(0)) clickImage.color = new Color(1, 1, 1, .75f);
        if (Input.GetMouseButtonUp(0))   clickImage.color = new Color(1, 1, 1, .2f);
    }
}
