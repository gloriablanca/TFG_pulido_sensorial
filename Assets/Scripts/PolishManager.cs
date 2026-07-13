using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PolishManager : MonoBehaviour
{
    public static PolishManager Instance { get; private set; }

    [Header("Capas")]
    public bool enemigoReacciona;
    public bool espadaExpresiva;
    public bool camaraResponde;
    public bool sonido;
    public bool interfaz;
    public bool mundo;

    bool globalToggle;

    [Header("UI de toggles")]
    [SerializeField] List<Image> toggleUIs = new List<Image>();

    [Header("Capa 6")]
    [SerializeField] GameObject globalVolume;
    [SerializeField] GameObject defaultLight;
    [SerializeField] GameObject polishedLight;
    [SerializeField] Material[] capsuleMaterials;
    [SerializeField] GameObject firefliesParticleSystem;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        ActivarTodas(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            globalToggle = !globalToggle;
            ActivarTodas(globalToggle);
        }

        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) enemigoReacciona = !enemigoReacciona;
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) espadaExpresiva  = !espadaExpresiva;
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) camaraResponde   = !camaraResponde;
        if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) sonido           = !sonido;
        if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5)) interfaz         = !interfaz;
        if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6)) mundo            = !mundo;

        ToggleUI(0, enemigoReacciona);
        ToggleUI(1, espadaExpresiva);
        ToggleUI(2, camaraResponde);
        ToggleUI(3, sonido);
        ToggleUI(4, interfaz);
        ToggleUI(5, mundo);

        //a partir de aquí lo de la capa 6!!!
        if (mundo)
        {
            globalVolume.SetActive(true);
            polishedLight.SetActive(true);
            defaultLight.SetActive(false);
            firefliesParticleSystem.SetActive(true);

            foreach (var mat in capsuleMaterials)
            {
                mat.SetFloat("_Metallic", 0.65f);
                mat.SetFloat("_Smoothness", 0.55f);
            }
        }
        else
        {
            globalVolume.SetActive(false);
            polishedLight.SetActive(false);
            defaultLight.SetActive(true);
            firefliesParticleSystem.SetActive(false);

            foreach (var mat in capsuleMaterials)
            {
                mat.SetFloat("_Metallic", 0f);
                mat.SetFloat("_Smoothness", 0.5f);
            }
        }
    }

    void ActivarTodas(bool activas)
    {
        enemigoReacciona = activas;
        espadaExpresiva  = activas;
        camaraResponde   = activas;
        sonido           = activas;
        interfaz         = activas;
        mundo            = activas;
    }

    //pinta el HUD
    public void ToggleUI(int idx, bool t)
    {
        if (t)
            toggleUIs[idx].color = new Color(0, 0, 0, .9f);
        else
            toggleUIs[idx].color = new Color(0, 0, 0, .4f);
    }
}
